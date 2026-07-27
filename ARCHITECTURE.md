# BloggerBazar architecture

## Target topology

The client is a React + TypeScript Telegram Mini App. The only production backend is ASP.NET Core 9 under `src/`.

```
React Mini App → ASP.NET Core API → Application (CQRS) → Domain
                                      ↓
                         Infrastructure (EF Core, PostgreSQL, Redis, Telegram, Click)
```

## Layer rules

- `Domain` contains only business state and invariants. It has no framework dependencies.
- `Application` contains commands, queries, validation, use-case contracts and abstractions.
- `Infrastructure` implements persistence and integrations. It may depend on Application and Domain.
- `Api` owns HTTP, configuration, observability and dependency composition.

## Security boundaries

- Every write from the Mini App is bound to a Telegram user by validating the signed `Authorization: tma <initData>` value server-side.
- Contact data stays server-only and is never included in public profile DTOs.
- Click and Telegram secrets are supplied exclusively through environment variables or a secret manager.
- CORS is an explicit allow-list; production must configure `FRONTEND_URL`.

## Payment boundary

`PaymentOrder` stores the payable product, price snapshot and provider transaction state. `ContactUnlock` is a separate immutable access record and is created only after a matching order is confirmed as paid. This prevents a client from granting itself access by merely creating an order and keeps the model extensible for PRO subscriptions and promoted listings.

The Click adapter must validate the provider signature before it can send a confirmation command. The application layer then verifies the reference and exact amount before atomically recording payment and access.

## Migration strategy

EF Core is the canonical data access layer and the migrations under `src/BloggerBazar.Infrastructure/Persistence/Migrations` are the only database migration lineage. The React client uses the ASP.NET Core API directly.
