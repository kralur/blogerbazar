# BloggerBazar — архитектурные решения

Источник: handoff ChatGPT (раздел 5) + сверка с кодом на `313d26f` (Phase 0, 2026-10-05).
Решения **приняты**; пересмотр только по явному согласованию.

Статусы: **РЕШЕНО** · **ОБСУЖДАЛОСЬ, НЕ РЕШЕНО** · **[НЕ УВЕРЕН]**.
Пометка **⚠ РАСХОЖДЕНИЕ** — найденное отличие кода от handoff; ожидает решения владельца,
текст решения не исправлялся.

---

## Доменная модель (справочно)

`MarketplaceRole` (`Domain/Enums/MarketplaceRole.cs`): `Blogger = 0`, `BrandFace = 1`, `Business = 2`.
Активная роль — `PlatformUser.SelectedMarketplaceRole` (nullable).

`CampaignApplicationStatus`: `Sent = 0`, `Viewed = 1`, `Accepted = 2`, `Rejected = 3`, `Withdrawn = 4`.
⚠ РАСХОЖДЕНИЕ (именование): handoff называет enum `ApplicationStatus`, в коде — `CampaignApplicationStatus`.
Повторная заявка после Rejected/Withdrawn запрещена unique `(CampaignId, BloggerId)`.

`DealStatus`: `Active = 0`, `Completed = 1`, `Cancelled = 2` (переход в Cancelled не реализован).

Deal source links: `CampaignApplicationId?`, `CollaborationRequestId?` — partial unique indexes
(`... IS NOT NULL`). Review: unique `(DealId, ReviewerTelegramUserId)`, rating 1..5, comment ≤ 1000.

---

## D1. Selected-role Deal authorization — РЕШЕНО

Deal access = `PlatformUser` + `SelectedMarketplaceRole` + профиль именно выбранной роли + participant predicate.
- Blogger: только `Deal.BloggerId == current BloggerProfile.Id`.
- Business: только `Deal.BusinessId == current BusinessProfile.Id`.
- Brand Face: Deal flow в Phase 3F не поддерживается.

**Причина:** один Telegram user может иметь несколько профилей; legacy union identity показывает Deal другой роли.
**Отвергнуто:** union `Blogger OR Business`; fallback на другой профиль; определение стороны только по Telegram user id.

## D2. DealAccess — РЕШЕНО

`DealAccess` (Phase 3F-A, `Application/Features/Deals/DealAccess.cs`) — foundation для Deal authorization.
Учитывает: PlatformUser существует, не blocked, не deleted, `SelectedMarketplaceRole`, профиль выбранной роли,
participant identity. Не создавать параллельный механизм; не превращать в общий authorization framework.

⚠ РАСХОЖДЕНИЕ (объём): сейчас `DealAccess` покрывает только **создание** Deal —
`RequireBusinessAsync` (Accept) и `RequireCollaborationParticipantAsync` (CollaborationRequest → Deal).
Scoped lookup существующего Deal по участнику (для list/details/Complete/Review) в нём отсутствует;
нарушения выражаются `UnauthorizedAccessException` (→ 403). Это ожидаемый объём работ 3F-B, но
«DealAccess учитывает participant identity» верно только для CollaborationRequest.

## D3. Foreign Deal = missing Deal — РЕШЕНО

Для private Deal resource: foreign → 404, missing → 404. Lookup scoped по участнику сразу.
**Причина:** «find by id → ownership → 403» раскрывает существование чужого Deal.
**Отвергнуто:** различать 403 foreign / 404 missing для private details/actions.

## D4. Immutable Campaign terms snapshot — РЕШЕНО (реализовано в 3F-A)

Campaign-origin Deal фиксирует snapshot в момент первого успешного Accept. Snapshot v1 (сверено с `Domain/Entities/Deal.cs`):

| Поле | Тип |
|---|---|
| `CampaignTermsSnapshotVersion` | `short?` (smallint), значение `1` |
| `CampaignTitleSnapshot` | `string?` (≤160) |
| `CampaignDescriptionSnapshot` | `string?` (≤3000) |
| `CampaignCitySnapshot` | `string?` (≤80) |
| `CampaignCategoriesSnapshot` | `IReadOnlyCollection<string>?` (`text[]`) |
| `CampaignRequirementsSnapshot` | `IReadOnlyCollection<string>?` (`text[]`) |
| `CampaignBudgetFromSnapshot` | `int?` |
| `CampaignBudgetToSnapshot` | `int?` |
| `CampaignDeadlineSnapshot` | `DateTime?` |

Arrays копируются defensively (`Array.AsReadOnly(... .ToArray())`).
**Причина:** Campaign A → accept → Deal → Campaign edited to B: Deal должен показывать A.

**Explicit columns вместо versioned JSON** — РЕШЕНО. Причины: type safety, текущий EF/PostgreSQL стиль,
прозрачная migration, простой DTO, меньше abstraction для MVP.
**Отвергнуто:** versioned JSON snapshot (слабее типизация, сложнее EF/query/validation, преждевременная abstraction).

Примечание: публичная перегрузка `Deal.Create(campaignApplicationId, bloggerId, businessId)` без snapshot
сохранилась (используется только тестами); production-пути Accept используют перегрузку со snapshot.

## D5. Legacy snapshot strategy — РЕШЕНО

Migration не backfill-ит старые Deals: snapshot version = NULL.
**Причина:** текущая Campaign — не доказательство исторических условий.
**Отвергнуто:** backfill из текущей Campaign.

## D6. Collaboration Deal snapshot — РЕШЕНО

Deal из CollaborationRequest: Campaign snapshot = NULL.
**Отвергнуто:** fake Campaign, fake budget, synthetic snapshot.

## D7. Campaign budget semantics — РЕШЕНО

Snapshot `CampaignBudgetFrom/To` = диапазон бюджета Campaign на момент Accept, **не** согласованная цена.
Application не содержит proposed/negotiated price; currency в domain/API не закреплена.
**Не использовать:** `AgreedAmount`, `DealPrice`, `NegotiatedPrice` (и `Currency`) без отдельной negotiation model.
**Причина:** нельзя превращать неизвестную семантику в финансовый факт.

## D8. sourceType — РЕШЕНО для 3F-B

Deal DTO получает language-neutral structured поле (`sourceType` или имя по конвенциям),
минимум: `CampaignApplication` / `CollaborationRequest`. Существующий `title` пока не удалять.
**Причина:** legacy `"Direct collaboration request"` попадает прямо в RU/UZ UI.

## D9. termsSource — РЕШЕНО для 3F-B

Read model различает `snapshot` / `liveCampaignFallback` / `collaboration` (или эквивалентные language-neutral значения).
- snapshot — Campaign Deal со snapshot v1;
- liveCampaignFallback — исторический Campaign Deal без snapshot; consumer знает, что это fallback;
- collaboration — Deal из CollaborationRequest, snapshot отсутствует.

## D10. Deal routes — РЕШЕНО

Collection `GET /api/deals/me`; private details `GET /api/deals/me/{dealId}`.
Action routes сохраняются ради compatibility: `POST /api/deals/{id}/complete`, `POST /api/deals/{id}/reviews`.
**Отвергнуто:** `GET /api/deals/{id}` как новый private read route.

## D11. Deal list pagination — РЕШЕНО для 3F

Не добавлять pagination/filter/search в 3F-B (список MVP мал, далее redesign). SHOULD после MVP.

## D12. Complete semantics — РЕШЕНО

Любой participant завершает Active Deal; двустороннего подтверждения до MVP нет.
`Active → Completed`, `CompletedAtUtc = server UTC` при первом Complete.
Application остаётся Accepted; Campaign lifecycle не меняется.

## D13. Complete idempotency — РЕШЕНО

Повторный Complete → 200 с текущим Completed DTO (не 409); timestamp не меняется.
**Причина:** double-click/retry безопасны.

## D14. Complete concurrency — РЕШЕНО

Conditional DB update (семантически `UPDATE deals SET status=Completed, completed_at_utc=now WHERE id=… AND status=Active`)
или EF-эквивалент (в проекте есть прецедент: `PlatformUserRepository.SoftDeleteIfActiveAsync` через `ExecuteUpdateAsync`).
Affected rows = 0 → перечитать scoped Deal: Completed → persisted DTO; foreign/missing → 404; будущий Cancelled → 409.
**Причина:** без двойной перезаписи timestamp, двойных side effects и 500.
**Отвергнуто:** concurrency token, row-version migration, migration только ради Complete. Migration в 3F-B не ожидается.

## D15. Review concurrency — РЕШЕНО

Unique DB constraint — окончательная защита. Concurrent duplicate → controlled 409, не 500.
Использовать `TrySaveChangesAsync` (ловит `PostgresErrorCodes.UniqueViolation`, чистит ChangeTracker).

## D16. Review actor — РЕШЕНО

Reviewer определяется `SelectedMarketplaceRole` (не «есть Blogger — значит Blogger, иначе Business»).
Blogger → Business; Business → Blogger; Brand Face unsupported.

## D17. Backend language-neutral — РЕШЕНО

DTO/API не отдают UI-локализованные строки как semantic discriminator; RU/UZ — frontend i18n.

## D18. Phase separation — РЕШЕНО

3F-A integrity/snapshot/access foundation · 3F-B backend Deal API/security/lifecycle · 3F-C frontend bridge.
**Отвергнуто:** весь Deal subsystem backend+frontend одним checkpoint.

## D19. Brand Face — РЕШЕНО

Brand Face applications/deals не добавлять в MVP Deal flow. Nullable `BrandFaceId` — неправильный shortcut;
нужен отдельный architecture decision позже.

## D20. Campaign editing after Deal — РЕШЕНО

Не запрещать редактирование Campaign после Accept; историческая корректность — через snapshot.
**Отвергнуто:** глобальный запрет edit после первого Deal (Campaign может работать с несколькими creators).

## D21. Deal direct CampaignId — РЕШЕНО

Не добавлять `Deal.CampaignId`; Campaign выводится через CampaignApplication.

## D22. Application → Deal bridge — РЕШЕНО (реализация в 3F-C)

Additive optional `dealId` в relevant CampaignApplication DTO: без N+1, без отдельного lookup endpoint,
backward-compatible. Modern Business Accept уже возвращает `dealId` (`CampaignApplicationDecisionDto`).

## D23. Frontend Deal route — РЕШЕНО для 3F-C

`#/deal/{id}`; Deal cards ведут на route вместо legacy modal; BottomNav считает его частью Requests.

## D24. Deal cache — РЕШЕНО для 3F-C

Lightweight cache/event pattern как у Applications. Без Redux/Zustand/нового global state.
Очистка при logout и смене MarketplaceRole.

## D25. Design System — РЕШЕНО

DS v2 — foundation; 3F-C использует существующие primitives; full redesign — отдельная фаза после 3F-C.

---

## Конвенции (из handoff §6)

- Сначала искать существующий pattern; не создавать новый framework.
- Persistence: `TrySaveChangesAsync` для unique-race recovery; raw `DbUpdateException` наружу не уходит;
  winner/duplicate перечитывается при необходимости.
- DTO: additive changes; structured semantic fields вместо UI strings; private DTO без contacts/phone/
  private Telegram info/payment data/лишних raw IDs.
- i18n: frontend RU/UZ, `npm run i18n:audit` при новых ключах.
- Migration: только с approval, минимальная, additive; inspect migration + Designer + ModelSnapshot.
- Frontend async: stale request guards, AbortController, cache namespaces, очистка на logout/role switch.
- Telegram/iMe navigation: native Back хрупкий — использовать существующие patterns, без history redesign.

## Решения по конвенциям и Phase 3F-B

### D26. Сериализация новых semantic значений — РЕШЕНО (2026-10-05)

Существующие enum остаются числами (`int Status` и т.п.) — не менять, чтобы не сломать frontend.
Новые semantic поля (например `sourceType`, `termsSource`) — `string` с фиксированным набором camelCase-значений,
объявленных константами в Application:
- `sourceType`: `"campaignApplication"` | `"collaborationRequest"`;
- `termsSource`: `"snapshot"` | `"liveCampaignFallback"` | `"collaboration"`.

**Причина:** самоописываемый контракт, прямо ложится в TypeScript union и i18n-ключи, additive.
**Отвергнуто:** глобальный `JsonStringEnumConverter` (ломает существующие числовые поля); числа для новых полей
(непрозрачно для frontend/i18n).

## Решения владельца для Phase 3F-B (2026-10-05)

### D27. Deleted counterparty — РЕШЕНО (закрывает вопрос 9.2)

Если профиль любой стороны soft-deleted, Deal для участника скрыт полностью:
list не показывает, details / Complete / Review → 404. Согласовано с существующим legacy list.
**Причина:** не изобретать новую deletion policy; privacy.
**Отвергнуто сейчас:** показывать Deal без identity counterparty (нужен отдельный продукт/UI design). Пересмотр — после MVP.

### D28. Scoped Deal lookup в DealAccess — РЕШЕНО

Participant resolution по `SelectedMarketplaceRole` добавляется в `DealAccess`; репозиторий получает lookup,
сразу ограниченный участником (роль + profile id). Отдельный helper не создаётся (D2).

### D29. Неподдерживаемая роль / нет профиля — РЕШЕНО

- `GET /api/deals/me`: Brand Face или нет профиля выбранной роли → **200 `[]`**
  (`ProfileDashboard.tsx` вызывает `getMyDeals()` для любой роли — 403 сломал бы старый frontend).
- details и actions: → **404** (foreign = missing).
- blocked / deleted PlatformUser → 403 (существующий `PlatformUserAccessPolicy`, без изменений).

### D30. Проверка concurrency-тестов — РЕШЕНО

Concurrency Complete/Review — `[IntegrationFact]` (реальный Postgres); ветка handler «0 rows → перечитать» —
unit-тест на fakes. Реальный прогон — через CI на **draft PR** feature-ветки в `main`
(CI запускается только на PR/push в `main`). Публикация по-прежнему fast-forward.

### P0-находки — классификация

- P0-1 (Review различает foreign/missing) — закрывается в 3F-B через D3.
- P0-2 (Complete/Review по Deal с удалённой стороной) — закрывается в 3F-B через D27.
- P0-3 (Review uniqueness по Telegram user) — LOW, только фиксация, в 3F-B не трогать.
