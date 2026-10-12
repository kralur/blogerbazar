# BloggerBazar architecture

## Target topology

The client is a React + TypeScript Telegram Mini App. The only production backend is ASP.NET Core 9 under `src/`.

```
React Mini App → ASP.NET Core API → Application (CQRS) → Domain
                                      ↓
                         Infrastructure (EF Core, PostgreSQL, Redis, Telegram, R2)
```

## Layer rules

- `Domain` contains only business state and invariants. It has no framework dependencies.
- `Application` contains commands, queries, validation, use-case contracts and abstractions.
- `Infrastructure` implements persistence and integrations. It may depend on Application and Domain.
- `Api` owns HTTP, configuration, observability and dependency composition.

## Security boundaries

- Every write from the Mini App is bound to a Telegram user by validating the signed `Authorization: tma <initData>` value server-side.
- Contact data stays server-only and is never included in public profile DTOs.
- Telegram and storage secrets are supplied exclusively through environment variables or a secret manager.
- CORS is an explicit allow-list; production must configure `FRONTEND_URL`.

## Payment boundary

There is no payment flow today: the paid contact unlock was removed (2026-10-12). The `PaymentOrder`,
`ContactUnlock` and credit tables stay as history. A future Pro pass (D32) needs its own order model and a
provider adapter that validates the provider signature and the exact amount before granting access.

## Migration strategy

EF Core is the canonical data access layer and the migrations under `src/BloggerBazar.Infrastructure/Persistence/Migrations` are the only database migration lineage. The React client uses the ASP.NET Core API directly.
