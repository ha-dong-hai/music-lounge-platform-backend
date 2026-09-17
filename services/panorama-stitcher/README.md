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

`test_security.py` covers authentication and the image-origin allowlist. `test_coverage.py` covers the full-turn check (a set that doesn't go all the way round is rejected with 422 before OpenCV runs), using yaw/HFOV values measured with real Hugin on a capture set with known geometry. `test_main.py` covers the LoFTR rescue's control flow (`_append_control_points`, `_rescue_disconnected_with_loftr`,
`_check_connectivity`'s wiring to it) with `_loftr_match_pair` mocked — no `torch`/`kornia`/Hugin
binaries needed to run these. Everything else in `main.py` shells out to real Hugin binaries or
`cv2`, which isn't covered by automated tests; see the "Verification" note in the LoFTR rescue's
design plan for what full end-to-end coverage would need.

## Run via Docker

```bash
docker build -t musiclounge-panorama-stitcher .
docker run -p 8000:8000 \
  -e STITCHER_API_KEY=dev-only-key \
  -e ALLOWED_IMAGE_ORIGINS=http://host.docker.internal:5299 \
  musiclounge-panorama-stitcher
```

## Deploying

This is **not** started by the main backend's `dotnet run` — run it separately (see above) for local
development. In production it is its own HTTP service: the backend's App Service plan (B1) is too
small for Hugin + PyTorch CPU + LoFTR, so it runs on **Azure Container Apps (Consumption)**, scaled
0–1. Scaled to zero it costs nothing; a month of occasional stitching fits inside the monthly free
grant (180,000 vCPU-seconds / 360,000 GiB-seconds per subscription).

### Image

`.github/workflows/panorama-stitcher.yml` runs the tests and, on every push to `master` that touches
this folder, publishes `ghcr.io/ha-dong-hai/musiclounge-panorama-stitcher` tagged `sha-<commit>` and
`latest`. Container Apps pulls it without registry credentials, so the package must be publicly
readable. This one was pullable anonymously right after its first publish (checked 2026-09-17). Check
it again if you publish under a different owner or repository, because a private package makes the
container app fail to start with an image-pull error:

```bash
TOKEN=$(curl -s "https://ghcr.io/token?scope=repository:ha-dong-hai/musiclounge-panorama-stitcher:pull" | jq -r .token)
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/vnd.oci.image.index.v1+json" \
  https://ghcr.io/v2/ha-dong-hai/musiclounge-panorama-stitcher/manifests/latest   # 200 = public
```

If it is not public: open the package on GitHub → *Package settings* → *Change visibility* → Public.

The LoFTR checkpoint is verified at build time with `ADD --checksum` (see the Dockerfile).

### One-time setup (Azure CLI)

```bash
RG=<resource-group>                     # same resource group as the backend
KEY=$(openssl rand -hex 32)             # shared secret; never commit it

az provider register --namespace Microsoft.App --wait

# No Log Analytics workspace: it bills per GB ingested, and this service logs little worth keeping.
az containerapp env create -n musiclounge-aca-env -g $RG -l eastasia --logs-destination none

az containerapp create -n musiclounge-stitcher -g $RG --environment musiclounge-aca-env \
  --image ghcr.io/ha-dong-hai/musiclounge-panorama-stitcher:latest \
  --ingress external --target-port 8000 \
  --min-replicas 0 --max-replicas 1 --cpu 2 --memory 4Gi \
  --secrets stitcher-key=$KEY \
  --env-vars STITCHER_API_KEY=secretref:stitcher-key \
             ALLOWED_IMAGE_ORIGINS=https://musiclounge-api.azurewebsites.net
```

Then point the backend at it (App Service → Environment variables):

| Setting | Value |
|---|---|
| `PanoramaStitcher__BaseUrl` | `https://<app FQDN from the create output>` |
| `PanoramaStitcher__ApiKey` | the same `$KEY` |
| `PanoramaStitcher__PublicBaseUrl` | `https://musiclounge-api.azurewebsites.net` (needed while uploads are stored on local disk) |

### Updating

Container Apps does not re-pull a tag that did not change name, so deploy the commit tag:

```bash
az containerapp update -n musiclounge-stitcher -g $RG \
  --image ghcr.io/ha-dong-hai/musiclounge-panorama-stitcher:sha-<short commit>
```

### Cold start

With zero replicas the first request waits for the container to start. Container Apps holds the
request meanwhile, but ingress times out any single request after **240 seconds** — so the backend
calls `GET /health` first to wake the service (up to 3 minutes, retrying), and only then sends
`/stitch`, instead of spending that budget on startup and stitching combined.
