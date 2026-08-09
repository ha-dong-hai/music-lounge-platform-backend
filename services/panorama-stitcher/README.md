# MusicLounge Panorama Stitcher

Small standalone HTTP service that stitches multiple overlapping phone photos (taken while
standing in one spot and rotating) into a single equirectangular panorama, using OpenCV's
`Stitcher` module.

Deliberately separate from the main .NET backend — see the docstring in `main.py` for why. The
backend calls this over plain HTTP (`IPanoramaStitchingService` / `HttpPanoramaStitchingService`),
the same shape it already uses for the Gemini/OpenAI integrations.

## API

- `GET /health` → `{"status": "ok"}`
- `POST /stitch` — body `{"image_urls": ["https://...", "https://..."]}` (2+ URLs, images taken
  from the same vantage point with overlap between consecutive shots). Returns the stitched
  panorama as `image/jpeg` bytes on success, or a JSON `{"detail": "..."}` error (400/422/500)
  with a specific, actionable reason on failure.

## Run locally

```bash
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

## Run via Docker

```bash
docker build -t musiclounge-panorama-stitcher .
docker run -p 8000:8000 musiclounge-panorama-stitcher
```

## Deploying

This needs to be hosted as its own reachable HTTP service (a small container instance is enough —
it's stateless and only runs briefly per request). Point the main backend at it via
`PanoramaStitcher:BaseUrl` in `appsettings.json` / `appsettings.Development.Local.json`. It is
**not** started by the main backend's `dotnet run` — start it separately (see above) for local
development, or deploy it alongside the backend in production.
