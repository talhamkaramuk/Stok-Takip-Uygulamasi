# Container Security

This document records the container hardening contract for STOKIO.

## Runtime User

- The API runtime image runs with a non-root numeric UID.
- The published application files remain read-only at runtime.
- The writable export directory is isolated at `/var/lib/stokio/export-files`.

## Filesystem

- Development Docker Compose runs the API service with `read_only: true`.
- `/tmp` is mounted as tmpfs for framework/runtime temporary files.
- Export files are written to the `api_export_files` volume.

## Linux Privileges

- API container capabilities are dropped with `cap_drop: ALL`.
- `no-new-privileges:true` is enabled for the API service.

## Healthcheck

- The Docker image defines a container healthcheck.
- The healthcheck executes `dotnet STOKIO.Api.dll --container-healthcheck`.
- The healthcheck URL defaults to `http://127.0.0.1:8080/health/ready`.
- Override the target with `STOKIO_HEALTHCHECK_URL` when needed.

## Secrets

- Development defaults in `docker-compose.yml` are not production secrets.
- Production values for `POSTGRES_PASSWORD`, `STOKIO_CONNECTION_STRING`, `STOKIO_JWT_SIGNING_KEY`, and similar settings must come from a CI secret store, orchestrator secret, or cloud secret manager.
- Production deployments must set `Database__EnsureCreated=false`, `Database__ApplyDevelopmentSchemaPatches=false`, and `Database__SeedDevelopmentData=false`.

## CI Vulnerability Scan

- GitHub Actions builds the API Docker image as `stokio-api:ci`.
- Trivy scans OS and library vulnerabilities.
- The pipeline fails on fixable `HIGH` and `CRITICAL` findings.
