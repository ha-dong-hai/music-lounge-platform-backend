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

cpfind's matching is still classical computer vision (SIFT-like local keypoints), which is a
known weak point on dim, low-texture, repetitive-pattern indoor scenes - exactly what a bar/
lounge interior looks like. Rather than replace Hugin wholesale, a narrow AI-based rescue
(_rescue_disconnected_with_loftr) runs LoFTR - a deep-learning matcher that doesn't depend on
locally-distinctive keypoints - ONLY on the specific pairs cpfind already failed to connect, and
only to write more standard .pto control-point lines that feed the exact same downstream
pipeline. This keeps the common case (cpfind connects everything) exactly as fast as before,
while giving genuinely-overlapping-but-textureless photo pairs a second, AI-assisted chance
before an Owner is told to retake a photo that may not have needed retaking.
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
import threading
import time
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


# Pillow's own Image.MAX_IMAGE_PIXELS guard (decompression-bomb protection, ~89 megapixels by
# default) only kicks in once the file is already fully decoded - but requests.get(url) by
# default buffers the ENTIRE response body into memory first (resp.content), before Pillow ever
# gets a look at it. A URL serving a multi-gigabyte file (bomb or not) would exhaust memory/
# bandwidth at the download step alone. Streamed with an explicit cap instead - 25MB is generous
# for a single phone photo (even high-res JPEGs are typically 2-15MB) while bounding the worst case.
_MAX_DOWNLOAD_BYTES = 25 * 1024 * 1024


def _download_and_correct_orientation(url: str) -> np.ndarray:
    """Downloads one image and returns it as a BGR numpy array, with EXIF orientation already
    baked into the pixels. cv2.imdecode reads raw pixel data and ignores the EXIF Orientation
    tag entirely — a phone that shot in portrait but stores the sensor's native landscape pixels
    (relying on the tag for display) would otherwise hand cv2.Stitcher a set of images that look
    correctly oriented in any normal viewer but are actually rotated relative to each other,
    which silently tanks feature-matching. This is a routine real-world failure mode for phone
    photos specifically, not a hypothetical edge case."""
    try:
        with requests.get(url, timeout=20, stream=True) as resp:
            resp.raise_for_status()
            content_length = resp.headers.get("Content-Length")
            if content_length is not None and int(content_length) > _MAX_DOWNLOAD_BYTES:
                raise HTTPException(400, f"Ảnh {url} vượt quá dung lượng cho phép (tối đa 25MB).")

            chunks = []
            total = 0
            for chunk in resp.iter_content(chunk_size=65536):
                total += len(chunk)
                if total > _MAX_DOWNLOAD_BYTES:
                    raise HTTPException(400, f"Ảnh {url} vượt quá dung lượng cho phép (tối đa 25MB).")
                chunks.append(chunk)
            content = b"".join(chunks)
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(400, f"Không tải được ảnh {url}: {e}")

    try:
        pil_img = Image.open(io.BytesIO(content))
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


# kornia's LoFTR (not the original academic repo) is used for its Apache-2.0 license and bundled
# pretrained-weight download (no manual checkpoint wrangling) - the 'indoor' checkpoint
# specifically, since it's trained on indoor scenes (ScanNet) rather than 'outdoor' (MegaDepth),
# a direct domain match for venue interiors. Loaded lazily (not at module import time) so a
# broken torch/kornia install only disables the rescue path instead of crashing the whole service
# at startup - and only once per process, guarded by a lock since FastAPI runs sync `def` request
# handlers in a real threadpool, so concurrent requests on a cold process would otherwise race on
# first load.
_loftr_lock = threading.Lock()
_loftr_matcher = None  # sentinel: None = not yet attempted (or attempted and failed)
_loftr_load_failed = False  # sticky - a broken install shouldn't retry every request


def _get_loftr_matcher():
    """Returns the process-wide LoFTR model, loading it on first call. Never raises - returns
    None if torch/kornia aren't installed or the model fails to load, which the rescue path
    treats as "rescue unavailable" rather than an error."""
    global _loftr_matcher, _loftr_load_failed
    if _loftr_matcher is not None or _loftr_load_failed:
        return _loftr_matcher
    with _loftr_lock:
        if _loftr_matcher is not None or _loftr_load_failed:
            return _loftr_matcher
        try:
            import kornia.feature as KF

            model = KF.LoFTR(pretrained="indoor")
            model.eval()
            _loftr_matcher = model
        except Exception:
            _loftr_load_failed = True
    return _loftr_matcher


# LoFTR's normal operating resolution - full phone-camera resolution (often 3000-4000px) is both
# unnecessary for matching and far too slow on CPU (no GPU assumed in this deployment). Its
# backbone downsamples in stride-8 steps, so both dimensions must be a multiple of 8 or the
# forward pass shape-mismatches (confirmed against kornia's LoFTR coarse-feature config).
_LOFTR_TARGET_LONG_SIDE_PX = 840
_LOFTR_SIZE_MULTIPLE = 8

# Below this, LoFTR itself considers a match unreliable.
_LOFTR_CONFIDENCE_THRESHOLD = 0.5
# cv2.findFundamentalMat's underlying algorithm needs at least 8 points to run at all.
_LOFTR_MIN_POINTS_FOR_RANSAC = 8
_LOFTR_RANSAC_REPROJ_THRESHOLD_PX = 3.0
# Caps .pto growth and autooptimiser's bundle-adjustment solve time - LoFTR routinely returns far
# more raw correspondences than are useful to hand it.
_LOFTR_MAX_CONTROL_POINTS_PER_PAIR = 80
# Deliberately well above _MIN_CONTROL_POINTS (4): LoFTR is being trusted in exactly the harder
# regime cpfind already failed on, so the bar to accept its output should be stricter, not equal.
_LOFTR_MIN_MATCHES_TO_TRUST = 20
# Bounds worst-case LoFTR calls per disconnected group and overall rescue wall-clock time - this
# all happens inside the same request as the .NET caller's 120s HttpClient timeout for this
# service (DependencyInjection.cs), shared with the Hugin subprocess calls and cv2.Stitcher.
_LOFTR_MAX_ATTEMPTS_PER_GROUP = 6
_LOFTR_RESCUE_TIME_BUDGET_SECONDS = 60.0

# Starting points reasoned from LoFTR's own documented behavior, NOT yet calibrated against real
# failing photo sets the way every other threshold in this file is (see _MIN_CONTROL_POINTS,
# _MAX_PLAUSIBLE_ROLL_RANGE_DEG, _MIN_COVERAGE_FRACTION). Revisit these four _LOFTR_* thresholds
# once real dim-lounge test photos that trigger the rescue path are available.


def _load_gray_for_loftr(path: str):
    """Reads an image as grayscale and downscales it to LoFTR's operating resolution (never
    upscales - phone photos are always well above it). Returns (tensor, scale_x, scale_y) where
    tensor has shape (1, 1, H, W) float32 in [0, 1], and scale_x/scale_y map a keypoint found in
    this downscaled tensor back to the ORIGINAL full-resolution image - pto_gen/nona operate on
    the full-res files on disk, not this in-memory downscaled copy. Returns (None, None, None) if
    the image can't be read."""
    import torch

    gray = cv2.imread(path, cv2.IMREAD_GRAYSCALE)
    if gray is None:
        return None, None, None
    orig_h, orig_w = gray.shape
    scale = min(1.0, _LOFTR_TARGET_LONG_SIDE_PX / max(orig_h, orig_w))
    new_h = max(_LOFTR_SIZE_MULTIPLE, int(orig_h * scale) // _LOFTR_SIZE_MULTIPLE * _LOFTR_SIZE_MULTIPLE)
    new_w = max(_LOFTR_SIZE_MULTIPLE, int(orig_w * scale) // _LOFTR_SIZE_MULTIPLE * _LOFTR_SIZE_MULTIPLE)
    resized = cv2.resize(gray, (new_w, new_h), interpolation=cv2.INTER_AREA)
    tensor = torch.from_numpy(resized).float()[None, None] / 255.0
    return tensor, orig_w / new_w, orig_h / new_h


def _loftr_match_pair(path_i: str, path_j: str) -> list[tuple[float, float, float, float, float]]:
    """Matches image path_i against path_j with LoFTR and returns up to
    _LOFTR_MAX_CONTROL_POINTS_PER_PAIR (x_i, y_i, x_j, y_j, confidence) tuples, in FULL-RESOLUTION
    original-image pixel coordinates, sorted by confidence descending. Every point has already
    passed both LoFTR's own confidence threshold and RANSAC geometric verification - cpfind's own
    matches are implicitly RANSAC-verified by Hugin already, so skipping this step here would
    make a rescue point LESS trustworthy than a real cpfind point, backwards for a rescue path
    whose whole job is to be trustworthy enough to substitute for cpfind. Returns [] (never
    raises) on any failure: no LoFTR model available, unreadable image, too few matches, etc."""
    matcher = _get_loftr_matcher()
    if matcher is None:
        return []

    import torch

    try:
        tensor_i, scale_x_i, scale_y_i = _load_gray_for_loftr(path_i)
        tensor_j, scale_x_j, scale_y_j = _load_gray_for_loftr(path_j)
        if tensor_i is None or tensor_j is None:
            return []

        with torch.inference_mode():
            out = matcher({"image0": tensor_i, "image1": tensor_j})

        kpts_i = out["keypoints0"].cpu().numpy()
        kpts_j = out["keypoints1"].cpu().numpy()
        conf = out["confidence"].cpu().numpy()
    except Exception:
        return []

    keep = conf >= _LOFTR_CONFIDENCE_THRESHOLD
    kpts_i, kpts_j, conf = kpts_i[keep], kpts_j[keep], conf[keep]
    if len(kpts_i) < _LOFTR_MIN_POINTS_FOR_RANSAC:
        return []

    try:
        _, inlier_mask = cv2.findFundamentalMat(
            kpts_i, kpts_j, cv2.FM_RANSAC, _LOFTR_RANSAC_REPROJ_THRESHOLD_PX, 0.99
        )
    except cv2.error:
        return []
    if inlier_mask is None:
        return []
    inliers = inlier_mask.ravel().astype(bool)
    kpts_i, kpts_j, conf = kpts_i[inliers], kpts_j[inliers], conf[inliers]
    if len(kpts_i) == 0:
        return []

    order = np.argsort(-conf)[:_LOFTR_MAX_CONTROL_POINTS_PER_PAIR]
    return [
        (
            float(kpts_i[idx, 0] * scale_x_i), float(kpts_i[idx, 1] * scale_y_i),
            float(kpts_j[idx, 0] * scale_x_j), float(kpts_j[idx, 1] * scale_y_j),
            float(conf[idx]),
        )
        for idx in order
    ]


def _append_control_points(
    pto_path: str, img_i: int, img_j: int,
    matches: list[tuple[float, float, float, float, float]],
) -> None:
    """Appends one Hugin .pto control-point line per match, connecting img_i (as lowercase 'n',
    the image passed as LoFTR's image0) to img_j (as uppercase 'N', image1) - the exact format
    cpfind itself writes and _parse_pto_connectivity already parses (tok[:1] == "n"/"N"). Inserted
    right after the last existing 'c ' line (or after the last 'i ' line if none exist), matching
    where cpfind places its own points, so pto_gen/autooptimiser/nona's line-oriented .pto reading
    is unaffected."""
    with open(pto_path, "r", encoding="utf-8", errors="ignore") as f:
        lines = f.readlines()

    new_lines = [
        f"c n{img_i} N{img_j} x{x_i:.2f} y{y_i:.2f} X{x_j:.2f} Y{y_j:.2f} t0\n"
        for (x_i, y_i, x_j, y_j, _conf) in matches
    ]

    insert_at = next(
        (idx + 1 for idx in range(len(lines) - 1, -1, -1) if lines[idx].startswith("c ")), None
    )
    if insert_at is None:
        insert_at = next(
            (idx + 1 for idx in range(len(lines) - 1, -1, -1) if lines[idx].startswith("i ")),
            len(lines),
        )

    lines[insert_at:insert_at] = new_lines
    with open(pto_path, "w", encoding="utf-8") as f:
        f.writelines(lines)


def _rescue_disconnected_with_loftr(
    pto_path: str, input_paths: list[str], groups: list[set[int]]
) -> bool:
    """For every connectivity group EXCEPT the largest, tries LoFTR on candidate
    (minority-image, majority-image) pairs - in deterministic ascending-index order, no
    spatial-adjacency assumption, consistent with why --multirow is already used instead of
    assuming capture order means spatial order - until ONE pair clears _LOFTR_MIN_MATCHES_TO_TRUST
    (then stops for that group: union-find will merge the rest of the minority group in via
    cpfind's own already-trusted internal edges once one bridge exists, so trying every cross-pair
    would only add cost, not connectivity). Appends control points for every pair that succeeds.
    Returns True iff at least one control point was appended (the caller should re-parse
    connectivity); False otherwise - LoFTR unavailable, time budget exhausted, or no candidate
    pair anywhere cleared the bar. Never raises."""
    if _get_loftr_matcher() is None:
        return False

    largest = max(groups, key=len)
    minority_groups = [sorted(g) for g in groups if g is not largest]
    majority = sorted(largest)

    deadline = time.monotonic() + _LOFTR_RESCUE_TIME_BUDGET_SECONDS
    appended_any = False

    for minority in minority_groups:
        attempts = 0
        for i in minority:
            if attempts >= _LOFTR_MAX_ATTEMPTS_PER_GROUP or time.monotonic() >= deadline:
                break
            bridged = False
            for j in majority:
                if attempts >= _LOFTR_MAX_ATTEMPTS_PER_GROUP or time.monotonic() >= deadline:
                    break
                attempts += 1
                matches = _loftr_match_pair(input_paths[i], input_paths[j])
                if len(matches) >= _LOFTR_MIN_MATCHES_TO_TRUST:
                    _append_control_points(pto_path, i, j, matches)
                    appended_any = True
                    bridged = True
                    break
            if bridged:
                break

    return appended_any


def _check_connectivity(work_dir: str, input_paths: list[str]) -> list[int]:
    """Runs Hugin's cpfind against the images already written to work_dir and returns the 0-based
    indices of any photos that don't share enough overlap with the rest to be usable (empty list
    = every photo is connected to every other photo, directly or transitively). If cpfind can't
    connect everything, first tries an AI-based rescue (_rescue_disconnected_with_loftr) on the
    specific disconnected pairs before giving up - see the module docstring for why."""
    pto = os.path.join(work_dir, "project.pto")
    _run([_hugin_tool("pto_gen"), "-o", pto, *input_paths], work_dir)
    _run([_hugin_tool("cpfind"), "--multirow", "-o", pto, pto], work_dir)

    groups = _parse_pto_connectivity(pto, len(input_paths))
    if len(groups) > 1 and _rescue_disconnected_with_loftr(pto, input_paths, groups):
        groups = _parse_pto_connectivity(pto, len(input_paths))  # re-read the now-updated .pto

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

# A stitched result covering only a fraction of the theoretical coverage below this is treated as
# incomplete. Calibrated against real test data: a known-good 6-photo result achieved ~0.51 of its
# theoretical (no-overlap) span, a known-bad (silently-dropped-images) result achieved ~0.29 - 0.4
# sits with margin in between. This will always be well under 1.0 since real photos overlap
# (theoretical span assumes none), not a bug in the math.
_MIN_COVERAGE_FRACTION = 0.4


class _ImageGeometry:
    __slots__ = ("yaw", "pitch", "hfov", "vfov")

    def __init__(self, yaw: float, pitch: float, hfov: float, vfov: float):
        self.yaw, self.pitch, self.hfov, self.vfov = yaw, pitch, hfov, vfov


def _read_image_geometry(pto_path: str) -> list[_ImageGeometry]:
    """Parses each image line's actual optimized yaw/pitch/HFOV plus pixel dimensions (to derive
    vertical FOV from HFOV and aspect ratio) out of the .pto file autooptimiser just wrote."""
    geoms = []
    anchor_hfov = 100.0
    with open(pto_path, "r", encoding="utf-8", errors="ignore") as f:
        for line in f:
            if not line.startswith("i "):
                continue
            toks = line.split()
            vals: dict[str, str] = {}
            for tok in toks:
                key = tok[0]
                if key in "wh" and tok[1:].isdigit():
                    vals[key] = tok[1:]
                elif key in "ypv" and (tok[1:].replace("-", "", 1).replace(".", "", 1).isdigit()):
                    vals[key] = tok[1:]
            w, h = float(vals.get("w", 1)), float(vals.get("h", 1))
            hfov = float(vals["v"]) if "v" in vals else anchor_hfov
            if not geoms:
                anchor_hfov = hfov
            yaw, pitch = float(vals.get("y", 0)), float(vals.get("p", 0))
            hfov_rad = np.radians(hfov)
            vfov = np.degrees(2 * np.arctan(np.tan(hfov_rad / 2) * (h / w))) if w else hfov
            geoms.append(_ImageGeometry(yaw, pitch, hfov, vfov))
    return geoms


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


def _optimize_geometry(work_dir: str) -> list[_ImageGeometry] | None:
    """Runs autooptimiser's geometry-only pass (-a) against the project.pto that
    _check_connectivity already produced, and returns per-image geometry IF the resulting roll
    values pass the sanity check - None if the alignment looks unreliable (see
    _MAX_PLAUSIBLE_ROLL_RANGE_DEG above). This is real, independently-computed geometry (not a
    guess) covering every image the connectivity check already confirmed overlaps the rest -
    used both to gate Hugin's own blend (roll check) and, via _read_image_geometry, to know the
    TRUE theoretical coverage a fully-successful stitch should achieve (width AND height),
    replacing a flat "N times one image's width" guess with the actual measured angular span."""
    pto = os.path.join(work_dir, "project.pto")
    _run([_hugin_tool("autooptimiser"), "-a", "-o", pto, pto], work_dir)
    roll_values = _read_roll_values(pto)
    if roll_values and (max(roll_values) - min(roll_values)) > _MAX_PLAUSIBLE_ROLL_RANGE_DEG:
        return None
    return _read_image_geometry(pto)


def _expected_coverage_px(geoms: list[_ImageGeometry], avg_width: float, avg_height: float) -> tuple[float, float]:
    """Theoretical (zero-overlap) pixel width/height a fully-successful stitch of these images
    would span, derived from their real optimized yaw/pitch/FOV - not a fixed multiplier."""
    yaw_min = min(g.yaw - g.hfov / 2 for g in geoms)
    yaw_max = max(g.yaw + g.hfov / 2 for g in geoms)
    pitch_min = min(g.pitch - g.vfov / 2 for g in geoms)
    pitch_max = max(g.pitch + g.vfov / 2 for g in geoms)
    avg_hfov = sum(g.hfov for g in geoms) / len(geoms)
    avg_vfov = sum(g.vfov for g in geoms) / len(geoms)
    expected_width = avg_width * (yaw_max - yaw_min) / avg_hfov if avg_hfov else avg_width
    expected_height = avg_height * (pitch_max - pitch_min) / avg_vfov if avg_vfov else avg_height
    return expected_width, expected_height


def _stitch_with_hugin_from_pto(work_dir: str) -> np.ndarray | None:
    """Runs the rest of the Hugin blend pipeline against the project.pto that _optimize_geometry
    already aligned (geometry, incl. the roll check, already done - avoids re-running autooptimiser
    -a a second time). Returns None (never raises) on any failure — this is the SECOND attempt
    after cv2.Stitcher, so a Hugin failure here just means "both approaches couldn't do it"."""
    try:
        pto = os.path.join(work_dir, "project.pto")
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
        # (see module docstring) — connectivity already confirmed all N photos genuinely overlap.
        # Judge its result against the REAL theoretical coverage (derived from autooptimiser's
        # own geometry below), not a flat guess — a flat "wider than one image" check only ever
        # caught missing WIDTH; a photo set spanning multiple pitch rows (ceiling/floor shots, not
        # just a horizontal ring) could join the ring fine but silently drop the pitched-up/down
        # shots, which a width-only check would never notice.
        avg_width = sum(img.shape[1] for img in images) / len(images)
        avg_height = sum(img.shape[0] for img in images) / len(images)
        geoms = _optimize_geometry(work_dir)

        looks_complete = ok and result is not None
        if looks_complete and geoms is not None:
            expected_w, expected_h = _expected_coverage_px(geoms, avg_width, avg_height)
            looks_complete = (
                result.shape[1] >= expected_w * _MIN_COVERAGE_FRACTION
                and result.shape[0] >= expected_h * _MIN_COVERAGE_FRACTION
            )
        # geoms is None means Hugin's own alignment looks unreliable (bad roll) - no independent
        # geometry to judge cv2.Stitcher against either, so its result (if any) is accepted as-is
        # rather than blocked on a check we have no trustworthy data to run.

        if not looks_complete:
            hugin_result = _stitch_with_hugin_from_pto(work_dir) if geoms is not None else None
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
