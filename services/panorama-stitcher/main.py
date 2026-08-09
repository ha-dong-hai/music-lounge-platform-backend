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
"""

import cv2
import numpy as np
import requests
from fastapi import FastAPI, HTTPException
from fastapi.responses import Response
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


class StitchRequest(BaseModel):
    image_urls: list[str]


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/stitch")
def stitch(req: StitchRequest):
    if len(req.image_urls) < 2:
        raise HTTPException(400, "Cần ít nhất 2 ảnh để ghép panorama.")

    images = []
    for url in req.image_urls:
        try:
            resp = requests.get(url, timeout=20)
            resp.raise_for_status()
        except Exception as e:
            raise HTTPException(400, f"Không tải được ảnh {url}: {e}")

        arr = np.frombuffer(resp.content, dtype=np.uint8)
        img = cv2.imdecode(arr, cv2.IMREAD_COLOR)
        if img is None:
            raise HTTPException(400, f"Không đọc được ảnh {url} — file có thể bị hỏng hoặc sai định dạng.")
        images.append(img)

    stitcher = cv2.Stitcher_create(cv2.Stitcher_PANORAMA)
    status, result = stitcher.stitch(images)

    if status != cv2.Stitcher_OK:
        reason = STITCH_ERROR_REASONS.get(status, f"Ghép ảnh thất bại (mã lỗi OpenCV: {status}).")
        raise HTTPException(422, reason)

    ok, buf = cv2.imencode(".jpg", result, [cv2.IMWRITE_JPEG_QUALITY, 90])
    if not ok:
        raise HTTPException(500, "Ghép ảnh thành công nhưng không mã hóa được ảnh kết quả.")

    return Response(content=buf.tobytes(), media_type="image/jpeg")
