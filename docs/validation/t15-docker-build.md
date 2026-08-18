# T15 — Docker image build and same-origin dashboard validation

> 2026-08-18 · Dockerfile optimization and `/monitor` dashboard navigation

## Scope

- Keep the single-container AppHost image contract: portal child process + in-process Aspire dashboard.
- Remove the duplicated Aspire staging directory from the final image.
- Cache target-architecture NuGet packages and skip the second restore during publish.
- Keep browser-facing Dashboard navigation on the portal's same-origin `/monitor` surface.

## Build validation

```bash
docker buildx build --builder orbstack --platform linux/arm64 --load \
  -t agent-context:review-optimized .
```

Results:

- Build succeeded for `linux/arm64`.
- First build after introducing the new NuGet cache: about 109 seconds (cold cache).
- Repeated cached build: about 4.4 seconds.
- Final image size: `558,693,244` bytes (about 559 MB), down from about 797 MB.
- `/app/aspire-tools` is absent from the final image.
- Aspire runtime cache remains available under `/root/.nuget/packages/` with both required RID packages and 168 files.

The Dockerfile stages the DCP and Dashboard RID packages outside `/app/publish`,
then copies only the runtime cache payload into the final image. Missing RID
packages fail the build instead of producing a broken AppHost image.

## Compose and dashboard validation

```bash
docker compose config --quiet
docker compose up -d --build
```

Results:

- Compose configuration is valid.
- `agent-context` and `agent-context-postgres` are healthy.
- `GET http://localhost:8080/api/health` returns HTTP 200 with database `ok`.
- `GET http://localhost:8080/monitor/` returns HTTP 200.
- `GET http://localhost:8080/monitor/metrics` returns HTTP 200.
- `GET http://localhost:8080/?view=Parameters` is served by the Aspire Dashboard query route, while plain `GET http://localhost:8080/` remains the portal.
- Dashboard HTML contains `<base href="/monitor/">` and `/navfix.js` on the prefixed surface.
- Compose injects `DASHBOARD_URL=http://localhost:8080/monitor`.
- Only portal port `8080` is published; Dashboard port `18888` remains container-internal.

The navigation fixer now matches on URL pathnames and preserves query strings
and fragments, so links such as metrics resource URLs remain under `/monitor`.
Resources' `Parameters`/`Graph` tabs use root-query URLs; YARP matches those
specific `view` queries and proxies them to Aspire without claiming the portal's
plain root path.
