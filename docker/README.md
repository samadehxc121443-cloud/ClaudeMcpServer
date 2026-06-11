# Docker Compose Orchestration

Runs the **LicenseServer + PostgreSQL + Redis + Vault + Keycloak** locally
with one command. No .NET SDK required on the host — the Dockerfile builds
the app inside a container.

## Requirements

- Docker Desktop (Windows/macOS) or Docker Engine + Compose v2.20+ (Linux).

## Quick start

```bash
cp .env.example .env       # then edit the secrets in .env
docker compose up --build
```

The API is now at `http://localhost:8080`.

```bash
# Health (also confirms the DB connection and the seed)
curl http://localhost:8080/health

# Validate a seeded demo key (run it twice — the second hit comes from Redis)
curl -X POST http://localhost:8080/api/license/validate \
  -H "Content-Type: application/json" \
  -d '{"apiKey":"demo-pro-1111111111111111111111"}'

# Admin: list all keys. Admin keys live in the DATABASE, not in .env —
# this one is seeded by docker/postgres/init/02-seed.sql:
curl http://localhost:8080/api/admin/keys -H "X-Admin-Key: demo-admin-99999999999999999999"

# Active plans (public — limits are data in the Plans table)
curl http://localhost:8080/api/plans

# Report usage: one round-trip returns used/limit/percent/allowed, so the
# MCP server can warn at 90% or block at the limit without a second call.
# The demo-free key is on the Free plan (100 emails/day).
curl -X POST http://localhost:8080/api/usage/report \
  -H "Content-Type: application/json" \
  -d '{"apiKey":"demo-free-222222222222222222222","operation":"email"}'
```

## Environments

`ASPNETCORE_ENVIRONMENT` is controlled from `.env` (default: `Development`).
`/health` reports which one is active. Development validates the DI graph on
startup and returns detailed errors; Production resolves lazily and keeps
error responses terse.

## Admin keys

Admin keys are **data, not configuration**: they live in the `AdminKeys`
table. Locally one is seeded (`demo-admin-...`); on a fresh database with no
seed (e.g. Railway), the app generates a bootstrap admin key on first start
and logs it once — grab it from the logs and store it securely.

## Layout

| File | Purpose |
|------|---------|
| `docker-compose.yml` (repo root) | App orchestration; `include:`s the generic infra compose |
| `docker/compose.infra.yml` | **All generic infra in one file**: Postgres, Redis, Vault, Keycloak |
| `docker/keycloak/import/realm-license-server.json` | App realm: roles, portal client, demo users |
| `docker/postgres/init/01-schema.sql` | Schema, generated with `dotnet ef migrations script --idempotent` |
| `docker/postgres/init/02-seed.sql` | Demo license keys (pro / free / expired / revoked) |
| `.env.example` | Template for the required environment variables |

`compose.infra.yml` contains nothing app-specific: any other orchestration
can `include:` it as one piece. App-specific glue (what secrets go into
Vault, realm content, SQL seeds) stays in the root `docker-compose.yml` and
the `docker/` data folders.

## How each service is used

**Postgres** — the app's database. Schema and demo data are seeded via
`/docker-entrypoint-initdb.d/` (alphabetical order, only when the data
volume is empty):

1. `01-schema.sql` creates the tables and marks the EF migration as applied
   in `__EFMigrationsHistory`. Idempotent (guarded per migration).
2. `02-seed.sql` inserts the demo keys with `ON CONFLICT DO NOTHING`.

When the app starts, its `Database.Migrate()` sees the migration already
applied and does nothing — if the init scripts are ever removed, `Migrate()`
still creates the schema by itself.

> **After adding a new EF migration**, regenerate the schema script:
> `dotnet ef migrations script --idempotent --project src/ClaudeMcpServer.LicenseServer --output docker/postgres/init/01-schema.sql`

**Redis** — backs `CachingLicenseManagerService`, a second decorator stacked
on the logging one (`Logging → Caching → LicenseManagerService`). License
validations are cached for 60 seconds, so repeated lookups skip Postgres.
The stack only forms when `REDIS_CONNECTION` is set; without it (e.g. on
Railway) the app runs with logging only.

**Vault** — holds the app's secrets. On every `up`, the one-shot
`vault-init` job writes `AdminKey` and the DB connection string into
Vault's KV store; the app fetches them at startup via `VAULT_ADDR` +
`VAULT_TOKEN`. The `license-server` container gets **no secrets through
environment variables**. Dev mode is in-memory — `vault-init` reseeds on
each start. Without `VAULT_ADDR` (e.g. on Railway) the app falls back to
regular configuration.

**Keycloak** — identity provider for humans (the portal, and later granular
roles). The `license-server` realm is imported on startup with two realm
roles (`license-admin`, `license-client`), a public `portal` client, and two
demo users. Admin endpoints accept **either** a Keycloak bearer token with
the `license-admin` role **or** the `X-Admin-Key` header (machine-to-machine,
validated against the database).

```bash
# Get a token as the demo admin (Keycloak admin console: http://localhost:8085)
TOKEN=$(curl -s -X POST http://localhost:8085/realms/license-server/protocol/openid-connect/token \
  -d "grant_type=password" -d "client_id=portal" \
  -d "username=demo-admin" -d "password=demo-admin-password" | jq -r .access_token)

# Use it on an admin endpoint
curl http://localhost:8080/api/admin/keys -H "Authorization: Bearer $TOKEN"
```

## Seeded demo keys

| Key | Plan | State |
|-----|------|-------|
| `demo-pro-1111111111111111111111` | Pro | active, never expires |
| `demo-free-222222222222222222222` | Free | active, expires in 30 days |
| `demo-expired-3333333333333333333` | Free | expired (validation fails) |
| `demo-revoked-4444444444444444444` | Pro | revoked (validation fails) |

## Day-2 operations

Postgres, Redis and Vault are **not exposed to the host** — they live on the
compose-internal network only. Use `exec` for consoles:

```bash
docker compose down            # stop; DB DATA IS KEPT (named volume)
docker compose down -v         # stop AND delete the database volume
docker compose up --build      # rebuild after code changes
docker compose logs -f license-server
docker compose exec postgres psql -U licenses -d licenses    # SQL console
docker compose exec redis redis-cli -a "$REDIS_PASSWORD"     # Redis console
docker compose exec -e VAULT_TOKEN="$VAULT_TOKEN" vault \
  vault kv get -address=http://127.0.0.1:8200 secret/license-server
```
