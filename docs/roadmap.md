# BloggerBazar — roadmap, техдолг, открытые вопросы

Источник: handoff ChatGPT (разделы 4, 8, 9) + сверка с кодом на `313d26f` (Phase 0).
Текущая фаза и checkpoint — в `docs/status.md`. Долгосрочные продуктовые идеи — в корневом `ROADMAP.md`.

Статусы: **РЕШЕНО** · **ОБСУЖДАЛОСЬ, НЕ РЕШЕНО** · **[НЕ УВЕРЕН]** · **⚠ РАСХОЖДЕНИЕ** (код ≠ handoff, ждёт решения).

---

## Не требуется до первого MVP

chat · payments · escrow · disputes · Brand Face applications/deals · advanced analytics · advanced Deal lifecycle.

## Последовательность — РЕШЕНО

```text
3F-B (backend) → 3F-C (frontend bridge) → FEATURE FREEZE FOR MVP
→ Full UI Redesign → MVP Release Candidate QA → Closed MVP launch
```

Closed MVP (ориентир, не invariant): ~5–10 Businesses, ~20–50 Bloggers.

---

## Завершено

### Campaign/Application backend (`fdc6ece`, далее `68fe2f1`)

Blogger:
```text
POST /api/campaigns/{campaignId}/applications
GET  /api/campaign-applications/mine
GET  /api/campaign-applications/mine/{id}
POST /api/campaign-applications/mine/{id}/withdraw
```
Business:
```text
GET  /api/campaigns/mine/{campaignId}/applications
POST /api/campaigns/mine/{campaignId}/applications/{applicationId}/accept   → dealId
POST /api/campaigns/mine/{campaignId}/applications/{applicationId}/reject
```
Сверено с контроллерами. Дополнительно в коде есть legacy routes, не упомянутые в handoff:
`GET /api/campaign-applications/me`, `POST /api/campaign-applications/{id}/accept`,
`PATCH /api/campaign-applications/{id}/status` (legacy Accept тоже пишет snapshot).

### Design System v2 (`4782195`, `745c213`)
Foundation, не законченный redesign.

### Phase 3E-B — Applications frontend (`8420d89`)
Blogger list/details, withdraw, Business inbox, accept/reject, status filters, stale request protection,
409 reconciliation, role-aware routes, cache cleanup, Applications/Deals coexistence.
Routes: `#/my-application/{id}`, `#/my-campaign-applications/{campaignId}`.

### Phase 3F-A — Deal Integrity & Access foundation (`313d26f`)
Immutable Campaign snapshot v1; additive migration `20261001082446_AddDealCampaignTermsSnapshot`;
modern + legacy Accept snapshot; Collaboration Deal snapshot-free; `DealAccess` foundation;
active-role creation hardening; concurrency/idempotency preservation; dev seed исправлен; regression tests.

---

## Завершено — Phase 3F-B: Private Deal API, Security & Lifecycle (backend only)

**Входит:**
- active-role-scoped `GET /api/deals/me`;
- private `GET /api/deals/me/{dealId}`;
- Deal details DTO; snapshot-aware read model; `sourceType` / `termsSource`;
- foreign/missing → 404;
- idempotent Complete + concurrent Complete protection (conditional update, без migration);
- Review authorization hardening (selected role) + concurrent duplicate → 409;
- backend tests.

**НЕ входит:** frontend, `#/deal/{id}`, Application `dealId` frontend bridge, frontend cache, redesign, chat,
payments, escrow, cancellation, disputes, two-party completion, Brand Face Deals, notification subsystem.

Migration не ожидается. Если понадобится — STOP.

## Завершено — Phase 3F-C: frontend bridge

`#/deal/{id}`, `DealDetails`; Deal cards → route вместо legacy Modal; Business Accept → «Открыть сделку»;
Blogger Accepted Application → «Открыть сделку»; additive `dealId` в Application DTO при необходимости;
Deal cache/events; cleanup на role switch/logout; Complete frontend; existing Review action (если безопасно и просто);
RU/UZ; BottomNav; native Back; responsive tests.

**НЕ входит:** full visual redesign, chat, payments, escrow, disputes, Brand Face, advanced Deal lifecycle.

## Phase 4 — до feature freeze (решение владельца 2026-10-05)

- **4A — Contacts privacy (ОПУБЛИКОВАНО):** `/api/contacts/*` отдаёт личные контакты только участнику сделки
  (или по ранее оплаченной разблокировке); `GET /api/deals/me/{id}/contact`; контакты и «Написать в Telegram»
  на странице сделки.
- **4B — Offers (ОПУБЛИКОВАНО):** «Предложить сотрудничество» с профиля блогера, уведомление ботом с кнопкой, срок 48 ч,
  принятие → Deal (на базе существующего `CollaborationRequest`).
- **4C — Reviews 2.0 (ОПУБЛИКОВАНО):** публичный рейтинг бизнеса, «слепые» отзывы, окно 14 дней, напоминания
  (фоновая задача + миграция `AddReviewPublicationAndDealReminders`). Детали — D33.
- **FEATURE FREEZE** (D35) → Full UI Redesign R1–R7.

## После Phase 4 — FEATURE FREEZE + Full UI Redesign — РЕШЕНО (D35)

Feature freeze с 2026-10-06: только редизайн, баги, QA. Редизайн — frontend-only, foundation — DS v2.

- **R1 — Основа (ОПУБЛИКОВАНО):** общий `PageHeader` на всех экранах, переключатель языка только в Профиле,
  убраны градиенты и декоративный синий, единый формат бюджета `formatBudgetRange`, перевод города/категорий в Профиле.
- **R2a — Главная (НА REVIEW):** поиск первым (быстрые фильтры платформ/категорий ведут в отфильтрованный каталог),
  «Ваши дела» из реальных данных (отклики на кампании, сделки в работе, ждущие отзыва, предложения блогеру),
  подборки, «Как это работает» для новичков; убраны hero-блок и статистика маркетплейса.
  Каталоги и «Заявки» принимают ссылки `?q=`, `?category=`, `?platform=`, `?tab=`.
  Не взято из совета GPT: персональные рекомендации по интересам бизнеса (нет данных в профиле, feature freeze)
  и «N новых откликов сегодня» (нет такого счётчика в API).
- **R2b — Каталоги и карточки:** поиск блогеров/бренд-фейсов, каталог кампаний, единые фильтры, карточки блогера/кампании.
- **R3 — Детальные страницы:** блогер, бренд-фейс, кампания.
- **R4 — Заявки/сделки/предложения:** вкладки, карточки, страницы сделки/предложения/отклика.
- **R5 — Кабинет бизнеса:** мои кампании, создание/редактирование, входящие отклики.
- **R6 — Профиль и онбординг:** профиль, анкеты (Wizard), первый запуск.
- **R7 — QA:** 320–430px, тёмная тема, длинные UZ-строки, empty/loading/error, клавиатура.

Проверка каждого этапа: скриншоты «до/после» (Playwright + mock API, light/dark, Business/Blogger).

---

## Известный техдолг, legacy и баги

| # | Проблема | Статус / фаза | Подтверждено в коде (Phase 0) |
|---|---|---|---|
| 8.1 | Legacy `GET /api/deals/me`, `POST /api/deals/{id}/complete`, `POST /api/deals/{id}/reviews` используют union Blogger+Business identity, игнорируют `SelectedMarketplaceRole` | 3F-B | Да: `GetMyDealsHandler`, `CompleteDealHandler`, `CreateReviewHandler` |
| 8.2 | `MyDealDto` определяет сторону через `deal.Blogger.TelegramUserId == telegramUserId` | 3F-B | Да: `GetMyDeals.cs` и `MarketplaceCatalogReadModel.GetDealsAsync` |
| 8.3 | Read model берёт live `Campaign.Title` вместо snapshot | 3F-B | Да |
| 8.4 | Английская строка `"Direct collaboration request"` в RU/UZ UI | 3F-B (`sourceType`) + 3F-C (перевод) | Да; `title` пока оставить |
| 8.5 | Нет Deal details endpoint | 3F-B | Да |
| 8.6 | Complete race: два concurrent Complete проходят, перезаписывают `CompletedAtUtc`, дублируют notification | 3F-B | Да: load → `deal.Complete()` → `SaveChangesAsync`, без условия |
| 8.7 | Повторный Complete → 409 (`"Only active deals can be completed."`) | 3F-B → 200 + текущий DTO | Да |
| 8.8 | Concurrent duplicate Review → unique violation → 500 | 3F-B → 409 | Да: `SaveChangesAsync`, не `TrySaveChangesAsync` |
| 8.9 | Deals открываются Modal внутри `#/requests` | 3F-C | не проверялось (frontend) |
| 8.10 | Modern Accept возвращает `dealId`, frontend игнорирует; у Blogger нет перехода к Deal | 3F-C | `dealId` в ответе — да |
| 8.11 | Нет Deal cache namespace (MyRequests на useState/useEffect) | 3F-C | не проверялось |
| 8.12 | Нет pagination/status filter | СОЗНАТЕЛЬНО ОТЛОЖЕНО | — |
| 8.13 | `Cancelled` есть, перехода нет | СОЗНАТЕЛЬНО ОТЛОЖЕНО (после MVP) | Да |
| 8.14 | Two-party completion | СОЗНАТЕЛЬНО ОТЛОЖЕНО | — |
| 8.15 | Full Review redesign (hardening — в 3F-B) | СОЗНАТЕЛЬНО ОТЛОЖЕНО | — |
| 8.16 | Brand Face applications/deals | СОЗНАТЕЛЬНО ОТЛОЖЕНО | — |
| 8.17 | Campaign edit last-write-wins, нет concurrency token | ИЗВЕСТНО, не blocker MVP, не в 3F | — |
| 8.18 | 13 integration tests skipped | ИСПРАВЛЕНО (`7722609`): CI запускает все integration-тесты | 13 × `[IntegrationFact]`, skip без `RUN_INTEGRATION_TESTS=true`. ⚠ Уточнение: skip только в локальных прогонах; CI (`ci.yml`) запускает их с Testcontainers |
| 8.19 | Применена ли snapshot migration в production | ОБСУЖДАЛОСЬ, НЕ ПОДТВЕРЖДЕНО | `appsettings.json`: `ApplyMigrationsOnStartup=false`; Development: `true`; Railway env не проверялся |
| 8.20 | Публичный список отзывов блогера всегда пуст: фильтр требовал `review.Business` у отзыва о блогере (там `BusinessId = null`) | ИСПРАВЛЕНО в 4C (фильтр через `review.Deal`) | `ReviewReadModel.GetBloggerReviewsAsync` |
| 8.21 | Напоминание/уведомление ведёт на `/deal/{id}`; если у получателя выбрана другая роль, сделка откроется как 404 | ИЗВЕСТНО, не blocker MVP | selected-role authorization (D-правило 1) |

Дополнительно найдено в Phase 0 (классификация утверждена, см. `docs/decisions.md`):
- **P0-1.** `CreateReviewHandler` проверяет `Status != Completed` (→ 409) и `ExistsAsync` (→ 409) **до** проверки
  участия: чужой Active Deal → 409, чужой Completed → 403, missing → 404 — существование чужого Deal различимо.
  → 3F-B (D3).
- **P0-2.** Legacy list скрывает Deal с удалённым (soft-deleted) профилем любой стороны, но Complete и Review
  такой Deal **не** проверяют — его можно завершить/отрецензировать по id. → 3F-B (D27).
- **P0-3.** Review uniqueness — `(DealId, ReviewerTelegramUserId)`, т.е. по Telegram user, а не по роли.
  [НЕ УВЕРЕН] возможен ли Deal, где оба профиля принадлежат одному Telegram user (self-deal); если да —
  второй review невозможен. LOW, только фиксация.

---

## Открытые вопросы

### 9.1 Production historical Deals — ОБСУЖДАЛОСЬ, НЕ РЕШЕНО
Неизвестно, сколько production Deals без snapshot. Для них — honest fallback (`liveCampaignFallback`).

### 9.2 Deleted counterparty semantics — РЕШЕНО (D27, 2026-10-05): скрывать Deal полностью, 404
Текущее поведение: global query filters нет; soft delete — явными условиями `!IsDeleted` в репозиториях.
Legacy list (`GetDealsAsync`) фильтрует `!deal.Blogger.IsDeleted && !deal.Business.IsDeleted` — Deal скрыт целиком.
Complete/Review удалённость не проверяют (P0-2). Для 3F-B не расширять доступ, сохранить privacy.
Решение: скрывать целиком (list + details/Complete/Review → 404). «Показать без identity» — пересмотр после MVP.

### 9.3 Brand Face future architecture — ОБСУЖДАЛОСЬ, НЕ РЕШЕНО
Проектируется позже, не внутри 3F.

### 9.4 Closed Campaign inbox — ОБСУЖДАЛОСЬ, НЕ РЕШЕНО (low priority)
Нужен ли owner полноценный applications inbox после закрытия Campaign. Не blocker.

### 9.5 Currency — ОБСУЖДАЛОСЬ, НЕ РЕШЕНО
Frontend форматировал budget как UZS; backend currency не закреплял. Не добавлять Currency в Deal без решения.

### 9.6 Collaboration display details — [НЕ УВЕРЕН]
Кроме `sourceType` + frontend translation, отдельная immutable terms model для CollaborationRequest не утверждалась.
Не создавать в 3F-B.

### D-OPEN-1 Строковая сериализация новых semantic enum — РЕШЕНО (D26)
См. `docs/decisions.md`.

### 9.7 Контакт партнёра внутри сделки — РЕШЕНО (D31, 2026-10-05): контакты только участникам сделки
Как участники связываются после Accept. Предложение Claude: кнопка «Написать в Telegram» на странице сделки
(без своего чата). Конфликт с платным открытием контактов (Click). Варианты: A — бесплатно после Accept;
B — платно, как сейчас; C — не добавлять. Решение владельца нужно до feature freeze.

### 9.8 Оплата / escrow и собственный чат — ОБСУЖДАЛОСЬ (2026-10-05)
Рекомендация Claude: не до закрытого MVP. Escrow требует лицензированного партнёра, споров и возвратов;
свой чат избыточен внутри Telegram. Вернуться после MVP по фактическим проблемам пользователей.
Конкурент: Blogix (blogix.uz) — биржа размещений Telegram/Instagram/YouTube (данные из поиска, сайт не открыт).
