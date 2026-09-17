# MusicLounge Panorama Stitcher

Small standalone HTTP service that stitches multiple overlapping phone photos (taken while
standing in one spot and rotating) into a single equirectangular panorama, using OpenCV's
`Stitcher` module.

Deliberately separate from the main .NET backend — see the docstring in `main.py` for why. The
backend calls this over plain HTTP (`IPanoramaStitchingService` / `HttpPanoramaStitchingService`),
the same shape it already uses for the Gemini/OpenAI integrations.

When Hugin's `cpfind` can't connect all photos (a known weak point on dim, low-texture lounge
interiors), the service tries an AI-based rescue with LoFTR (`kornia`, CPU-only `torch`) on just
the disconnected pairs before giving up — see `_rescue_disconnected_with_loftr` in `main.py`. This
adds a real dependency footprint: the `torch`+`kornia` CPU wheels plus the baked-in LoFTR
checkpoint add roughly several hundred MB to ~1GB to the built image. The checkpoint is downloaded
at **Docker build time** (see the Dockerfile), not at runtime, so the running container needs no
outbound network access for it and pays no cold-start penalty.

## API

- `GET /health` → `{"status": "ok"}`
- `POST /stitch` — requires header `X-Stitcher-Key` (see **Security** below). Body
  `{"image_urls": ["https://...", "https://..."]}` (2+ URLs, images taken
  from the same vantage point with overlap between consecutive shots). Returns the stitched
  panorama as `image/jpeg` bytes on success, or a JSON `{"detail": "..."}` error (400/422/500)
  with a specific, actionable reason on failure.

## Security (MLACP-431)

The service is reachable over HTTP and downloads the image URLs it is given, so both checks below are
**closed when not configured** — a deployment missing either variable rejects every `/stitch` call
(503) instead of running wide open.

| Variable | Meaning |
|---|---|
| `STITCHER_API_KEY` | Shared secret. `/stitch` requires header `X-Stitcher-Key` with this exact value (constant-time compare). The backend sends it from `PanoramaStitcher:ApiKey`. |
| `ALLOWED_IMAGE_ORIGINS` | Comma-separated origins (`scheme://host[:port]`) images may be downloaded from — normally just the backend's public URL, e.g. `https://musiclounge-api.azurewebsites.net`. Compared as parsed origins, so look-alike hosts (`…net.evil.com`), `user@host` tricks, wrong scheme or wrong port are all rejected. |

Redirects are never followed: an allowed host could otherwise bounce the download to an internal
address. `/health` stays open for platform health probes and for the backend's warm-up call.

## Run locally

```bash
pip install -r requirements.txt
export STITCHER_API_KEY=dev-only-key
export ALLOWED_IMAGE_ORIGINS=http://localhost:5299
uvicorn main:app --reload --port 8000
```

Set the same key as `PanoramaStitcher:ApiKey` in the backend's `appsettings.Development.Local.json`.

## Tests

```bash
pip install -r requirements-dev.txt
pytest
```

`test_security.py` covers authentication and the image-origin allowlist. `test_main.py` covers the LoFTR rescue's control flow (`_append_control_points`, `_rescue_disconnected_with_loftr`,
`_check_connectivity`'s wiring to it) with `_loftr_match_pair` mocked — no `torch`/`kornia`/Hugin
binaries needed to run these. Everything else in `main.py` shells out to real Hugin binaries or
`cv2`, which isn't covered by automated tests; see the "Verification" note in the LoFTR rescue's
design plan for what full end-to-end coverage would need.

## Run via Docker

```bash
docker build -t musiclounge-panorama-stitcher .
docker run -p 8000:8000 -e STITCHER_API_KEY=dev-only-key   -e ALLOWED_IMAGE_ORIGINS=http://host.docker.internal:5299 musiclounge-panorama-stitcher
```

## Deploying

This needs to be hosted as its own reachable HTTP service (a small container instance is enough —
it's stateless and only runs briefly per request). Point the main backend at it via
`PanoramaStitcher:BaseUrl` in `appsettings.json` / `appsettings.Development.Local.json`. It is
**not** started by the main backend's `dotnet run` — start it separately (see above) for local
development, or deploy it alongside the backend in production.
