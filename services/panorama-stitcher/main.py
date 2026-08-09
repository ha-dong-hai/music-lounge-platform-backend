"""
MusicLounge Panorama Stitcher — small standalone microservice.

Deliberately kept OUT of the main .NET backend process: OpenCV's native panorama-stitching
module is well-supported and battle-tested in Python (opencv-python-headless), but the .NET
binding (OpenCvSharp) ships OS-version-pinned native binaries for Linux (e.g. tied to a specific
Ubuntu point release) with no generic "linux-x64" package. Since the target cloud host for the
main backend isn't locked in yet, isolating that native/OS risk to this small, independently
deployable service means a future infra choice for the .NET app can't break stitching, and vice
versa. The .NET backend calls this over plain HTTP (IPanoramaStitchingService), the same shape
it already uses for the Gemini/OpenAI integrations.

Two-tier stitching: cv2.Stitcher first (fast, in-process, no temp files) — if that fails, falls
back to a Hugin CLI pipeline (pto_gen -> cpfind -> autooptimiser -> pano_modify -> nona ->
enblend), which multiple independent reviews rate as producing better results than OpenCV's
built-in stitcher on harder cases (fisheye, uneven exposure, weak feature overlap) — exactly the
kind of amateur handheld-phone photos this feature's actual users will submit. Hugin's tools work
on files, not in-memory arrays, hence the tempfile.TemporaryDirectory() usage below.

Pre-flight connectivity check: real testing found cv2.Stitcher can silently join only a SUBSET of
the given photos (confirmed reproducible: same 6 real test photos, wildly different outcomes call
to call) while still returning Stitcher_OK, with no way for the caller to know some photos were
dropped - Stitcher.component()/.cameras() (which images were actually used) aren't exposed by
this Python binding at all (checked: not in dir(stitcher)). Reverse-engineering that decision via
cv2.detail (OpenCV's lower-level stitching building blocks) was tried and DIDN'T match Stitcher's
real behavior even after matching its documented registrationResol/panoConfidenceThresh defaults.

Researched the actual root cause rather than guessing further: this is a known, still-unresolved
weakness of cv2.Stitcher going back years (opencv/opencv#4591, #21010, #22125, #22447) - traced by
OpenCV's own issue tracker to the FLANN matcher's internal randomness, which cv2.setRNGSeed does
NOT control (confirmed separately: opencv/opencv#24835, "RANSAC is insensitive to setRNGSeed").
Verified directly against our own real test photos that neither setRNGSeed nor disabling OpenCL
(cv2.ocl.setUseOpenCL(False) - also implicated in some reports) eliminates the run-to-run
variance. There is no clean single-parameter fix; treating cv2.Stitcher as reliably steerable via
a seed was the wrong model. Since this is a chronic, multi-year unfixed issue rather than
something fixable at the application level, cv2.Stitcher is now used ONLY as a cheap opportunistic
first attempt, gated by real evidence instead of blind retries.

Hugin's cpfind, which this service already runs as the fallback path, is the answer instead: it
writes real, inspectable control-point counts per image pair into the .pto project file - actual
ground truth from a real external tool, not a guess, and confirmed stable across repeated runs
(exact match counts wiggle slightly, but which images belong together never changed across 3
repeated runs on the same photos - unlike cv2.Stitcher's wild swings). Running it as a pre-flight
check (before ever calling cv2.Stitcher) tells us definitively which photos have enough overlap
with the rest, so an Owner gets a precise "photo #N doesn't overlap enough" error instead of a
silently incomplete "successful" panorama - and the same already-computed control points feed
straight into the Hugin blend pipeline when cv2.Stitcher's opportunistic attempt comes up short.
"""

import os

# Found via a real crash, not preemptively: stitching a larger image set (14 photos covering a
# full sphere, not just a 6-photo horizontal ring) hit "OpenCL error CL_OUT_OF_RESOURCES" inside
# cv2.Stitcher's buildWarpSphericalMaps at a ~4350x2070 warp buffer size, which OpenCV's OpenCL
# error handling could not recover from — it took down the entire uvicorn worker process (a hard
# C++ terminate, not a catchable Python exception), failing every other in-flight request too.
# The in-process cv2.ocl.setUseOpenCL(False) call does NOT prevent this crash (confirmed: still
# crashed with that call in place) — OpenCL must be disabled before OpenCV's native library loads
# and detects it, which these two env vars do, set before `import cv2` so it's self-contained
# regardless of how this process gets launched (no reliance on Dockerfile/shell env setup).
os.environ["OPENCV_OPENCL_RUNTIME"] = "disabled"
os.environ["OPENCV_OPENCL_DEVICE"] = "null"

import io
import subprocess
import tempfile
from collections import defaultdict

import cv2
import numpy as np
import requests
from fastapi import FastAPI, HTTPException
from fastapi.responses import Response
from PIL import Image, ImageOps
from pydantic import BaseModel

app = FastAPI(title="MusicLounge Panorama Stitcher")

# cv2.Stitcher status codes (stable since OpenCV 3.x) — surfaced as a specific reason instead of
# a bare error code, since the caller (an Owner who just took some phone photos) needs to know
# WHAT to fix, not a number.
STITCH_ERROR_REASONS = {
    1: "Không đủ điểm chung giữa các ảnh — ảnh có thể chụp quá xa nhau hoặc không đủ phần trùng lặp (overlap) giữa các tấm liền kề.",
    2: "Không xác định được góc chụp giữa các ảnh — ảnh có thể bị mờ, thiếu chi tiết, hoặc chụp lệch quá nhiều so với việc chỉ xoay tại chỗ.",
    3: "Không cân chỉnh được độ phơi sáng giữa các ảnh.",
}

# Hugin CLI tools — Windows (winget install Hugin.Hugin) puts them here; the Linux/Docker image
# installs the `hugin-tools` + `enblend` apt packages instead, which land on PATH directly.
_HUGIN_WIN_BIN = r"C:\Program Files\Hugin\bin"


def _hugin_tool(name: str) -> str:
    win_path = os.path.join(_HUGIN_WIN_BIN, f"{name}.exe")
    return win_path if os.path.exists(win_path) else name


class StitchRequest(BaseModel):
    image_urls: list[str]


@app.get("/health")
def health():
    return {"status": "ok"}


def _download_and_correct_orientation(url: str) -> np.ndarray:
    """Downloads one image and returns it as a BGR numpy array, with EXIF orientation already
    baked into the pixels. cv2.imdecode reads raw pixel data and ignores the EXIF Orientation
    tag entirely — a phone that shot in portrait but stores the sensor's native landscape pixels
    (relying on the tag for display) would otherwise hand cv2.Stitcher a set of images that look
    correctly oriented in any normal viewer but are actually rotated relative to each other,
    which silently tanks feature-matching. This is a routine real-world failure mode for phone
    photos specifically, not a hypothetical edge case."""
    try:
        resp = requests.get(url, timeout=20)
        resp.raise_for_status()
    except Exception as e:
        raise HTTPException(400, f"Không tải được ảnh {url}: {e}")

    try:
        pil_img = Image.open(io.BytesIO(resp.content))
        pil_img = ImageOps.exif_transpose(pil_img)  # rotates/flips pixels per EXIF, drops the tag
        pil_img = pil_img.convert("RGB")
    except Exception:
        raise HTTPException(400, f"Không đọc được ảnh {url} — file có thể bị hỏng hoặc sai định dạng.")

    return cv2.cvtColor(np.array(pil_img), cv2.COLOR_RGB2BGR)


# cv2.Stitcher's run-to-run variance comes from FLANN matcher internals that cv2.setRNGSeed does
# not control (see module docstring - researched via OpenCV's own issue tracker rather than
# guessed) - so retrying it with different seeds has no real theoretical grounding, confirmed by
# testing: disabling OpenCL and cycling seeds both failed to make it reliable on our own real test
# photos. It's used here for exactly one cheap opportunistic attempt; whether to trust the result
# is decided by the caller against real connectivity evidence, not by hoping a retry helps.
def _stitch_with_opencv(images: list[np.ndarray]) -> tuple[bool, np.ndarray | None, str | None]:
    stitcher = cv2.Stitcher_create(cv2.Stitcher_PANORAMA)
    status, result = stitcher.stitch(images)
    if status != cv2.Stitcher_OK:
        return False, None, STITCH_ERROR_REASONS.get(status, f"Ghép ảnh thất bại (mã lỗi OpenCV: {status}).")
    return True, result, None


def _run(cmd: list[str], cwd: str) -> None:
    result = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=120)
    if result.returncode != 0:
        raise RuntimeError(f"{cmd[0]} thất bại: {result.stderr.strip() or result.stdout.strip()}")


# Below this many control points, treat a pair as noise rather than a real overlap — chosen
# conservatively low so it never breaks a genuinely-connected set: on real test data the weakest
# TRUE connecting edge in a fully-usable 6-photo set still had 2 matches, but that image was also
# strongly linked (14-17 matches) via its other neighbor, so raising the bar to 4 here filters
# spurious matches without ever discarding the edge that was actually load-bearing.
_MIN_CONTROL_POINTS = 4


def _parse_pto_connectivity(pto_path: str, n_images: int) -> list[set[int]]:
    """Reads the real control-point counts Hugin's cpfind wrote into the .pto project file (each
    "c n<i> N<j> ..." line is one matched keypoint pair between image i and image j) and groups
    images into connected components by union-find. This is actual output from a real tool, not
    a guess at what any stitcher's internals decided."""
    pair_counts: dict[tuple[int, int], int] = defaultdict(int)
    with open(pto_path, "r", encoding="utf-8", errors="ignore") as f:
        for line in f:
            if not line.startswith("c "):
                continue
            n_val = j_val = None
            for tok in line.split():
                if tok[:1] == "n" and tok[1:].isdigit():
                    n_val = int(tok[1:])
                elif tok[:1] == "N" and tok[1:].isdigit():
                    j_val = int(tok[1:])
            if n_val is not None and j_val is not None:
                key = (min(n_val, j_val), max(n_val, j_val))
                pair_counts[key] += 1

    parent = list(range(n_images))

    def find(x: int) -> int:
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    def union(a: int, b: int) -> None:
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb

    for (i, j), count in pair_counts.items():
        if count >= _MIN_CONTROL_POINTS:
            union(i, j)

    groups: dict[int, set[int]] = defaultdict(set)
    for i in range(n_images):
        groups[find(i)].add(i)
    return list(groups.values())


def _check_connectivity(work_dir: str, input_paths: list[str]) -> list[int]:
    """Runs Hugin's cpfind against the images already written to work_dir and returns the 0-based
    indices of any photos that don't share enough overlap with the rest to be usable (empty list
    = every photo is connected to every other photo, directly or transitively)."""
    pto = os.path.join(work_dir, "project.pto")
    _run([_hugin_tool("pto_gen"), "-o", pto, *input_paths], work_dir)
    _run([_hugin_tool("cpfind"), "--multirow", "-o", pto, pto], work_dir)

    groups = _parse_pto_connectivity(pto, len(input_paths))
    if len(groups) <= 1:
        return []
    largest = max(groups, key=len)
    return sorted(i for g in groups if g is not largest for i in g)


# autooptimiser's bundle adjustment (-a) can converge to a bad local minimum on weakly-connected
# image chains and flip a SUBSET of images ~180 degrees in roll while staying internally
# consistent with each other - confirmed directly on our own real test photos (roll values came
# back as [51, -14, -147, -172, -178, -178] - a ~229 degree range - for 6 photos that all truly
# share the same roll, since none of them were ever rotated relative to each other). enblend then
# blends that geometry as-is, producing a wide but visibly corrupted panorama (upside-down
# sections, severe color/exposure artifacts from misaligned overlaps) - worse than not stitching
# at all, since it LOOKS like a complete result. A real handheld phone panorama can have genuine
# roll variation from an unsteady hand, but nothing close to this - 90 degrees is a generous
# margin above plausible handheld tilt while comfortably below the actual failure observed.
_MAX_PLAUSIBLE_ROLL_RANGE_DEG = 90.0


def _read_roll_values(pto_path: str) -> list[float]:
    rolls = []
    with open(pto_path, "r", encoding="utf-8", errors="ignore") as f:
        for line in f:
            if not line.startswith("i "):
                continue
            for tok in line.split():
                if tok[:1] == "r" and tok[1:].replace("-", "", 1).replace(".", "", 1).isdigit():
                    rolls.append(float(tok[1:]))
                    break
    return rolls


def _stitch_with_hugin_from_pto(work_dir: str) -> np.ndarray | None:
    """Runs the rest of the Hugin blend pipeline against the project.pto that _check_connectivity
    already produced in work_dir (avoids re-running pto_gen/cpfind a second time). Returns None
    (never raises) on any failure — this is the SECOND attempt after cv2.Stitcher, so a Hugin
    failure here just means "both approaches couldn't do it"."""
    try:
        pto = os.path.join(work_dir, "project.pto")

        # Geometry-only pass first, so a bad alignment can be caught BEFORE spending time
        # blending it into a corrupted image.
        _run([_hugin_tool("autooptimiser"), "-a", "-o", pto, pto], work_dir)
        roll_values = _read_roll_values(pto)
        if roll_values and (max(roll_values) - min(roll_values)) > _MAX_PLAUSIBLE_ROLL_RANGE_DEG:
            return None

        _run([_hugin_tool("autooptimiser"), "-m", "-l", "-s", "-o", pto, pto], work_dir)
        _run([_hugin_tool("pano_modify"), "--canvas=AUTO", "--crop=AUTO", "-o", pto, pto], work_dir)

        nona_prefix = os.path.join(work_dir, "remapped")
        _run([_hugin_tool("nona"), "-o", nona_prefix, "-m", "TIFF_m", pto], work_dir)

        remapped_files = sorted(
            os.path.join(work_dir, f) for f in os.listdir(work_dir)
            if f.startswith("remapped") and f.endswith(".tif")
        )
        if len(remapped_files) < 2:
            return None

        blended = os.path.join(work_dir, "blended.tif")
        _run([_hugin_tool("enblend"), "-o", blended, *remapped_files], work_dir)

        return cv2.imread(blended, cv2.IMREAD_COLOR)
    except Exception:
        return None


@app.post("/stitch")
def stitch(req: StitchRequest):
    if len(req.image_urls) < 2:
        raise HTTPException(400, "Cần ít nhất 2 ảnh để ghép panorama.")

    images = [_download_and_correct_orientation(url) for url in req.image_urls]

    with tempfile.TemporaryDirectory() as work_dir:
        input_paths = []
        for i, img in enumerate(images):
            path = os.path.join(work_dir, f"in{i}.jpg")
            cv2.imwrite(path, img)
            input_paths.append(path)

        disconnected = _check_connectivity(work_dir, input_paths)
        if disconnected:
            positions = ", ".join(f"#{i + 1}" for i in disconnected)
            raise HTTPException(
                422,
                f"Ảnh {positions} không tìm đủ điểm trùng khớp với các ảnh còn lại — có thể do "
                "chụp cách quá xa hoặc không đủ phần chồng lấn (overlap) với ảnh liền kề. Hãy "
                "chụp lại ảnh này gối lên ảnh bên cạnh nhiều hơn rồi thử lại.",
            )

        ok, result, error_reason = _stitch_with_opencv(images)

        # cv2.Stitcher can "succeed" while having silently joined only a fraction of the images
        # (see module docstring) — connectivity already confirmed all N photos genuinely overlap,
        # so a result much narrower than that implies is a sign this run got unlucky, not that
        # the room is actually smaller. The 1.3x threshold is an empirical margin from real
        # testing (bad runs landed ~1.2x one image's width, good runs ~2x or more) — a heuristic,
        # not an exact measure, and worth revisiting once more real photo sets have gone through this.
        avg_width = sum(img.shape[1] for img in images) / len(images)
        looks_complete = ok and result is not None and result.shape[1] >= avg_width * 1.3

        if not looks_complete:
            hugin_result = _stitch_with_hugin_from_pto(work_dir)
            if hugin_result is not None:
                result = hugin_result
            elif not ok:
                # Both attempts failed despite good connectivity — surface OpenCV's reason, since
                # it names an actual cause (exposure mismatch, blurry images...) an Owner can act
                # on, unlike a generic "Hugin also failed" message.
                raise HTTPException(422, error_reason)
            # else: cv2.Stitcher's (narrow) result is kept — Hugin couldn't improve on it, and
            # some result beats none when connectivity already confirmed the room does overlap.

    ok_encode, buf = cv2.imencode(".jpg", result, [cv2.IMWRITE_JPEG_QUALITY, 90])
    if not ok_encode:
        raise HTTPException(500, "Ghép ảnh thành công nhưng không mã hóa được ảnh kết quả.")

    return Response(content=buf.tobytes(), media_type="image/jpeg")
