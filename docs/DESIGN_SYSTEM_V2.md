# BloggerBazar Design System v2

## Purpose

Design System v2 is the shared visual and interaction foundation for the Telegram Mini App. It keeps BloggerBazar's acid-lime identity while replacing the mixed legacy blue-gradient and light-only component styles with semantic tokens.

This phase changes shared foundations only. Individual product screens migrate incrementally as their workflows are completed.

## Principles

1. **Marketplace first.** Interfaces should be compact, scannable, and focused on repeated decisions rather than decorative presentation.
2. **Acid lime means primary action.** `--bb-accent` is reserved for the strongest action, selection, and verification emphasis.
3. **Blue means navigation or information.** `--bb-action` is used for links, focus, informational states, and secondary interactive emphasis.
4. **Semantic colors are stateful.** Success, warning, error, info, and neutral styles must use the semantic tokens instead of raw Tailwind colors.
5. **One component contract across themes.** Components use the same markup in light and dark themes; CSS variables provide the theme values.
6. **Telegram constraints are native constraints.** Screens, overlays, fixed actions, and navigation must account for Telegram viewport and safe-area variables.

## Token layers

Tokens live in `frontend/src/styles.css`. Tailwind aliases in `frontend/tailwind.config.js` point to those variables so existing utility classes inherit both themes.

### Surfaces and text

- `--bb-background`: application canvas.
- `--bb-surface`: standard controls and cards.
- `--bb-surface-secondary`: quiet controls, placeholders, and neutral states.
- `--bb-surface-elevated`: dialogs and elevated surfaces.
- `--bb-text`: primary content.
- `--bb-text-secondary`: labels, metadata, and supporting content.
- `--bb-border`: dividers and control boundaries.

### Brand and state

- `--bb-accent`, `--bb-accent-pressed`, `--bb-accent-subtle`: primary brand action.
- `--bb-action`, `--bb-action-pressed`, `--bb-action-subtle`: navigation, links, and info.
- `--bb-success`, `--bb-warning`, `--bb-error`, `--bb-info`: semantic foregrounds.
- Matching `*-subtle` tokens: semantic backgrounds.
- `--bb-premium`: premium-only emphasis, not a general brand color.

### Shape and elevation

- Controls: `--bb-radius-control` (`12px`).
- Cards: `--bb-radius-card` (`16px`).
- Bottom sheets/dialogs: `--bb-radius-overlay` (`20px`).
- Minimum control height: `--bb-control-height` (`48px`), with a minimum `44px` tap target.
- Use `--bb-shadow-card`, `--bb-shadow-action`, or `--bb-shadow-overlay`; do not introduce page-specific shadows.

## Shared primitives

All product screens should prefer exports from `frontend/src/components/ui.tsx`.

- `Button`: primary, secondary, ghost, and danger variants.
- `Input`, `Textarea`, `SearchBar`: common labels, errors, focus treatment, and theme behavior.
- `Card`, `StatsCard`, `Divider`, `SectionHeader`: content structure.
- `Badge`, `StatusBadge`, `Chip`: compact labels and states.
- `Modal`, `BottomSheet`: focus trap, Telegram BackButton registration, body scroll lock, and safe areas.
- `Skeleton`, `LoadingState`, `EmptyState`, `ErrorState`, `OfflineState`, `PermissionDeniedState`: async and exceptional states.
- `Toast`: short action feedback, positioned above Telegram navigation and safe areas.
- `FixedActionBar`, `BottomNav`, `FloatingActionButton`: fixed controls with keyboard and safe-area behavior.

Raw colors, custom shadows, and new one-off modal implementations are not allowed when a shared primitive can represent the behavior.

## Typography and spacing

- Screen title: `30px` maximum in compact product views.
- Section title: `18px`, weight `800`.
- Body: `15px`, regular or semibold.
- Supporting copy: `14px`.
- Metadata: `12-13px`.
- Use the existing `4px` spacing rhythm and prefer `8`, `12`, `16`, `20`, `24`, and `32px` gaps.
- Letter spacing is `0` for new compact UI. Legacy negative tracking is migrated when its screen is redesigned.

## Accessibility and localization

- Interactive controls require a visible `:focus-visible` treatment.
- Icon-only buttons require an accessible name.
- Status cannot rely on color alone; always include text.
- Dialogs must preserve focus, close with Escape and Telegram BackButton, and restore focus to the trigger.
- Layouts must tolerate longer Russian and Uzbek labels without fixed text widths.
- All user-facing strings belong in both locale dictionaries and must pass `npm run i18n:audit`.
- Motion respects `prefers-reduced-motion`.

## Responsive and Telegram rules

- Primary target: `320px-430px` Mini App viewport.
- No horizontal scrolling outside intentional rails.
- Fixed actions use `--tg-content-safe-bottom` and disappear or move when the virtual keyboard is open.
- Top content uses `--tg-effective-content-top` plus `env(safe-area-inset-top)`.
- Bottom sheets use `--tg-viewport-height`, remain scrollable, and never extend behind unsafe areas.

## Migration checklist

When redesigning a screen:

1. Replace raw palette utilities with semantic tokens or shared primitives.
2. Remove page-specific button, field, badge, dialog, and async-state implementations.
3. Verify light and dark Telegram themes.
4. Verify widths at `320px`, `360px`, `390px`, and `430px`.
5. Verify RU and UZ with long labels and empty/error/loading states.
6. Verify keyboard, Telegram BackButton, bottom navigation, and safe-area clearance.
7. Run frontend tests, production build, and i18n audit.
