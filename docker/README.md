# Docker Compose Orchestration

Runs the **LicenseServer + PostgreSQL** locally with one command. No .NET SDK
required on the host — the Dockerfile builds the app inside a container.

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

# Validate a seeded demo key
curl -X POST http://localhost:8080/api/license/validate \
  -H "Content-Type: application/json" \
  -d '{"apiKey":"demo-pro-1111111111111111111111"}'

# Admin: list all keys (use the ADMIN_KEY from your .env)
curl http://localhost:8080/api/admin/keys -H "X-Admin-Key: <ADMIN_KEY>"
```

## Layout

| File | Purpose |
|------|---------|
| `docker-compose.yml` (repo root) | App orchestration; `include:`s the generic Postgres |
| `docker/compose.postgres.yml` | **Generic, reusable** Postgres service (volume + healthcheck + initdb) |
| `docker/postgres/init/01-schema.sql` | Schema, generated with `dotnet ef migrations script --idempotent` |
| `docker/postgres/init/02-seed.sql` | Demo license keys (pro / free / expired / revoked) |
| `.env.example` | Template for the required environment variables |

## How seeding works

Postgres runs everything in `/docker-entrypoint-initdb.d/` (alphabetical
order) **only when the data volume is empty** — i.e. on first start:

1. `01-schema.sql` creates the tables and marks the EF migration as applied
   in `__EFMigrationsHistory`. It is idempotent (guarded per migration).
2. `02-seed.sql` inserts the demo keys with `ON CONFLICT DO NOTHING`.

When the app starts, its `Database.Migrate()` sees the migration already
applied and does nothing. If you ever wipe the init scripts, `Migrate()`
still creates the schema by itself — seeding is a convenience, not a
dependency.

> **After adding a new EF migration**, regenerate the schema script:
> `dotnet ef migrations script --idempotent --project src/ClaudeMcpServer.LicenseServer --output docker/postgres/init/01-schema.sql`

## Seeded demo keys

| Key | Plan | State |
|-----|------|-------|
| `demo-pro-1111111111111111111111` | Pro | active, never expires |
| `demo-free-222222222222222222222` | Free | active, expires in 30 days |
| `demo-expired-3333333333333333333` | Free | expired (validation fails) |
| `demo-revoked-4444444444444444444` | Pro | revoked (validation fails) |

## Day-2 operations

```bash
docker compose down            # stop; DATA IS KEPT (named volume)
docker compose down -v         # stop AND delete the database volume
docker compose up --build      # rebuild after code changes
docker compose logs -f license-server
docker compose exec postgres psql -U licenses -d licenses   # SQL console
```
