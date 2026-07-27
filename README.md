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
- Hidden contacts, Telegram invoice payments and credit-based legacy wallet compatibility.
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

## Telegram Payments and Bot Webhook

`CLICK__TELEGRAMPROVIDERTOKEN` is read only from local configuration and is never committed. A contact unlock is granted only after the authenticated Telegram webhook processes a successful payment.

Telegram delivers bot commands, pre-checkout queries and successful payments to the single endpoint `/api/webhooks/telegram`. Configure it with the same `TELEGRAM_WEBHOOK_SECRET` value:

```powershell
curl.exe -X POST "https://api.telegram.org/bot$env:TELEGRAM_BOT_TOKEN/setWebhook" `
  -d "url=https://your-api.example.com/api/webhooks/telegram" `
  -d "secret_token=$env:TELEGRAM_WEBHOOK_SECRET" `
  -d "allowed_updates=[\"pre_checkout_query\",\"message\"]"
```

Confirm the payment method with Telegram before release: contact unlocks are digital access and may require Telegram Stars (`XTR`) rather than a third-party provider.

## Validation

```powershell
dotnet build BloggerBazar.sln --no-restore
dotnet test BloggerBazar.sln --no-restore
npm run build
```
