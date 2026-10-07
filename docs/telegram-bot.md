# Telegram-бот: тексты и настройка в BotFather

Правило владельца (2026-10-07): в текстах для пользователей (приложение, бот, BotFather) не использовать длинное тире «—».

Настраивает владелец вручную в [@BotFather](https://t.me/BotFather). Код этих настроек не меняет
(CLAUDE.md, правило 5). Всё ниже — в чате с BotFather: `/mybots` → выбрать бота.

## 1. Описание — «Что умеет этот бот?» (до 512 символов)

Видно в пустом чате до нажатия «Start». `Edit Bot` → `Edit Description` (или команда `/setdescription`):

```text
BloggerBazar: площадка, где бизнес находит блогеров для рекламы, а блогеры находят рекламные заказы.
Бот присылает уведомления: новые отклики и предложения, сделки, напоминания об отзывах.

BloggerBazar: biznes reklama uchun blogerlarni, blogerlar esa reklama buyurtmalarini topadigan maydon.
Bot bildirishnomalar yuboradi: yangi javoblar va takliflar, bitimlar, fikrlar haqida eslatmalar.
```

## 2. Краткое описание — профиль бота (до 120 символов)

`Edit Bot` → `Edit About` (или `/setabouttext`):

```text
Реклама у блогеров Узбекистана · O‘zbekiston blogerlarida reklama
```

## 3. Кнопка меню — открывает приложение слева от поля ввода

`Bot Settings` → `Menu Button` → `Configure menu button`:

1. Отправить URL Mini App — тот же, что `Telegram:MiniAppUrl` в Railway (https).
2. Отправить название кнопки: `Открыть / Ochish`

## 4. Главное Mini App — кнопка «Открыть» в профиле бота

`Bot Settings` → `Configure Mini App` → `Enable Mini App` → отправить тот же URL.

## 5. Команды

`Edit Bot` → `Edit Commands` (или `/setcommands`), одной строкой:

```text
start - Открыть BloggerBazar / BloggerBazar’ni ochish
```

Других команд нет: вся работа — в приложении, бот только приветствует и присылает уведомления.

## 6. Webhook — без него бот не отвечает на `/start`

Telegram пересылает сообщения боту на `POST https://<домен API>/api/webhooks/telegram`. Сервер принимает
запрос только с заголовком секрета, равным `Telegram__WebhookSecret` (Railway, сервис API; 1–256 символов:
латиница, цифры, `_`, `-`). Без переменной сервер отвечает 401 на всё.

Включить (в браузере, одной строкой; токен и секрет никому не пересылать):

```text
https://api.telegram.org/bot<ТОКЕН>/setWebhook?url=https://<ДОМЕН_API>/api/webhooks/telegram&secret_token=<СЕКРЕТ>&allowed_updates=["message","pre_checkout_query"]&drop_pending_updates=true
```

Проверить: `https://api.telegram.org/bot<ТОКЕН>/getWebhookInfo` — `url` указывает на API, нет `last_error_message`.
Уведомления (сервер → пользователь) от webhook не зависят: им нужен только `Telegram:BotToken` и чтобы
пользователь нажал «Start» или разрешил сообщения в приложении.

## Что бот отвечает сам (код)

- Язык сообщений — язык, выбранный в приложении (D39). Пока он неизвестен (пользователь ещё не открывал
  приложение после обновления) — RU + UZ в одном сообщении.
- `/start` и `/start <параметр>` (ссылка `t.me/<бот>?start=…`) — приветствие и кнопка «🚀 Открыть» / «🚀 Ochish»
  (`TelegramBotClient.StartText`). Кнопка есть, только если в Railway задан `Telegram:MiniAppUrl`.
- Уведомления — `Application/Notifications/BotMessages.cs`, с кнопкой «Открыть» / «Ochish» на нужный экран:
  отклик → отклики кампании; решение по отклику, оффер, сделка, отзывы, напоминания → соответствующая страница.
- Сообщения, отправленные боту текстом, остаются без ответа.
- Бот может написать только тому, кто нажал «Start» или разрешил сообщения в приложении (ограничение Telegram).
