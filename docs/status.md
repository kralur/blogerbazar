# BloggerBazar — текущий статус

Волатильный файл: обновляется при каждой публикации/checkpoint. Исторические результаты gates
**не являются** результатами будущих изменений.

_Последнее обновление: 2026-10-05, Phase 0 (documentation setup)._

## Checkpoint

| | |
|---|---|
| Published `main` / `origin/main` | `313d26f8188a27f1e983e48c6914163a2a93e51a` — `feat(deals): add immutable campaign terms snapshot` |
| `origin/codex/phase-3f-a-deal-integrity` | `313d26f` |
| Рабочая ветка Claude | `claude/pensive-fermat-rp0p5w` (`313d26f` + docs-коммит Phase 0, не в `main`) |

## Текущая фаза

- **Phase 0 — Documentation setup**: завершена; решения по вопросам E1–E6 приняты владельцем (D26–D30), документация закоммичена в feature-ветку.
- Следующая implementation phase: **Phase 3F-B — Private Deal API, Security & Lifecycle** (backend only). Код 3F-B не менялся.

## Последние результаты gates

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

## Среда Claude Code (cloud)

- `dotnet`: отсутствует (`command not found`).
- Docker: CLI есть (29.6.2), daemon недоступен (`/var/run/docker.sock` отсутствует) → Testcontainers не запустятся.
- Node: `/opt/node22` (npm доступен); `node_modules` не установлены.
- CI (`.github/workflows/ci.yml`): backend с `RUN_INTEGRATION_TESTS=true` + frontend на PR/push в `main` — путь получить реальные gates.
