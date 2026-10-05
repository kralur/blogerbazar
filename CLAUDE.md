# BloggerBazar — инструкции для Claude Code

Marketplace рекламного сотрудничества: **Blogger**, **Business**, **Brand Face**.
Основной клиент — Telegram / iMe Mini App. Цель MVP — замкнуть цикл
`Campaign → Application → Accept → Deal → Complete` между Business и Blogger.

Принятые архитектурные решения — в `docs/decisions.md`. **Не пересматривать их без явного
согласования.** Если код расходится с документацией — зафиксировать расхождение и остановиться.

Текущий checkpoint, фаза и последние результаты gates — только в `docs/status.md`.

## Стек

- Backend: C# / ASP.NET Core 9, Clean Architecture, CQRS (MediatR), EF Core + PostgreSQL (Npgsql),
  Redis, FluentValidation, xUnit (+ Testcontainers.PostgreSql для integration).
- Frontend: React 18, TypeScript, Vite, Vitest, Tailwind; Telegram Mini App; RU/UZ; light/dark; DS v2.
- Infra: Railway, PostgreSQL, Redis, Cloudflare R2, Telegram auth (`Authorization: tma <initData>`).

## Структура

```text
BloggerBazar.sln
src/BloggerBazar.Domain/          entities, enums (без зависимостей)
src/BloggerBazar.Application/     Features/<Area>/ — команды, запросы, handlers, DTO, validators
                                  Abstractions/Persistence — интерфейсы репозиториев, IUnitOfWork
src/BloggerBazar.Infrastructure/  Persistence/ — DbContext, репозитории, read models, Migrations/
src/BloggerBazar.Api/             Controllers/, Middleware/ExceptionHandlingMiddleware.cs, Errors/
tests/BloggerBazar.Application.Tests/   unit (fake repositories) + Integration/ (Testcontainers)
frontend/                         src/ (App.tsx, pages/, components/), tests/ (vitest)
scripts/check-i18n.mjs            i18n audit
```

Ключевые места Deal-подсистемы: `src/BloggerBazar.Application/Features/Deals/`
(`DealAccess.cs`, `GetMyDeals.cs`, `CompleteDeal.cs`, `DealDto.cs`),
`Features/Reviews/CreateReview.cs`, `Api/Controllers/DealsController.cs`,
`Infrastructure/Persistence/MarketplaceCatalogReadModel.cs` (`GetDealsAsync`).

## Команды

Backend (из корня):

```bash
dotnet build BloggerBazar.sln
dotnet test BloggerBazar.sln                       # integration-тесты skipped без env (локально)
RUN_INTEGRATION_TESTS=true dotnet test BloggerBazar.sln   # нужен Docker (Testcontainers)
dotnet tool restore && dotnet ef migrations has-pending-model-changes \
  --project src/BloggerBazar.Infrastructure --startup-project src/BloggerBazar.Api
```

Frontend (из корня, npm workspaces):

```bash
npm ci
npm test               # vitest run
npm run build          # tsc && vite build
npm run i18n:audit
```

Общее: `git diff --check`.

## Жёсткие правила

1. **Selected-role authorization.** Доступ определяется `PlatformUser.SelectedMarketplaceRole` +
   профилем именно этой роли. Запрещено `Blogger OR Business`, fallback на другой профиль,
   определение стороны по Telegram user id.
2. **Foreign = missing = 404** для private-ресурсов (Deal). Lookup сразу scoped по участнику;
   никакого «find by id → ownership check → 403».
3. **Backend language-neutral.** Никаких UI/локализованных строк как semantic discriminator
   (пример антипаттерна: `"Direct collaboration request"`). Перевод — frontend i18n (RU/UZ).
4. **DTO-изменения additive** в backend-only фазах; старый frontend должен продолжать работать.
5. **Без infra/config изменений:** Railway, R2, Telegram/Bot/webhook, `.env`, secrets,
   `appsettings*.json`, runtime, deployment — только по явному разрешению.
6. **Migration только с явным approval.** Минимальная, additive, без unrelated drift;
   проверить migration + Designer + ModelSnapshot; никакого backfill недостоверными данными.
   Если реализация неожиданно требует migration — STOP и объяснить.
7. **Не устанавливать** SDK/системные пакеты без разрешения.
8. Сначала искать существующий pattern (`TrySaveChangesAsync`, `ExecuteUpdateAsync`
   conditional update, `*Access` helpers). Не создавать новый framework.
9. Не расширять scope фазы (см. «НЕ входит» в `docs/roadmap.md`).

## Конвенции API

- Ошибки: `ExceptionHandlingMiddleware` → ProblemDetails с `code`.
  `UnauthorizedAccessException` → 403, `InvalidOperationException` с «not found» → 404,
  прочие `InvalidOperationException` → 409, `ValidationException` → 422.
- Существующие enum в JSON — **числа** (`int Status`); не менять. **Новые** semantic поля —
  `string` с фиксированным набором camelCase-значений (D26). Глобальный `JsonStringEnumConverter`
  не подключать — сломает текущий frontend.
- Unique race: DB constraint — окончательная защита; `TrySaveChangesAsync` → controlled 409,
  raw `DbUpdateException` наружу не уходит.

## Git workflow

1. Перед любой фазой: `git fetch origin`; доложить branch, HEAD, `origin/main`, merge-base,
   `git status`, stash. Если `origin/main` ≠ ожидаемому baseline — **STOP** (без merge/rebase).
2. Работа в feature branch, основанной на точном `origin/main` (`claude/...`).
3. **До review: no commit, no push, no merge, no rebase.**
4. Публикация только после approval: commit → push feature branch → fetch → fast-forward
   `main` → push `main` → финальная проверка. Без merge commit, без force push.
   Feature branch сохраняется.

## Gates

Backend затронут: `dotnet build`, `dotnet test` (указать число skipped), `git diff --check`,
EF pending model changes (при изменении модели), secret scan изменённых файлов.
Перед публикацией (если среда позволяет): `npm test`, `npm run i18n:audit`, `npm run build`.

CI (`.github/workflows/ci.yml`) на PR в `main` и push в `main` запускает backend с
`RUN_INTEGRATION_TESTS=true` (Testcontainers) и frontend. Если локально нет `dotnet`/Docker —
открыть **draft PR** feature-ветки, чтобы получить реальные результаты; публикация всё равно fast-forward.

**Правило NOT RUN:** если команда реально не запускалась (нет `dotnet`, нет Docker и т.п.) —
писать `NOT RUN`. Никогда не переносить старые результаты как новые.

## Формат отчёта

Implementation report:
`A. Git · B. Что реализовано · C. Changed files · D. Security/access behavior · E. Tests ·
F. Build · G. Diff check · H. Secret scan · I. Scope confirmation · J. Risks/unresolved · K. Verdict`

Review findings: `BLOCKER / HIGH / MEDIUM / LOW` — файл, место, достижимый сценарий,
минимальное исправление. Без теоретических проблем без сценария.

Строка **«Второе мнение (GPT)»** в каждом отчёте: «нужно» только при спорном моменте
(несколько равноценных архитектурных вариантов, компромисс по безопасности, продуктовое решение),
с конкретным вопросом для GPT; иначе «не нужно» и короткая причина.

Вердикт: `READY FOR <PHASE> REVIEW` или `BLOCKED`.
Publication report: commit SHA и message, old main → new main, feature branch SHA,
подтверждение fast-forward, gates, финальный clean state.

## Документация

- `docs/decisions.md` — архитектурные решения, причины, отвергнутые альтернативы.
- `docs/roadmap.md` — фазы, техдолг, открытые вопросы.
- `docs/status.md` — текущий checkpoint, фаза, результаты gates (волатильное).
- `docs/DESIGN_SYSTEM_V2.md`, `docs/MIGRATION_CHECKLIST.md`, `ARCHITECTURE.md`, `README.md` — существующие.
