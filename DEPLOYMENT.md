# MiniSteam v2.9 deployment baseline

## Secrets

Do not commit JWT or SMTP secrets.

For development use User Secrets. For containers/production use environment variables:

```text
Jwt__Key
Mail__Password
ConnectionStrings__DefaultConnection
```

## Docker

Copy `.env.example` to `.env` and replace both placeholder secrets.

Generate the JWT key with PowerShell:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Then:

```powershell
docker compose up --build
```

The compose baseline runs:

```text
MiniSteam ASP.NET container
SQL Server 2022 container
persistent SQL volume
```

`Database__ApplyMigrationsOnStartup=true` is enabled in Docker Compose for this educational deployment workflow. The normal application default remains `false`.

## Health endpoints

```text
/health/live
/health/ready
/health
```

A deployment system should normally use `/health/live` for liveness and `/health/ready` for readiness.

## CI

`.github/workflows/ci.yml` runs:

```text
restore
build Release
test Release
```

on pushes and pull requests for `master` and `v2-development`.

## Production note

The Docker baseline is intentionally a learning/deployment foundation, not a claim that the project now has Steam-scale operations. A real production environment would still require TLS termination, backups, secret management, monitoring, resource limits, database maintenance, and an external deployment platform.
