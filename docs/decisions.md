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

## D19. Brand Face — ЗАМЕНЕНО решением D46 (2026-10-09)

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

### D27. Deleted counterparty — ПЕРЕСМОТРЕНО в D43 (2026-10-09) (закрывает вопрос 9.2)

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

## Продуктовые решения владельца (2026-10-05, после 3F-C)

### D31. Контакты только после сделки («сначала предложение») — РЕШЕНО

Личные контакты (телефон, Telegram, email) видны только участникам сделки — после Accept отклика
или принятого предложения. Публичные соцсети блогера (Instagram, канал) остаются в профиле.
Бизнес находит блогера в каталоге → «Предложить сотрудничество» (формат, бюджет, сроки, текст) →
уведомление блогеру в Telegram-боте → принять/отклонить, предложение сгорает через 48 ч →
при принятии создаётся Deal → контакты и «Написать в Telegram» на странице сделки.
Ранее оплаченные разблокировки контактов (`ContactUnlock`) продолжают действовать.
**Причина:** платформа сделок, а не справочник: сделки фиксируются, появляются отзывы и рейтинги;
блогеров не спамят. Открыть контакты позже легко, закрыть после открытия — почти невозможно.
**Отвергнуто:** публичные контакты (решение Phase 2, `CHANGELOG.md`); контакт за разовую оплату.
**Пересмотр:** по итогам закрытого MVP (опрос бизнесов); возможен прямой контакт в Business Pro.

### D32. Монетизация — РЕШЕНО (направление)

Закрытый MVP — всё бесплатно. Публичный запуск: Бизнес — Free (1 кампания, лимит предложений) +
Pro (пропуск на 30 дней через Click, без автосписания); Блогер и Brand Face — бесплатно + платный Boost
с пометкой «Реклама». Boost не заменяет порядок по verified/рейтингу/активности.
Комиссия со сделки — только после escrow через лицензированного партнёра.
**Отвергнуто:** подписка со всех трёх ролей (снижает предложение); комиссия без оплаты через платформу.

### D33. Отзывы 2.0 и напоминания — РЕШЕНО (реализация в 4C)

Отзывы только по реальной Completed сделке, обе стороны. Публичный рейтинг бизнеса (профиль и кампании).
«Слепые» отзывы: публикуются, когда оставили обе стороны, или через 14 дней. Напоминания ботом
с кнопкой открытия сделки: сразу, +1, +3, +7 дней; стоп после отзыва; окно 14 дней; только 9:00–21:00
(Ташкент). Напоминание завершить зависшую Active сделку. Требует фоновой задачи и миграции (approval отдельно).

**Реализация (4C, миграция `AddReviewPublicationAndDealReminders` одобрена владельцем):**
- `reviews.PublishedAtUtc` (null = скрыт) + глобальный EF query filter «только опубликованные» — все рейтинги,
  счётчики и списки учитывают «слепоту» без правки каждого запроса. Код, которому нужны скрытые отзывы
  («уже оставил отзыв», флаги сделки, напоминания), использует `IgnoreQueryFilters`. Существующие отзывы
  при миграции получили `PublishedAtUtc = CreatedAtUtc` (они уже были публичны).
- Публикация: сразу после второго отзыва (`PublishRevealedAsync(dealId)`) и ежечасно фоновой задачей
  (пара отзывов или истёкшее окно). Окно отзыва — 14 дней от `CompletedAtUtc`; после него `CreateReview` → 409,
  `CanReview = false`; в DTO сделки additive `reviewDeadlineUtc`.
- «Сразу» = уведомление при завершении сделки (было с 3F-B). Далее: +1/+3/+7 дней (только последняя наступившая
  стадия, без «догоняющих» дублей), сторонам без отзыва; зависшая Active: +7 и +14 дней обеим сторонам.
- `deal_reminders` с уникальным `(DealId, Kind, RecipientRole)`; запись — `INSERT … ON CONFLICT DO NOTHING`
  до отправки (at-most-once, безопасно при нескольких инстансах). Сбой Telegram не повторяется.
- `DealReminderWorker` (hosted service, раз в час, первый запуск через 1 мин). Отключается
  `DealReminders:Enabled=false` (по умолчанию включён; в тестовых хостах выключен).
- Публичный рейтинг бизнеса: `GET /api/businesses/{id}/reviews` → `{ rating, reviewsCount, items }`;
  показывается на странице кампании (отдельной публичной страницы бизнеса пока нет).
- Уточнение (RC QA, 2026-10-07, по просьбе владельца): сторона без отзыва видит, что партнёр уже оценил сделку
  (additive `partnerHasReviewed` в списке и в сделке: «Вам оставили отзыв», «Оставьте свой, чтобы его увидеть»).
  Оценка и текст по-прежнему скрыты до публикации.

### D34. Brand Face в сделках — ЗАМЕНЕНО решением D46 (2026-10-09)

Предложения, сделки и отзывы для Brand Face требуют отдельной архитектуры (D19). Закрытый MVP — с блогерами.

### D35. Feature freeze и визуальное направление редизайна — РЕШЕНО (владелец + второе мнение GPT)

После 4C — **feature freeze**: новых продуктовых функций до закрытого запуска нет, только редизайн UI,
исправления багов и QA. Редизайн — только frontend (без API/DTO/миграций), этапами R1–R7 (`docs/roadmap.md`).

Направление: **«Premium creator marketplace × modern fintech × Telegram-native»** — развитие DS v2, не новый стиль.
- Пропорция ≈ 80% нейтральные поверхности, 15% тёмный (текст, выбранное), 5% lime.
- Lime только: главное действие (CTA), активное/выбранное состояние, verified, мелкие акценты.
- Синий — только информационные статусы/фокус, не декор. Градиенты, glassmorphism, свечения — запрещены.
- Главный визуальный контент — фото и цифры (охват, цена, рейтинг), а не декоративные блоки.
- Локальность — через реальных авторов, UZS, RU/UZ, а не через орнаменты.
- Переключатель языка — только в Профиле и на экранах первого запуска.

Отвергнуто: полная смена стиля (дольше, теряется узнаваемость), «lime везде», крипто/AI-эстетика.

### D36. Срок кампании и коды бизнес-конфликтов — РЕШЕНО (владелец: «логичнее и красивее»)

Найдено на MVP QA (устройство): кампания с прошедшим дедлайном оставалась в каталоге как открытая.
- Дедлайн — календарный день; кампания открыта **включительно** до этого дня (по UTC), затем считается истёкшей
  (`Campaign.IsExpired`). Данные не меняются: статус остаётся `Published`, без фонового автозакрытия и без миграции.
- Каталог, старый поиск и главная (включая счётчик активных кампаний) показывают только неистёкшие
  (`MarketplaceCatalogVisibility.OpenForApplications`). Страница кампании по прямой ссылке остаётся доступной
  и показывает «Приём откликов завершён»; отклик на истёкшую кампанию → 409 `campaign_expired`.
- Создание/редактирование: дедлайн в прошлом не принимается (422). Бизнес видит на своей кампании подсказку
  продлить срок — после продления кампания снова видна.
- `BusinessRuleConflictException(code)` → 409 с конкретным `code` (snake_case, как другие коды ошибок),
  чтобы клиент различал причины: `offer_daily_limit`, `offer_already_active`, `campaign_expired`,
  `favorite_own_profile` (свой профиль в избранное; кнопка на своих карточках не показывается).
  Прочие `InvalidOperationException` по-прежнему дают общий `conflict`.
- Форма предложения: срок по умолчанию — через 7 дней.

Отвергнуто: автозакрытие статусом (нужен фоновый процесс и изменение данных), 404 для истёкшей кампании
(ломает ссылки из уведомлений и откликов).

### D37. Создание кампании на виду и предложение «из кампании» — РЕШЕНО (владелец)

На QA бизнес не нашёл, как создать объявление (кампанию): действие было спрятано за «Мои кампании» и «+».
- На экране кампаний у бизнеса в шапке — «Мои кампании» и основная кнопка «Создать» (плавающая «+» убрана).
- В форме предложения блогеру — «Заполнить из кампании»: список своих открытых неистёкших кампаний;
  подставляются бюджет (верхняя граница), срок и текст (название + описание). Поля остаются редактируемыми.
  Только frontend, предложение к кампании **не привязывается** (без API/DTO/миграций).
- После закрытого запуска (вне D35): «Пригласить в кампанию» — предложение, привязанное к кампании, с её условиями в сделке.

### D38. Экран «Настройки» и выбор темы — РЕШЕНО (владелец)

Профиль отвечает на «кто я на площадке» (роль, анкета, переходы); «как работает приложение» — отдельный
экран `#/settings` (строка «Настройки ›» в профиле, видна всегда, даже без анкеты).
- Настройки: язык (RU/UZ), тема, «Выйти», «Удалить аккаунт», версия. Выход/удаление — общий `AccountActions`.
- Тема: «Как в Telegram» (по умолчанию) / «Светлая» / «Тёмная». Выбор хранится на устройстве
  (`localStorage bloggerbazar.theme`) и переопределяет тему Telegram; палитра Telegram (`themeParams`)
  используется только в режиме «Как в Telegram». Цвета шапки/фона Telegram следуют выбранной теме.
- Только frontend. Небольшое исключение из D35 (новая настройка), согласовано владельцем.
- Не добавлено: уведомления (нужен backend), поддержка (нет контакта).

### D39. Язык сообщений бота — РЕШЕНО (владелец, 2026-10-07)

Бот пишет на языке интерфейса пользователя, а не на двух языках сразу.
- Источник — язык, выбранный в приложении (Настройки / анкета). Отдельного выбора языка в боте нет:
  одна настройка, без callback-кнопок (они требовали бы изменения webhook — infra).
- `PlatformUser.PreferredLanguage` (`"ru"` / `"uz"`, nullable; миграция `AddPlatformUserPreferredLanguage`,
  одобрена владельцем). Mini App сообщает язык `PUT /api/users/me/language` после входа и при смене языка;
  `GET /api/users/me` отдаёт `preferredLanguage` (additive).
- Тексты бота — `BotText` (RU + UZ) в `BotMessages`; бот-клиент выбирает язык получателя по chat id
  (= Telegram user id). Язык неизвестен (пользователь не открывал приложение после обновления) или
  поиск не удался — оба языка в одном сообщении. `/start` — так же.
- Backend остаётся language-neutral: язык — данные пользователя, а не discriminator; тексты бота — не DTO.
- Небольшое исключение из D35 (feature freeze), согласовано владельцем перед закрытым запуском.
- Отвергнуто: язык из Telegram `language_code` (не совпадает с выбором в приложении); меню языка в боте (две настройки).

### D40. Безопасность 1: личность из Telegram, без ложного «проверен» — РЕШЕНО (владелец, 2026-10-08)

Найдено негативной проверкой: можно было назваться чужим ником, все анкеты получали «Проверенный профиль»,
огромные числа ломали сохранение.
- **@username** берётся только из подписанных данных Telegram (`initData`, `ContactValidation.TelegramHandle`)
  для блогера, бизнеса и бренд-фейса; поле `Username`/`Telegram` из запроса сервер игнорирует (DTO не меняется).
  Нет ника в Telegram → контакт Telegram не показывается, остаётся телефон.
- **«Проверенный профиль»**: автоодобрение анкеты больше не ставит `IsVerified` (`Approve(verified: false)`);
  значок в приложении не показывается, пока нет настоящей проверки (владение площадкой). Ручная модерация
  админом по-прежнему может отметить профиль проверенным. Данные в базе не меняются (без backfill).
- **Пределы чисел** (`InputLimits`): цены и бюджеты ≤ 1 млрд сум, подписчики ≤ 500 млн, охват ≤ 1 млрд;
  в формах не больше 9 цифр. Цены в анкете блогера больше не заполнены образцами.
- **Телефон**: нажатие открывает выбор «Позвонить / Написать в Telegram (t.me/+номер) / Скопировать»:
  Telegram на iPhone часто блокирует звонок из Mini App.
- Отложено (нужна миграция, отдельное разрешение): телефон только через «Поделиться номером» Telegram.

### D41. Телефон только из Telegram — РЕШЕНО (владелец, 2026-10-08)

Телефон в профиле нельзя ввести вручную: только номер, который Telegram передал боту от самого владельца.
- Миграция `AddPlatformUserVerifiedPhone` (одобрена владельцем): `platform_users.VerifiedPhone` (varchar 32)
  и `PhoneVerifiedAtUtc`, nullable, только добавление, без backfill.
- Источник: сообщение с `contact` в webhook бота (защищён секретом Telegram), принимается только когда
  `contact.user_id == from.id`; пересланная чужая карточка отклоняется. Номер нормализуется: Узбекистан как
  `+998 88 197 29 29`, другие страны принимаются как `+<цифры>`.
- Первый вход и все без номера: обязательный экран «Подтвердите номер» (`WebApp.requestContact`) до выбора роли.
  Сменить номер: команда `/phone` в чате бота (кнопка `request_contact`); новый номер сразу ставится во все
  профили пользователя.
- Анкеты блогера и бизнеса: телефон только показывается; сервер берёт его из `VerifiedPhone`, значение формы
  игнорируется; без подтверждённого номера сохранение → 409 `phone_not_verified`.
- DTO additive: `CurrentPlatformUserDto.VerifiedPhone`.

### D42. Срок входа, картинки профиля, ссылки — РЕШЕНО (владелец, 2026-10-08)

- `Telegram:MaxInitDataAgeSeconds` в `appsettings.json`: 3600 → 86400 (24 часа), с разрешения владельца:
  открытое надолго приложение не должно внезапно терять доступ. На истёкший вход (401) приложение
  показывает «Сессия устарела, откройте заново». Если в Railway задана переменная
  `Telegram__MaxInitDataAgeSeconds`, она главнее файла.
- Аватар, обложка блогера, логотип бизнеса и фото бренд-фейса меняются только загрузкой через profile media;
  ссылка, присланная с анкетой, сервером игнорируется (нельзя подставить чужую внешнюю картинку).
- Ссылки с данными входа (`https://bank.uz@evil.com`, `user:pass@`) отклоняются и сервером, и приложением.

### D43. Удалённый партнёр: сделки и отзывы остаются — РЕШЕНО (владелец, 2026-10-09, пересматривает D27)

Тест показал: бизнес удалил аккаунт, и у блогера пропали сделка и отзывы, то есть его репутация.
- Сделка остаётся у участника, который не удалял аккаунт (`DealParticipantFilter.Visible`: проверяется
  только своя сторона). Имя, фото и ссылка на профиль удалённого партнёра не отдаются; additive
  `counterpartyDeleted: true`, `counterpartyName` пустой, `counterpartyProfileId` null.
- Любые действия по такой сделке закрыты: `canComplete`/`canReview` = false; Complete, Review и контакты
  по-прежнему идут через строгий фильтр (`DealParticipantFilter.For`, обе стороны существуют) → 404.
- Отзыв удалённого автора остаётся в профиле получателя и в рейтинге; автор отдаётся как
  `reviewerDeleted: true` без имени, фото и ссылки. Счётчик завершённых сделок бизнеса их тоже учитывает.
- Приложение показывает «Аккаунт удалён» и поясняет, что связаться и завершить сделку уже нельзя.
- Без миграции; DTO-изменения только добавляют поля.

### D44. Бренд-фейсы до полной поддержки: честный «скоро» — ЧАСТИЧНО ЗАМЕНЕНО D46 (отклики есть; «скоро» осталось только для офферов)

D19/D34 в силе: отклики, офферы и сделки бренд-фейсов — отдельная фаза после запуска. До неё бренд-фейс
на странице кампании видит «отклики появятся совсем скоро» вместо совета сменить роль, а бизнес на
странице бренд-фейса — «предложения скоро, пока напишите по контактам». Без изменений сервера.

### D45. Номер партнёра: карточка контакта в чат вместо звонка — РЕШЕНО (владелец, 2026-10-09)

Telegram на iPhone не даёт Mini App начать звонок (`tel:` блокируется даже по нажатию), поэтому кнопки
«Позвонить» нет. В сделке номер партнёра можно отправить себе: `POST /api/deals/me/{id}/contact/share`
(та же проверка доступа, что у контактов сделки: выбранная роль, обе стороны существуют, иначе 404) →
бот присылает `sendContact` (имя партнёра + «(BloggerBazar)») в чат пользователя с ботом. Нет номера →
409 `contact_phone_missing`; бот не может написать (не нажат «Старт» или бот заблокирован) →
409 `contact_share_failed`. Без миграции.

### D46. Бренд-фейс участвует в откликах, сделках и отзывах как блогер — РЕШЕНО (владелец, 2026-10-09)

Причина: без откликов бренд-фейсы остаются без рекламы («станет тухло»). Владелец: одинаковые возможности
с блогером; кампании не размечаются «для кого», бизнес сам принимает или отклоняет; модерации бренд-фейса нет.

Модель: «сторона создателя» = блогер **или** бренд-фейс. В `campaign_applications`, `collaboration_requests`
и `deals` `BloggerId` стал nullable, добавлен nullable `BrandFaceId` (FK на `brand_face_profiles`, те же
правила удаления, что у блогера), и **CHECK `(BloggerId IS NULL) <> (BrandFaceId IS NULL)`**: ровно один
создатель. Это и снимает опасение D19 («nullable — shortcut»): база не допускает ни пустой, ни двойной стороны.
Уникальность как у блогера: `(CampaignId, BrandFaceId)` для откликов, активный оффер `(BusinessId, BrandFaceId)`.
`reviews.BrandFaceId` + `ReviewTargetType.BrandFace = 2`. Миграция `AddBrandFaceCreatorParticipation`
(только additive, без backfill: старые строки остаются блогерскими).

Анкета бренд-фейса (Q17): Instagram обязателен — бизнес смотрит там фото до оффера; отдельного поля «Опыт»
в форме нет (старый текст переносится в «О себе» при сохранении, колонка остаётся для совместимости).

Доступ: выбранная роль `BrandFace` + собственный профиль бренд-фейса (D-правило selected-role), lookup сразу
scoped по `BrandFaceId`, чужое = 404. Для блогера по-прежнему нужен одобренный профиль, для бренд-фейса —
просто существующий. Номер бренд-фейса для контактов сделки — номер, подтверждённый Telegram для аккаунта.

API (additive, D26): `creatorRole` (`blogger`/`brandFace`) и `brandFaceId` во входящих откликах;
`counterpartyRole` (`business`/`blogger`/`brandFace`) в сделке и в старом списке откликов;
`reviewerRole` в отзывах; `GET /api/brand-faces/{id}/reviews` (рейтинг, количество, страница).
Поля `blogger*` во входящих откликах несут создателя любого вида — старый frontend продолжает работать.

Этапы: этот пакет — отклики, сделки, завершение, отзывы, контакты. Офферы бизнеса бренд-фейсу (BF-3) —
следующим пакетом; до него на странице бренд-фейса остаётся «предложения скоро» (D44).
**Отвергнуто:** объединить профили блогера и бренд-фейса (разные анкеты и каталоги); отдельные таблицы
откликов/сделок для бренд-фейса (дублирование всего цикла и чтения).

### D47. Живой бренд-фейс: что анкета требует и что видит бизнес — РЕШЕНО (владелец, 2026-10-09)

Бизнес выбирает бренд-фейса глазами и по условиям. Анкета требует: Instagram (Q17), пол (`female`/`male`),
возраст 16–80, хотя бы один формат (`photoShoot`, `video`, `ugc`, `event`, `ambassador`; строки D26).
Необязательно: до 4 фото помимо аватара, ссылка на видео-визитку (https), цена, «О себе», портфолио.

Хранение: фото лежат в R2 (тот же конвейер, что у аватара: webp, 512 px, ~50–100 КБ), в базе только URL
(`brand_face_profiles.PhotoUrls text[]`); видео не загружаем, только ссылка (`ShowreelUrl`). Миграция
`AddBrandFacePresentation`: `Formats`, `PhotoUrls` (text[], пусто по умолчанию), `ShowreelUrl` — additive.
Галерея: `POST/DELETE /api/profile-media/brand-face/photos`, лимит 4 → 409 `photo_limit`; удаляется из
хранилища только URL из своей галереи. Каталог: фильтры `gender`, `minAge`/`maxAge`; в карточке пол и возраст.
Старые анкеты без новых полей видны как раньше; при следующем сохранении их нужно заполнить, в профиле
плашка «Дополнить анкету».
**Отвергнуто:** загрузка видео к нам (дорого, тяжело); отдельная таблица фото (лишняя сложность для 4 URL);
рост/размер одежды и эксклюзивность (позже, если бизнес попросит).

### D48. Офферы бизнеса бренд-фейсу — РЕШЕНО (владелец, 2026-10-09)

Бизнес отправляет бренд-фейсу оффер так же, как блогеру (`POST /api/offers` с `brandFaceId` вместо `bloggerId`,
ровно один получатель). Форматы — свои: `photoShoot`, `video`, `ugc`, `event`, `ambassador` (новые значения
`CollaborationFormat` 4–8, колонка int, миграции нет); блогерские форматы бренд-фейсу и наоборот — 422.
Те же правила: дневной лимит, один активный оффер на пару (уникальный индекс из D46), 48 часов, принять/отклонить
может только выбранная роль «Бренд-фейс» со своей анкетой, иначе 404. Принятие открывает сделку с `BrandFaceId`.
`OfferDto` additive: `brandFaceId`, `counterpartyRole`. Надпись «скоро» (D44) убрана.

### D49. Языки — только из списка — РЕШЕНО (владелец, 2026-10-09)

Языки анкеты (сейчас только у бренд-фейса) выбираются из фиксированного списка ISO-кодов (uz, ru, en, kaa, tg, kk,
ky, tk, tr, fa, ar, zh, ko, ja, de, fr, es, it, hi) с поиском, до 5, без ручного ввода: иначе одно и то же пишут
по-разному и с ошибками. Сервер принимает только коды из списка (422). Старые записи свободным текстом остаются
до редактирования: известные написания переводятся в коды, остальное отбрасывается при сохранении. Названия — i18n.

### D50. Пауза профиля роли — РЕШЕНО (владелец, 2026-10-09)

Каждая роль (блогер, бизнес, бренд-фейс) может поставить свой профиль на паузу и вернуть его
(`PUT /api/users/me/roles/{blogger|brand-face|business}/visibility`, тело `{ "hidden": true|false }`).
Флаг `IsHidden` у профиля роли (миграция `AddProfileVisibility`, bool, по умолчанию false — additive).
Скрытый профиль: пропадает из каталогов и главной (кампании скрытого бизнеса тоже); не получает новых
офферов (скрытый получатель читается как отсутствующий — 404) и не может сам откликаться или отправлять
офферы (409 `profile_hidden`); на кампанию скрытого бизнеса откликнуться нельзя (409 `business_hidden`).
Страница по прямой ссылке остаётся с пометкой «на паузе», текущие сделки и отзывы не трогаются.
DTO additive: `isHidden` у профилей, `businessHidden` у кампании.
Шапка профиля показывает аватар и имя выбранной роли (Telegram — только если у роли нет профиля);
значок «Активен» остаётся, у скрытого профиля — «Скрыт». Удаление роли/профиля — не входит.
**Отвергнуто:** удаление профиля (теряются сделки и отзывы); общий флаг на пользователя (паузу хотят по роли).

### D51. Данные сделки, лёгкая версия — РЕШЕНО (владелец, 2026-10-10)

Сначала данные сделки, потом аналитика; рабочие пространства (команда бизнеса) — только после подтверждения
спроса (аудит этапа 0 и модель — в roadmap). Всё необязательное: сделка не ждёт бумажной работы.
**Цена:** у сделки из оффера цена = бюджет оффера (уже согласован, менять нельзя: 409 `price_fixed_by_offer`);
иначе бизнес указывает/меняет `AgreedPrice` (`PUT /api/deals/me/{id}/price`).
**Публикации:** блогер/бренд-фейс добавляет до 5 ссылок https на рекламу с просмотрами (необязательно)
(`POST/PUT/DELETE /api/deals/me/{id}/publications`); бизнес нажимает «Подтвердить»
(`.../{publicationId}/confirm`). Новые просмотры снимают подтверждение; подтверждённую ссылку удалить нельзя;
одна ссылка в сделке один раз (уникальный индекс, 409 `publication_duplicate`, `publication_limit`,
`publication_confirmed`). Бизнесу приходит сообщение бота о новой ссылке. Результаты доступны в активной и
завершённой сделке, но не в отменённой и не с удалённым партнёром. Чужая сделка или ссылка = 404.
**UX:** поля «ссылка» (исполнителю) и «цена» (бизнесу) спрашиваются прямо в окне «Завершить сделку», их можно
пропустить; блок «Результат» на странице сделки. Данные привязаны к сделке (`BusinessId`), автор действия
хранится как Telegram id, поэтому переход на рабочие пространства их не ломает.
Миграция `AddDealResults`: `deals.AgreedPrice`, `AgreedPriceSetAtUtc`, таблица `deal_publications` — additive,
старые сделки без цены (ничего не выдумываем). DTO additive: `agreedPrice`, `canSetPrice`, `publications`,
`canAddPublication`, `canConfirmPublications`.
**Не входит:** скриншоты (нужно закрытое хранилище), CPM и графики, проверка просмотров через API площадок,
согласование изменения цены второй стороной.
