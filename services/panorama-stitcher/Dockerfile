FROM python:3.12-slim

WORKDIR /app

# libgl1/libglib2.0-0 — OpenCV's Linux wheels dynamically link against these even in the
# "headless" build; missing them fails at import time with a cryptic libGL.so.1 error, not at
# pip-install time, so this is easy to miss until the container actually starts.
# hugin-tools/enblend — the CLI fallback pipeline used when cv2.Stitcher can't handle a photo set
# (see _stitch_with_hugin in main.py). Only the CLI tools package is needed, not the `hugin` GUI.
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1 libglib2.0-0 hugin-tools enblend \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Pre-downloads kornia's indoor-pretrained LoFTR checkpoint into torch's weight cache at BUILD
# time, not first-request time - the running container needs no runtime network access for it and
# it doesn't add multi-second cold-start latency to the connectivity-rescue path on every restart.
# Placed right after the pip install (before COPY main.py) so Docker's layer cache keeps this
# expensive layer intact across ordinary main.py-only rebuilds. Also doubles as a build-time smoke
# test: a broken torch/kornia CPU install fails the build here, loudly, instead of failing
# silently in production. If this fails with a libgomp.so.1 error (a known occasional issue for
# torch CPU wheels on slim Debian images), add libgomp1 to the apt-get install line above.
RUN python -c "import kornia.feature as KF; KF.LoFTR(pretrained='indoor')"

COPY main.py .

EXPOSE 8000
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
