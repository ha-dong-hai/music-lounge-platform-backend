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

COPY main.py .

EXPOSE 8000
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
