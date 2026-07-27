# BloggerBazar — First Run Checklist

## 1. Required software

- Windows PowerShell 7 or Windows PowerShell.
- .NET SDK 9.
- Node.js 22 LTS and npm.
- PostgreSQL running on `localhost:5432`.
- Redis is optional. When it is absent, BloggerBazar uses in-memory distributed caching.

## 2. Local configuration

From the repository root, create the ignored local configuration file:

```powershell
Copy-Item .env.example .env
```

Set `ConnectionStrings__Postgres` in `.env` to the existing local `bloghub` database. For the supplied local PostgreSQL setup this is:

```text
ConnectionStrings__Postgres=Host=localhost;Port=5432;Database=bloghub;Username=postgres;Password=postgres
```

`ConnectionStrings__Postgres` is the only database connection string consumed by the API. Do not commit `.env`.

## 3. Redis configuration

For local Redis, leave this setting enabled:

```text
ConnectionStrings__Redis=localhost:6379
```

If Redis is not running, remove or leave `ConnectionStrings__Redis` empty. The API starts with the distributed-memory-cache fallback.

## 4. Telegram configuration

Set these values in `.env`; they are read at startup and are never stored in source code:

```text
TELEGRAM_BOT_TOKEN=your-bot-token
TELEGRAM_BOT_USERNAME=your_bot_username
TELEGRAM_MINIAPP_URL=https://your-public-mini-app-url
TELEGRAM_WEBHOOK_SECRET=your-random-webhook-secret
```

Telegram requires a public HTTPS URL for both the Mini App and webhook. Use a tunnel during local development, then configure the Mini App URL in BotFather. The API validates `initData` with the configured bot token; browser-only requests without Telegram `initData` cannot call protected endpoints.

After the public API URL is available, configure the webhook:

```powershell
curl.exe -X POST "https://api.telegram.org/bot$env:TELEGRAM_BOT_TOKEN/setWebhook" `
  -d "url=https://your-public-api.example.com/api/webhooks/telegram" `
  -d "secret_token=$env:TELEGRAM_WEBHOOK_SECRET"
```

## 5. Click configuration

Set the Telegram payment provider token in `.env`:

```text
CLICK__TELEGRAMPROVIDERTOKEN=your-click-telegram-provider-token
```

Leave it empty until Click is configured. Contact unlock invoice creation then reports a configuration error instead of charging users. The existing `PAYMENTS__CONTACTUNLOCKAMOUNTUZS` setting controls the local demo price.

## 6. Restore and migrate the database

Run these commands from the repository root:

```powershell
dotnet tool restore
dotnet restore
dotnet tool run dotnet-ef database update --project src/BloggerBazar.Infrastructure --startup-project src/BloggerBazar.Api
```

The design-time DbContext loads `.env`, so the migration command targets `bloghub`. Existing EF Core migrations are canonical and must not be recreated.

## 7. Start the backend

```powershell
dotnet run --project src/BloggerBazar.Api
```

Development startup applies pending migrations and inserts deterministic demo data only when the database contains no blogger profiles. It creates 20 bloggers, 10 businesses, 15 campaigns, completed deals, reviews, ratings, portfolios and social-platform records.

Open these endpoints after startup:

- `http://localhost:8080/health`
- `http://localhost:8080/swagger`
- `http://localhost:8080/openapi/v1.json`

## 8. Start the frontend

In a second terminal:

```powershell
npm install
npm run dev
```

Open `http://localhost:5173`. Vite proxies `/api` and `/health` to `http://localhost:8080` by default. To use another backend URL, set `VITE_DEV_API_URL` before starting Vite.

## 9. Optional Docker Compose stack

`docker compose up --build` starts an isolated PostgreSQL and Redis stack. For the existing local `bloghub` database, use the direct backend startup above instead of starting the Compose PostgreSQL service.

## 10. Troubleshooting

- **Database connection fails:** confirm PostgreSQL is running, the `bloghub` database exists, and the `ConnectionStrings__Postgres` value in `.env` is correct.
- **Redis health check fails:** start Redis or clear `ConnectionStrings__Redis` to use the memory fallback.
- **Telegram authorization fails:** open the Mini App through Telegram, verify the bot token, and ensure the public HTTPS URL is configured in BotFather.
- **Click invoice creation fails:** set a valid Telegram provider token issued for Click; do not use placeholder values in production.
- **Port is in use:** stop the conflicting process or change `applicationUrl` in `src/BloggerBazar.Api/Properties/launchSettings.json` and `VITE_DEV_API_URL` together.

## 11. Verification checklist

- [ ] `dotnet build` completes without warnings or errors.
- [ ] `dotnet test` passes.
- [ ] `npm run build` succeeds.
- [ ] `GET /health` returns HTTP 200 with PostgreSQL and cache checks healthy.
- [ ] Swagger UI opens at `/swagger` in Development.
- [ ] Development seed data appears in the catalog.
- [ ] Telegram Mini App opens through the configured public HTTPS URL.
- [ ] Click invoice creation is tested only with a configured provider token.
