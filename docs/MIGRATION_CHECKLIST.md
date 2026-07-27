# Node.js to ASP.NET Core Migration Checklist

Status date: 2026-07-27. The ASP.NET Core 9 API is the only production backend.

| Node module | ASP.NET Core equivalent | Status |
| --- | --- | --- |
| Blogger profiles, search and contacts | `BloggersController`, `ContactsController`, CQRS blogger features | ✅ Already migrated |
| Portfolio and social platforms | EF Core `PortfolioItem` and `SocialPlatform`, profile contracts | ✅ Already migrated |
| Business profiles | `BusinessesController`, CQRS business features | ✅ Already migrated |
| Campaigns | `CampaignsController`, CQRS campaign features | ✅ Already migrated |
| Campaign applications | `CampaignApplicationsController`, review/accept/status features | ✅ Already migrated |
| Direct collaboration requests | `CollaborationRequestsController`, `CollaborationRequest` entity and handlers | ✅ Migrated in this change |
| Deals | `DealsController`, including deals from campaigns and direct requests | ✅ Migrated in this change |
| Reviews | `DealsController` and review features | ✅ Already migrated |
| Credit wallet and ledger | `WalletController`, `CreditAccount`, `CreditLedgerEntry` | ✅ Migrated in this change |
| Contact unlock payments | `PaymentsController`, Telegram invoice webhook and `PaymentOrder` | ✅ Already migrated; supersedes the Node credit-only path |
| Admin moderation | Admin blogger, marketplace, campaign and credit endpoints | ✅ Migrated in this change |
| Telegram bot `/start` | Protected `TelegramBotWebhookController` and Bot API client | ✅ Migrated in this change |
| Notifications | No standalone notification module existed in Node; Telegram payment and bot webhook flows are handled by ASP.NET Core | ✅ No legacy module to migrate |
| Prisma schema | EF Core entities, `BloggerBazarDbContext`, canonical migrations | ✅ Migrated in this change |

## Removal gates

- [x] ASP.NET Core covers every legacy Node module.
- [x] React calls only ASP.NET Core API paths.
- [x] Docker Compose starts only the ASP.NET Core API.
- [x] .NET tests have no Node dependency.
- [x] No deployment automation references Node.
- [x] EF Core has no pending model changes.

The legacy `backend/` and `prisma/` source trees have been removed from the versioned project after this checklist completed.
