# BloggerBazar — текущий статус

Волатильный файл: обновляется при каждой публикации/checkpoint. Исторические результаты gates
**не являются** результатами будущих изменений.

_Последнее обновление: 2026-10-05, публикация Phase 3F-C._

## Checkpoint

| | |
|---|---|
| Published `main` / `origin/main` | Phase 3F-C (см. `git log`; предыдущие checkpoints `a17819b` 3F-B, `313d26f` 3F-A) |
| Рабочая ветка Claude | `claude/pensive-fermat-rp0p5w` (сохраняется) |
| CI PR для gates | 3F-B: https://github.com/kralur/blogerbazar/pull/1 · 3F-C: https://github.com/kralur/blogerbazar/pull/2 |

## Текущая фаза

- **Phase 3F-B — Private Deal API, Security & Lifecycle**: опубликована (`a17819b`).
- **Phase 3F-C — frontend Deal bridge**: опубликована (`579d6c3`).
- **Phase 4A — Contacts privacy** (D31): закоммичена (`75aa8e3`, fix `b3d4dcd`, tests `fb9ac1c`), CI зелёный (265 passed), придержана до 4B.
- **Phase 4B — Offers**: реализована, не закоммичена, ожидает review владельца. Миграция `AddCollaborationOfferTerms` одобрена владельцем.
- Далее: 4B (предложения) → 4C (отзывы 2.0) → FEATURE FREEZE → Full UI Redesign.

## Последние результаты gates

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

Деплой: Railway автоматически деплоит при push в `main` (подтверждено владельцем). Дополнительно
`deploy-railway.yml` деплоит после зелёного CI на `main` — до этого он не срабатывал, т.к. CI был красный.

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

- `dotnet`: отсутствует (`command not found`).
- Docker: CLI есть (29.6.2), daemon недоступен (`/var/run/docker.sock` отсутствует) → Testcontainers не запустятся.
- Node: `/opt/node22` (npm доступен); `node_modules` не установлены.
- CI (`.github/workflows/ci.yml`): backend с `RUN_INTEGRATION_TESTS=true` + frontend на PR/push в `main` — путь получить реальные gates.
