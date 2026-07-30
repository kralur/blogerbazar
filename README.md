# BloggerBazar

Telegram Mini App marketplace for businesses and bloggers in Uzbekistan.

## Production Architecture

- **Frontend:** React, TypeScript, Vite, Tailwind CSS, Telegram Mini Apps SDK.
- **Backend:** ASP.NET Core 9 with Clean Architecture, CQRS/MediatR and FluentValidation.
- **Data:** PostgreSQL through Entity Framework Core; Redis for distributed caching.
- **Runtime:** Docker Compose, Nginx and a single ASP.NET Core API.

The repository has no Node.js backend or Prisma dependency. EF Core migrations in `src/BloggerBazar.Infrastructure/Persistence/Migrations` are the canonical database history.

## Capabilities

- Blogger and business profiles, portfolio and social-platform links.
- Blogger search, campaign publishing, campaign applications and direct collaboration requests.
- Deals, post-deal reviews, profile moderation and administration.
- Public profile contacts, plus legacy contact-unlock and wallet APIs retained only for existing clients.
- Server-side Telegram `initData` validation, webhook authentication and rate limiting.

## Run with Docker Compose

1. Copy `.env.example` to `.env` and configure the required values.
2. Set strong `POSTGRES_PASSWORD`, `TELEGRAM_BOT_TOKEN`, `TELEGRAM_WEBHOOK_SECRET` and an administrator Telegram ID.
3. Start the stack:

```powershell
docker compose up --build
```

The Mini App is available at `http://localhost:8081`. The API is proxied through the same origin at `/api`; the health endpoint is `/health`.

PostgreSQL and Redis bind to localhost only. Production traffic must terminate at a TLS reverse proxy in front of the frontend service.

## Local Development

Start PostgreSQL and Redis through Compose, then run:

```powershell
dotnet tool restore
dotnet run --project src/BloggerBazar.Api
npm install
npm --workspace frontend run dev
```

Vite proxies `/api` and `/health` to `VITE_DEV_API_URL` or `http://localhost:8080`.

## Database Migrations

For a single API instance, Docker Compose applies migrations at startup. For multi-replica deployments, disable `Database__ApplyMigrationsOnStartup` and apply migrations as a dedicated release step:

```powershell
dotnet tool restore
dotnet ef database update --project src/BloggerBazar.Infrastructure --startup-project src/BloggerBazar.Api
```

## Telegram Bot Webhook

Telegram delivers bot commands and payment-related updates to the single endpoint `/api/webhooks/telegram`. Configure it with the same `TELEGRAM_WEBHOOK_SECRET` value:

```powershell
curl.exe -X POST "https://api.telegram.org/bot$env:TELEGRAM_BOT_TOKEN/setWebhook" `
  -d "url=https://your-api.example.com/api/webhooks/telegram" `
  -d "secret_token=$env:TELEGRAM_WEBHOOK_SECRET" `
  -d "allowed_updates=[\"pre_checkout_query\",\"message\"]"
```

## Deprecated Contact-Unlock APIs

Contacts are public in BloggerBazar v1. New clients must read them through `GET /api/contacts/{targetType}/{targetId}` and must not use the contact-unlock payment flow.

The following endpoints remain available only for backward compatibility and are marked **Deprecated** in Swagger/OpenAPI:

- `POST /api/payments/contact-unlocks`
- `POST /api/payments/contact-unlocks/{reference}/invoice`
- `GET /api/payments/contact-unlocks/{targetType}/{targetId}`
- `POST /api/wallet/contact-unlocks`

They are not used anywhere in the current React user interface. Existing integrations continue to receive the same routes and responses; no new integration should depend on them.

## Validation

```powershell
dotnet build BloggerBazar.sln --no-restore
dotnet test BloggerBazar.sln --no-restore
npm run build
```
