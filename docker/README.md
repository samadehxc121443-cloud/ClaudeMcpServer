# Docker Compose Orchestration

Runs the **LicenseServer + PostgreSQL + Redis + Vault** locally with one
command. No .NET SDK required on the host — the Dockerfile builds the app
inside a container.

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

# Admin: list all keys (use the ADMIN_KEY from your .env)
curl http://localhost:8080/api/admin/keys -H "X-Admin-Key: <ADMIN_KEY>"
```

## Layout

| File | Purpose |
|------|---------|
| `docker-compose.yml` (repo root) | App orchestration; `include:`s the generic services below |
| `docker/compose.postgres.yml` | **Generic** Postgres (persistent volume + healthcheck + initdb) |
| `docker/compose.redis.yml` | **Generic** Redis (cache — deliberately no volume) |
| `docker/compose.vault.yml` | **Generic** Vault in dev mode (secrets store) |
| `docker/postgres/init/01-schema.sql` | Schema, generated with `dotnet ef migrations script --idempotent` |
| `docker/postgres/init/02-seed.sql` | Demo license keys (pro / free / expired / revoked) |
| `.env.example` | Template for the required environment variables |

The generic files contain nothing app-specific: any other orchestration can
`include:` them as-is. App-specific glue (like what secrets go into Vault)
stays in the root `docker-compose.yml`.

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

## Seeded demo keys

| Key | Plan | State |
|-----|------|-------|
| `demo-pro-1111111111111111111111` | Pro | active, never expires |
| `demo-free-222222222222222222222` | Free | active, expires in 30 days |
| `demo-expired-3333333333333333333` | Free | expired (validation fails) |
| `demo-revoked-4444444444444444444` | Pro | revoked (validation fails) |

## Day-2 operations

```bash
docker compose down            # stop; DB DATA IS KEPT (named volume)
docker compose down -v         # stop AND delete the database volume
docker compose up --build      # rebuild after code changes
docker compose logs -f license-server
docker compose exec postgres psql -U licenses -d licenses    # SQL console
docker compose exec redis redis-cli                          # Redis console
docker compose exec vault vault kv get -address=http://127.0.0.1:8200 secret/license-server
```
