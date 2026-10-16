# BloggerBazar — текущий статус

Волатильный файл: обновляется при каждой публикации/checkpoint. Исторические результаты gates
**не являются** результатами будущих изменений.

_Последнее обновление: 2026-10-16, сайт-админка опубликована до `14c38f2`; в review: цена обязательна при завершении сделки бизнесом (D51, изменение 2026-10-16)._

## Checkpoint

| | |
|---|---|
| Published `main` / `origin/main` | `14c38f2` логотип, красная кнопка, фильтры модерации (до этого `c4e6ad5` фаза 4, `ef05ef3` доработка фазы 3, `991cd4d` фаза 3, `a7d9e15` фаза 2, `ca9b16d` фаза 1, `0a37df8` Б4 CSP report-only, `880c553` D56, `e6dd11d`, `14db7da`, `0f25c80`, `2f30ca9`, `88b664f`, `450bf55`, `5250b46`, `07f7b23`, `11d672d`, `b849656`, `4b22bf8`, `17a8c07`, `5de31b5`, `e8363e0` D52 жалобы + очистка оплаты, `f0cd8bb` D51, `46b6320` D50, `aa5c4dd` D47–D49, `3d8d1c8` D46, `92e7ec6`…`2db7833` RC QA, безопасность 1–2, D42–D45; ранее `c9e5e9e` MVP QA, раунд 4; предыдущие checkpoints `0b8aed0` D38, `f82d387` QA3, `eb82bb4` QA2+D37, `8478f84` D36, `66c2e1f` QA1, `e8a6ca1` R7, `755e847` R6, `a4385ef` R5, `3d00671` R4, `d45bf43` R3, `323825a` R2b, `abf79d6` R2a, `bd46784` R1, `9f0e632` 4C, `16603e9` 4A+4B, `b26abeb` 3F-C, `a17819b` 3F-B) |
| Рабочая ветка Claude | `claude/new-session-di2yul` (сайт-админка; прежняя `claude/pensive-fermat-rp0p5w` сохраняется) |
| CI PR для gates | 3F-B: …/pull/1 · 3F-C: …/pull/2 · 4A+4B: https://github.com/kralur/blogerbazar/pull/3 · 4C: https://github.com/kralur/blogerbazar/pull/4 · R1: https://github.com/kralur/blogerbazar/pull/5 · R2a: https://github.com/kralur/blogerbazar/pull/6 · R2b: https://github.com/kralur/blogerbazar/pull/7 · R3: https://github.com/kralur/blogerbazar/pull/8 · R4: https://github.com/kralur/blogerbazar/pull/9 · R5: https://github.com/kralur/blogerbazar/pull/10 |

## Текущая фаза

- **Phase 3F-B — Private Deal API, Security & Lifecycle**: опубликована (`a17819b`).
- **Phase 3F-C — frontend Deal bridge**: опубликована (`579d6c3`).
- **Phase 4A — Contacts privacy** (D31): опубликована (`75aa8e3`, fix `b3d4dcd`, tests `fb9ac1c`).
- **Phase 4B — Offers**: опубликована (`29516c3`). Миграция `AddCollaborationOfferTerms` (одобрена владельцем)
  применяется при старте только если `Database__ApplyMigrationsOnStartup=true` в Railway — проверить (8.19).
- **Phase 4C — Reviews 2.0 + напоминания** (D33): опубликована (`fd2054d`). Миграция
  `AddReviewPublicationAndDealReminders` одобрена владельцем; в Railway тоже нужен `ApplyMigrationsOnStartup` (8.19).
- **FEATURE FREEZE** (D35). **Redesign R1 — основа**: опубликован (`945c7f6`). **R2a — главная**: опубликована (`6d965bd`). **R2b — каталоги и карточки**: опубликованы (`7c0c6a9`). **R3 — детальные страницы**: опубликованы (`ba3bfa8`). **R4 — заявки и сделки**: опубликованы (`8764b6e`). **R5 — кабинет бизнеса**: опубликован (`a4385ef`). **R6 — профиль и онбординг**: опубликован (`755e847`). **R7 — QA**: опубликован (`e8a6ca1`). **MVP QA, раунд 1 (iOS, тёмная тема)**: опубликован (`66c2e1f`). **Срок кампании и коды конфликтов (D36)**: опубликован (`8478f84`). **MVP QA, раунд 2** + D37: опубликован (`eb82bb4`). **MVP QA, раунд 3**: опубликован (`f82d387`). **D38 — экран «Настройки» и выбор темы**: опубликован (`0b8aed0`). **MVP QA, раунд 4** (одна кнопка фото: заменить/удалить через меню): опубликован (`c9e5e9e`).
- **MVP Release Candidate QA**: в работе — чек-лист `docs/rc-qa.md`, прогон на устройствах владельцем.
- **RC QA, правка 1** (запрос разрешения боту писать пользователю; удаление неиспользуемого кода и 146 ключей i18n): опубликована (`c093266`).
- **RC QA, правка 2** (тексты бота RU + UZ, приветствие `/start`, кнопки «Открыть» в уведомлениях, `docs/telegram-bot.md`): опубликована (`5c5c37d`).
- **D39 — язык сообщений бота** (миграция `AddPlatformUserPreferredLanguage`, одобрена владельцем): опубликован (`c682d96`).
- **RC QA, правка 3** (опубликована, `a39f8ce`): бот не отправлял ни одного сообщения — адрес Bot API собирался строкой
  `bot<токен>/метод`, а двоеточие в токене .NET читал как схему URI (`NotSupportedException`, лог Railway
  2026-10-07); то же в платёжном шлюзе. Webhook отвечал 500 и блокировал очередь Telegram. Плюс: формат суммы
  в оффере и фильтрах бренд-фейсов, поле даты на iOS, обязательное «Сообщение» в оффере.
  В Railway нужен `ApplyMigrationsOnStartup` (8.19).
- **RC QA 8–9 окт** (опубликовано, `2db7833`…`92e7ec6`): тексты без тире, безопасность 1–2 (D40, D41), D42–D45,
  удалённый партнёр (D43), смена роли по ссылке из бота.
- **Бренд-фейсы D46–D49** (опубликовано, `3d8d1c8`, `aa5c4dd`): отклики, офферы, сделки, отзывы, живая анкета,
  галерея, языки из списка. CI зелёный (458 тестов с integration).
- **D50 — пауза профиля роли** (опубликовано, `46b6320`, миграция `AddProfileVisibility`): CI зелёный.
- **D51 — данные сделки, лёгкая версия** (опубликовано, `f0cd8bb`, CI зелёный): цена сделки, ссылки на публикации с
  просмотрами и подтверждением бизнеса, поля в окне завершения. Миграция `AddDealResults` (одобрена владельцем
  вместе с этапом). Плюс: правило 1 в `GetMyCampaignApplications`, контакты бизнеса для бренд-фейса со сделкой.
- **Очистка оплаты контактов + D52 жалобы** (опубликовано, `e8363e0`, CI зелёный): удалён мёртвый код оплаты (webhook бота
  остался для `/start`, `/phone`, контакта); жалобы всех ролей с очередью в админке, миграция `AddComplaints`;
  исправлен дашборд админки (параллельные запросы на одном DbContext).
- **D53 — «Подходят вам» на главной**: опубликовано, `GET /api/marketplace/for-me`, без миграции. Админка 2
  (плитки, поиск и фильтры пользователей, «Новые», «Написать в Telegram»): опубликована (`11d672d`).
- **Жалобы: действия + напоминание о результатах**: опубликовано (`07f7b23`, без миграции): в карточке жалобы «Открыть» (профиль или
  кампания) и «Заблокировать пользователя», фильтр Открытые/Решённые/Отклонённые; через 2 дня после завершения сделки
  без ссылки на публикацию бот один раз просит исполнителя добавить её (`DealReminderKind.ResultsDay2`).
- **D54 — советы от бота**: опубликовано (`5250b46`): пн/ср/пт одна подсказка по роли без повторов, отключение в Настройках;
  миграция `AddPlatformUserBotTips` (одобрена владельцем); кнопки бота открывают экраны из белого списка.
- **Безопасность 3**: опубликовано (`450bf55`, без миграции): заголовки защиты в ответах API, проверка уязвимых прод-пакетов
  в CI, Dependabot. Дальнейшие этапы Б4–Б6 — в `docs/roadmap.md`, раздел «Безопасность».
- **Мелочи QA + часть Б4 + админка 3** (в работе, не закоммичено, без миграции): карточка жалобы не вылезает за экран;
  лимит 30 изменений в минуту на пользователя и понятное сообщение при 429; инструкция при утечке токена бота;
  «Прислать мне подсказку» в админке; карточка пользователя (`GET /api/admin/users/{id}`); напоминание о ссылке не
  занимает выборку раньше 2-го дня; правки UZ-текстов (апострофы, дни недели, три формулировки).
- **Аудит бэкенда** (в review, без миграции): удалены legacy `/api/collaboration-requests/*` и
  `PATCH /api/campaign-applications/{id}/status`; условное решение по отклику (гонка «принять/отозвать»);
  отклонённую админом кампанию нельзя вернуть; роль Owner не выдаётся через API; кампанию создаёт только роль
  Business; legacy Accept → 404 на чужой отклик; смена статуса кампании в журнале. Итоги — `docs/roadmap.md`,
  «Безопасность»; открытый вопрос 9.9 (Telegram бренд-фейса). Интеграционные тесты впервые прогнаны локально
  (Docker в сессии): 526/526.
- **Проверка экранов на телефоне** (в review, без миграции): Playwright-скрипт в сессии (в репозиторий не добавлен,
  roadmap 8.22) открыл ~26 экранов на роль в 360 и 320 px, RU/UZ, обе темы. Исправлено: 500 в фильтре каталога по
  дедлайну (дата без часового пояса; то же в дедлайне оффера); кнопки админки и шапки страниц на узком экране;
  факты профиля в один столбец на 320 px; бледные цвета: бейджи статусов (зелёный, жёлтый, красный), красные кнопки
  и кружок счётчика в тёмной теме, тосты, кнопка «Пожаловаться» и счётчик жалоб в светлой теме, лаймовая ссылка
  на публикацию. Новые токены `--bb-*-on-subtle` и `--bb-*-fill` (`docs/DESIGN_SYSTEM_V2.md`).
- **Воронка в админке (D55)** (в review, без миграции): `GET /api/admin/funnel?period=7d|30d|all`, экран
  `#/admin/funnel` по ролям: от первого входа до отзыва, доля от открывших и от прошлого шага, отмечен шаг
  с наибольшей потерей.
- Пакет «аудит + проверка экранов + воронка» опубликован (`88b664f`, CI PR #62 зелёный).
- **Telegram бренд-фейса по D31**: опубликовано (`2f30ca9`, без миграции): убран из публичных данных; Telegram и телефон — только
  бизнесу со сделкой (`GET /api/contacts/BrandFace/{id}`, roadmap 9.9).
- **Проверка экранов в CI** (в review): `frontend/e2e/screen-check.mjs` + job «Phone screen check» (Postgres-сервис,
  API с development seed, Vite, Chromium); dev-зависимость `@playwright/test` 1.56.1 (разрешение владельца 2026-10-14);
  звезда рейтинга `aria-hidden` (декоративная, число рядом). Локальный прогон как в CI: 432 экрана, 0 проблем.
- **Срок входа 6 часов**: опубликовано (`14db7da`, решение владельца 2026-10-14, D42): `Telegram:MaxInitDataAgeSeconds` 86400 → 21600.
  Если в Railway задана переменная `Telegram__MaxInitDataAgeSeconds`, она главнее файла: проверить и убрать/поменять.
- **Баги владельца 15 окт и скорость**: опубликовано (`e6dd11d`, без миграции): «Настройки» и админка рисуются сразу из уже
  загруженных данных (Q51, Q52; при выходе забываются); быстрый старт: вернувшийся пользователь сразу видит
  экраны своей роли, проверка аккаунта идёт в фоне (403 → экран входа, нет телефона → экран телефона), Q53;
  `frontend/nginx.conf`: gzip (главный скрипт 357 → 103 КБ) и `Cache-Control` (assets на год, страница no-cache).
  Сервер в US West (California); переезд в EU West владелец отложил (2026-10-15). Плитки «Блогеры»/«Компании» без удалённых
  профилей; в жалобе на профиль владельца нет «Заблокировать» (additive `ownerIsPlatformOwner`), Q54.
- **D56 счётчики**: опубликовано (`880c553`, миграция `AddPlatformUserActivity` одобрена владельцем): последний вход (не чаще раза
  в час) и метка рекламной ссылки `?start=<метка>`; админка: «Заходили в приложение», «Откуда пришли». Сайт-админка
  для полной аналитики записана в roadmap (после запуска).
- **Б4 защита страниц** (опубликовано, `0a37df8`, без миграции): CSP в режиме Report-Only в `frontend/nginx.conf` (+`nosniff`,
  `Referrer-Policy`), приложение шлёт нарушения на `POST /api/csp-reports`, админка → «Защита страниц»; тест держит
  хеш встроенного скрипта в шаге с `index.html`. Строгий режим — после недели без нарушений (Q56).
- **Рабочие пространства бизнеса**: аудит этапа 0 сделан (roadmap, п. 8), реализация ждёт подтверждения спроса.
- **Сайт-админка, мелкие правки** (в review, только сайт): логотип ведёт на «Обзор», «Выйти на всех устройствах»
  красным; поиск и фильтры в модерации блогеров (статус, город, категория), компаний (статус, город, видимость) и
  кампаний (статус, продвижение).
- **Mini App: «карманная» админка (D57)** (review, без миграции): в Mini App остались обзор с цифрами, жалобы, модерация
  блогеров и бизнесов, карточка пользователя с блокировкой и подсказки бота. Убраны списки пользователей и кампаний,
  источники, воронка, журнал и безопасность (они на сайте). Новая кнопка «Полная админка» открывает сайт в браузере:
  адрес приходит в additive поле `adminWebUrl` в `GET /api/admin/dashboard` (из `AdminWeb:Origin`, `null` пока сайт выключен).
  Серверные `/api/admin/*` и `shared/` не менялись.
- **Сайт-админка, фаза 4: выгрузки CSV** (опубликовано, `c4e6ad5`, CI зелёный, без миграции): «Скачать CSV» на списках и отчётах, запись в журнал
  до скачивания (`POST /api/admin-web/exports`), пометка персональных данных; `scripts/check-admin-web-deploy.mjs` для
  проверки сайта после деплоя: проверка проведена, лимит входа и подделка X-Forwarded-For подтверждены на бою.
- **Сайт-админка, доработка фазы 3** (опубликовано, `ef05ef3`, CI зелёный, без миграции): страница разбора жалобы с историей обоих
  (`GET /api/admin/complaints/{id}/review`), журнал с периодом и поиском (`audit-logs?from&to`), сортировка и фильтр
  активности в «Пользователях».
- **Сайт-админка, фаза 3: списки и действия (D57)** (опубликовано, `991cd4d`, CI зелёный, без миграции): страницы пользователей с карточкой,
  модерации, жалоб, сделок, журнала и защиты страниц; подтверждение перед каждым изменением; `GET /api/admin/deals`,
  `POST /api/admin-web/logout-all`; модерация анкет пишется в журнал; слова ролей, статусов и журнала в `shared/admin`.
- **Сайт-админка, фаза 2: обзор и графики (D57)** (опубликовано, `a7d9e15`, CI зелёный, без миграции): `GET /api/admin/analytics/daily`, `money`,
  `geography`, `retention`, `sources` (группировка в SQL, дни по Ташкенту); страницы «Обзор», «Аналитика», «Воронка» с
  графиками Recharts и таблицами; города, категории и тексты воронки вынесены в `shared/` (Mini App выглядит так же).
- **Сайт-админка, фаза 1: вход (D57)** (опубликовано, `ca9b16d`, CI зелёный, в Railway работает, без миграции): `POST/GET /api/admin-web/session`,
  `POST /api/admin-web/logout`, `GET /api/admin-web/config`; вход Telegram Login Widget, cookie `__Host-bb_admin` и сессия
  в Redis (8 ч, час простоя); все `/api/admin/*` принимают `tma` или cookie (проверка Origin на изменениях); каркас
  `admin-web/` (вход, меню, пустой «Обзор», RU/UZ, темы), общий `shared/` (токены DS v2, пресет Tailwind, ядро i18n),
  Dockerfile и nginx со строгим CSP, job в CI. Включается переменной `AdminWeb__Origin`; шаги владельца —
  `docs/admin-web.md`. Попутно: проверка i18n ловит дубли ключей и в строках с несколькими ключами (убраны два
  перекрытых дубля `form.success*` в словарях Mini App, тексты не изменились).

## Последние результаты gates

### Сайт-админка, фаза 4 (локально, до review, 2026-10-16)

```text
dotnet build (чистая): 0 errors, 1 старое предупреждение CS0618; dotnet test: 680 total, 615 passed, 65 skipped
  (integration без Docker, из них 1 новый); было: 672 / 608 / 64
RUN_INTEGRATION_TESTS=true: NOT RUN локально — через CI на draft PR
EF has-pending-model-changes: No changes; npm test: frontend 479 passed, admin-web 67 passed; builds passed;
i18n:audit passed (admin-web 492 keys); npm audit: 0 vulnerabilities
Chromium под CSP из nginx: 99 экранов, 0 проблем; скачивание CSV пользователей и отчёта «По дням» работает (BOM, «;»)
scripts/check-admin-web-deploy.mjs против боевого сайта (2026-10-16): 28 из 29 проверок ok (CSP, HSTS, nosniff,
  no-referrer, noindex, robots, API через сайт, 401 без cookie, 403 с чужого Origin). Пункт про лимит входа показал
  «401 x6» из-за среды Claude: её прокси выходит с разных IP на каждое соединение. Повтор по одному соединению (curl
  --next): 401 x5, затем 429; с разным поддельным X-Forwarded-For в каждом запросе тоже 401 x5, затем 429, значит
  Railway не пропускает чужой заголовок и лимит не обходится (вопрос из D57 закрыт).
git diff --check: clean; secret scan: чисто
```

### Сайт-админка, доработка фазы 3 (локально, до review, 2026-10-16)

```text
dotnet build (чистая): 0 errors, 1 старое предупреждение CS0618; dotnet test: 672 total, 608 passed, 64 skipped
  (integration без Docker, из них 2 новых); было: 661 / 599 / 62
RUN_INTEGRATION_TESTS=true: NOT RUN локально — через CI на draft PR; разбор жалобы, 404 и журнал по дням с 422
  проверены на локальном PostgreSQL через API
EF has-pending-model-changes: No changes; npm test: frontend 479 passed, admin-web 62 passed; builds passed;
i18n:audit passed (admin-web 466 keys); npm audit: 0 vulnerabilities; Chromium под CSP: 97 экранов, 0 проблем;
git diff --check: clean; secret scan: чисто
```

### Сайт-админка, фаза 3 (локально, до review, 2026-10-16)

```text
dotnet build (чистая сборка): passed, 0 errors, 1 старое предупреждение CS0618 (ImageOrientation)
  попутно убрано предупреждение EF1002 из фазы 2 (AdminAnalyticsReadModel.GeoRowsAsync: текст запроса только из кода);
  в отчёте фазы 2 было «0 warnings» по инкрементальной сборке, которая его не показала
dotnet test: 661 total, 599 passed, 62 skipped (integration без Docker, из них 4 новых)
  было до фазы: 635 total, 577 passed, 58 skipped
RUN_INTEGRATION_TESTS=true: NOT RUN локально (нет Docker daemon) — через CI на draft PR; новые эндпоинты проверены на
  локальном PostgreSQL через API: список сделок и 422, модерация в журнале, выход на всех устройствах (второй браузер 401)
EF has-pending-model-changes: No changes have been made to the model since the last migration
dotnet list package --vulnerable (Api): нет уязвимых пакетов
npm test: frontend 74 files, 479 passed (один раз упал старый тест про устаревший ответ кампании, 3 повтора зелёные,
  на исходном коде тоже зелёный); admin-web 8 files, 58 passed
npm run build: frontend и admin-web passed; npm run i18n:audit: passed (frontend 1152 keys, admin-web 412 keys)
npm audit --omit=dev --audit-level=high: 0 vulnerabilities
admin-web в Chromium под CSP из nginx: 91 экран (все страницы, 1280/1024/360/320, RU/UZ, светлая/тёмная, окна
  подтверждения на телефоне): 0 нарушений CSP, 0 ошибок, без прокрутки вбок
Сквозная проверка на локальном API: вход, блокировка и разблокировка с подтверждением, все разделы, журнал, выход везде
git diff --check: clean; secret scan изменённых файлов: чисто
```

### Сайт-админка, фаза 2 (локально, до review, 2026-10-16)

```text
dotnet build: passed (0 warnings, 0 errors); dotnet test: 635 total, 577 passed, 58 skipped (integration без Docker, из них 5 новых)
  было до фазы: 621 total, 568 passed, 53 skipped
RUN_INTEGRATION_TESTS=true: NOT RUN локально (нет Docker daemon) — через CI на draft PR; SQL отчётов проверен на
  локальном PostgreSQL через API (граница дня по Ташкенту, деньги, возвращаемость, источники, 422 на неверный период)
EF has-pending-model-changes: No changes have been made to the model since the last migration
dotnet list package --vulnerable (Api): нет уязвимых пакетов
npm test: frontend 74 files, 479 passed (как до фазы); admin-web 7 files, 41 passed
npm run build: frontend и admin-web passed; npm run i18n:audit: passed (frontend 1145 keys, admin-web 195 keys)
npm audit --omit=dev --audit-level=high: 0 vulnerabilities (Recharts 3.10.1 добавил 37 пакетов)
admin-web в Chromium под CSP из nginx: 34 экрана (вход, обзор, аналитика, воронка; 1280/1024/360/320; RU/UZ;
  светлая/тёмная; таблицы; подсказка графика; свои даты): 0 нарушений CSP, 0 ошибок, без прокрутки вбок
Вход и страницы на локальном API через прокси Vite: вход по подписанным данным виджета, все отчёты загрузились
git diff --check: clean; secret scan изменённых файлов: чисто
```

### Сайт-админка, фаза 1 (локально, до review, 2026-10-15)

```text
dotnet build: passed (0 errors, 1 старое предупреждение CS0618 в ImageOrientation); dotnet test: 621 total, 568 passed, 53 skipped (integration без Docker, из них 7 новых)
  было до фазы: 559 total, 513 passed, 46 skipped
RUN_INTEGRATION_TESTS=true: NOT RUN локально (нет Docker daemon) — через CI на draft PR
EF has-pending-model-changes: No changes have been made to the model since the last migration
dotnet list package --vulnerable (src): нет уязвимых пакетов
npm test: frontend 74 files, 479 passed (как до фазы); admin-web 5 files, 26 passed
npm run build: frontend и admin-web passed; npm run i18n:audit: passed (frontend 1145 keys, admin-web 47 keys)
npm audit --omit=dev --audit-level=high: 0 vulnerabilities
Mini App CSS до/после выноса токенов: все правила и 67 токенов совпадают (меняется только группировка)
Сборка образов (симуляция шагов Dockerfile без Docker): frontend и admin-web — npm ci + build passed;
  без правки frontend/Dockerfile сборка Mini App падала бы (нет shared/)
admin-web в Chromium под CSP из nginx: 19 экранов (1280/1024/360/320, RU/UZ, светлая/тёмная, меню, возврат из
  Telegram): 0 нарушений CSP, 0 горизонтальной прокрутки, контраст текста ≥ 4.5:1
nginx -t: NOT RUN локально (нет nginx) — шаг в CI
git diff --check: clean; secret scan изменённых файлов: только заведомо тестовые токены вида 123456:...
```

### Мелочи QA + Б4 + админка 3 + аудит бэкенда + проверка экранов + воронка (локально, до review, 2026-10-12)

```text
dotnet build: passed (0 errors); dotnet test: 539 total, 495 passed, 44 skipped (integration без env)
RUN_INTEGRATION_TESTS=true dotnet test (Docker в сессии): 539/539 passed
EF has-pending-model-changes: No changes have been made to the model since the last migration
npm test: 73 files, 468 passed / 0 failed; npm run build: passed; i18n:audit: passed (1134 keys)
Проверка экранов (Playwright, вне репозитория): 212 экранов × 360 и 320 px, 0 проблем вёрстки;
  бледным осталось только ★ рейтинга (жёлтая звезда рядом с числом, оставлено)
git diff --check: clean; secret scan изменённых файлов: clean
```

### Redesign R5 (GitHub Actions, run 37434347086, коммит `a4385ef`)

```text
Backend build and tests: passed; Frontend build and tests: passed; Production Docker build validation: passed
```

### Redesign R5 (локально, до review)

```text
npm test: 43 files, 321 passed / 0 failed; npm run build: passed; i18n:audit: passed (914 keys); tsc: passed
dotnet: не затронут (frontend-only)
```

### Redesign R4 (GitHub Actions, run 37433056226, коммит `8764b6e`)

```text
Backend build and tests: passed; Frontend build and tests: passed; Production Docker build validation: passed
```

### Redesign R4 (локально, до review)

```text
npm test: 43 files, 319 passed / 0 failed; npm run build: passed; i18n:audit: passed (914 keys); tsc: passed
dotnet: не затронут (frontend-only)
```

### Redesign R3 (GitHub Actions, run 37431956793, коммит `ba3bfa8`)

```text
Backend build and tests: passed; Frontend build and tests: passed; Production Docker build validation: passed
```

### Redesign R3 (локально, до review)

```text
npm test: 43 files, 317 passed / 0 failed; npm run build: passed; i18n:audit: passed (912 keys); tsc: passed
dotnet: не затронут (frontend-only)
```

### Redesign R2b (GitHub Actions, run 37430885286, коммит `7c0c6a9`)

```text
Backend build and tests: passed; Frontend build and tests: passed; Production Docker build validation: passed
```

### Redesign R2b (локально, до review)

```text
npm test: 42 files, 313 passed / 0 failed; npm run build: passed; i18n:audit: passed (907 keys); tsc: passed
dotnet: не затронут (frontend-only)
```

### Redesign R2a (GitHub Actions, run 37428451689, коммит `6d965bd`)

```text
Backend build and tests: passed; Frontend build and tests: passed; Production Docker build validation: passed
```

### Redesign R2a (локально, до review)

```text
npm test: 41 files, 310 passed / 0 failed; npm run build: passed; i18n:audit: passed (906 keys); tsc: passed
dotnet: не затронут (frontend-only)
```

### Redesign R1 (GitHub Actions, run 37427205573, коммит `945c7f6`)

```text
Backend build and tests: passed; Frontend build and tests: passed; Production Docker build validation: passed
```

### Redesign R1 (локально, до review)

```text
npm test: 41 files, 304 passed / 0 failed; npm run build: passed; i18n:audit: passed (879 keys); tsc: passed
dotnet: не затронут (frontend-only)
```

### Phase 4C (GitHub Actions, run 37422835227, коммит `fd2054d`)

```text
dotnet build: passed (0 warnings)
dotnet test (RUN_INTEGRATION_TESTS=true, migrations applied on Postgres): 316 passed / 0 failed / 0 skipped
Frontend build and tests: passed; Production Docker build validation: passed
```

### Phase 4C (локально, до review)

```text
npm test: 41 files, 304 passed / 0 failed; npm run build: passed; i18n:audit: passed (879 keys)
dotnet build / test / EF pending model changes: NOT RUN локально (нет dotnet) — проверка через CI на draft PR
```

### Phase 4A + 4B (GitHub Actions, run 37302835374, коммит `29516c3`)

```text
dotnet build: passed
dotnet test (RUN_INTEGRATION_TESTS=true, migrations applied on Postgres): 286 passed / 0 failed / 0 skipped
Frontend build and tests: passed; Production Docker build validation: passed
```

### Phase 4B (локально, до review)

```text
npm test: 41 files, 299 passed / 0 failed; npm run build: passed; i18n:audit: passed (874 keys); tsc: passed
dotnet build / test / EF pending model changes: NOT RUN локально (нет dotnet) — проверка через CI на PR #3
git diff --check: passed; secret scan: no matches
```

### Phase 4A (локально, до review)

```text
npm test: 40 files, 289 passed / 0 failed; npm run build: passed; i18n:audit: passed (834 keys)
dotnet build / test: NOT RUN локально (нет dotnet) — проверка через CI на draft PR
git diff --check: passed; secret scan: no matches; migration: none
```

### Phase 3F-C (GitHub Actions, run 37296454851, коммит `579d6c3`)

```text
dotnet build: passed
dotnet test (RUN_INTEGRATION_TESTS=true): 255 passed / 0 failed / 0 skipped
Frontend build and tests: passed; Production Docker build validation: passed
```

### Phase 3F-C (локально, до review)

```text
npm test: 40 files, 287 passed / 0 failed (baseline 275)
npm run build: passed
npm run i18n:audit: passed (833 keys)
tsc --noEmit: passed
dotnet build / test: NOT RUN локально (нет dotnet) — проверка через CI на draft PR
git diff --check: passed; secret scan: no matches
```

### Phase 3F-B + CI fix (GitHub Actions, run 37293136432, коммит `7722609`)

```text
dotnet build: passed (0 warnings, 0 errors)
dotnet test (RUN_INTEGRATION_TESTS=true, Testcontainers): 254 passed / 0 failed / 0 skipped
Frontend build and tests (npm test, i18n audit, build): passed
Production Docker build validation: passed
git diff --check: passed; secret scan: no matches; migration: none
```

Важно: до коммита `7722609` CI на `main` **никогда не был зелёным** (все runs #2–#60): тесты с
`WebApplicationFactory` падали, потому что тестовые настройки передавались через `ConfigureAppConfiguration`
и не были видны `Program` до `builder.Build()`. Исправлено через `UseSetting` (только тесты).
Прежние результаты «210 passed / 13 skipped» были локальными прогонами без integration-тестов.

Деплой: Railway автоматически деплоит при push в `main` (подтверждено владельцем). Workflow
`deploy-railway.yml` удалён 2026-10-12 (решение владельца): с 8 окт. он падал на первом шаге без `RAILWAY_TOKEN`.

### Phase 3F-A (перед публикацией `313d26f`, выполнял Codex — историческое)

```text
dotnet build: passed
dotnet test: 210 passed / 0 failed / 13 skipped
git diff --check: passed
EF pending model changes: none
secret scan: passed
```

### Phase 3E-B (`8420d89`, историческое)

```text
Frontend: 39 test files, 275 passed, 0 failed
Backend: 197 passed, 0 failed, 13 skipped
i18n keys: 811
```

### Предыдущий Claude onboarding (историческое, по handoff)

```text
npm test: 275 passed / 0 failed
npm run build: passed
npm run i18n:audit: passed
dotnet: SDK отсутствовал → backend NOT RUN
```

### Phase 0 (эта сессия)

```text
dotnet build: NOT RUN (dotnet отсутствует в среде)
dotnet test:  NOT RUN (dotnet отсутствует в среде)
npm test / build / i18n:audit: NOT RUN (код не менялся; node_modules не установлены)
git diff --check: passed (только docs)
```

### Phase 3F-B (локально, до CI — историческое)

```text
dotnet build: NOT RUN (dotnet отсутствует в среде) — проверка будет через CI на draft PR
dotnet test:  NOT RUN (dotnet отсутствует в среде) — проверка будет через CI на draft PR
EF pending model changes: NOT RUN (модель не менялась, migration нет)
npm test / build / i18n:audit: NOT RUN (frontend не менялся)
git diff --check: passed
```

## Среда Claude Code (cloud)

- `dotnet`: в базовой среде отсутствует. В сессии 2026-10-15 .NET SDK 9.0.319 поставлен с разрешения владельца
  (официальный образ `mcr.microsoft.com/dotnet/sdk:9.0`, только в среде сессии; в apt для Ubuntu 24.04 его нет).
- Docker: CLI есть (29.6.2), daemon недоступен (`/var/run/docker.sock` отсутствует) → Testcontainers не запустятся.
- Node: `/opt/node22` (npm доступен); `node_modules` не установлены.
- CI (`.github/workflows/ci.yml`): backend с `RUN_INTEGRATION_TESTS=true` + frontend на PR/push в `main` — путь получить реальные gates.
- **Тексты без длинного тире**: опубликованы (`2db7833`).
- **RC QA, пакет A** (на review): поворот фото с камеры (EXIF), кнопки роли на «Кампаниях», своя категория,
  склонение количеств (RU), сортировка и кнопки шапки на узком экране, подпись партнёра на предложении,
  причина «нельзя откликнуться», «осталось N» и подсказка на предложении, звонок по номеру, имя партнёра
  и название сделки в сообщениях бота.
- **RC QA, пакеты B–E** (на review, один общий commit с пакетом A):
  - B: «потянуть, чтобы обновить», обновление при возвращении в приложение, кружки «ждёт ответа» (`PendingApplicationsCount`).
  - C: публичная страница бизнеса `GET api/businesses/{id}`, `#/company/:id`, переходы по фото и имени, предложение по нажатию на цену.
  - D: подписчики, охват и ER по каждой площадке (миграция `AddSocialPlatformReachAndEngagement`, одобрена владельцем;
    итоги профиля считаются из площадок, ER взвешен по подписчикам); меню «+» у фото одинаковое для всех ролей.
  - E: меню «⋯» на карточке и странице кампании: `POST api/campaigns/mine/{id}/reopen`,
    `DELETE api/campaigns/mine/{id}` (только без откликов, иначе 409 `campaign_has_applications`).
