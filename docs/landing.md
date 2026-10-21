# Сайт-лендинг (landing): запуск и настройка

Публичная страница BloggerBazar (решение D58). Код: `landing/`. Все кнопки открывают бота с меткой
`site_blogger`, `site_business` или `site`, поэтому в админке в «Источниках» видно, сколько людей пришло с сайта.

## Что сделать владельцу один раз (Railway)

Секреты не нужны. Имя бота — публичное, не путать с токеном бота.

1. **Railway → New → GitHub Repo → `kralur/blogerbazar`** (новый сервис, например `landing`).
   - Settings → Build: **Dockerfile Path** = `landing/Dockerfile`.
   - Variables: `BOT_USERNAME` = имя бота без `@` (то же, что `Telegram__BotUsername` у API).
     Без него сборка остановится с понятной ошибкой.
   - Settings → Networking → **Generate Domain**. Если Railway спросит порт: `8080`.
2. Открыть адрес и нажать «Я блогер»: должен открыться бот. В админке через день появится метка `site_blogger`
   в «Источниках» (если человек нажал Start).

## Свой домен (когда понадобится)

1. **Купить домен** у регистратора:
   - `.uz` (например `bloggerbazar.uz`): у аккредитованных регистраторов зоны `.uz`, список на сайте cctld.uz;
   - `.com` и другие: например Cloudflare Registrar или Namecheap.
2. **Railway → сервис `landing` → Settings → Networking → Custom Domain** → ввести домен. Railway покажет запись
   (обычно CNAME), её нужно добавить в DNS у регистратора. Сертификат HTTPS Railway выпустит сам.
3. Адрес Railway (`...up.railway.app`) продолжит работать.

Сайт-админку на свой домен (например `admin.bloggerbazar.uz`) — см. `docs/admin-web.md`, раздел «Свой домен».

## Как устроено

- Одна страница: первый экран с кнопками «Я блогер» и «Я бизнес», «Как это работает» (шаги для бизнеса и блогера),
  «Почему BloggerBazar», «Для кого», «Частые вопросы», финальная кнопка.
- Тексты: `landing/src/i18n/{ru,uz}.json`, проверяются `npm run i18n:audit`. Без длинного тире, узбекские ‘ ’.
- Нет API, cookie и форм; строгий CSP; сайт открыт для поисковиков.
- Проверки: `npm --workspace landing run test`, `npm --workspace landing run build`; в CI отдельная задача
  «Landing build and tests» и сборка образа с проверкой nginx.
