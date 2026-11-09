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
- `--bb-success-on-subtle`, `--bb-warning-on-subtle`, `--bb-error-on-subtle`: state text on its `*-subtle`
  surface or a plain card (badges, the report button). The light-theme success, warning and error colors are too
  pale for small text on a pale surface; these mixes keep at least 4.5:1 in both themes.
- `--bb-error-fill`, `--bb-success-fill`, `--bb-warning-fill`, `--bb-info-fill`: solid fills under white text
  (danger button, counter badge, toasts), the same in both themes. Never put white text on `--bb-success`,
  `--bb-warning`, `--bb-error` or `--bb-info` directly: in the dark theme they are light colors.
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

## Redesign rules (D35)

- Visual direction: premium creator marketplace × modern fintech × Telegram-native. About 80% neutral surfaces,
  15% dark (text, selected states), 5% lime.
- `--bb-accent` (lime): primary CTA, active/selected state (`.choice-selected`, selected segments), verified marks.
- Blue (`--bb-action`/`--bb-info`): informational statuses and focus only — never decoration.
- No gradients, glassmorphism or glows. `--bb-gradient-*` remain only as solid aliases for legacy utilities.
- Every screen starts with `PageHeader` (`components/PageHeader.tsx`): optional back link (hidden inside Telegram,
  which shows its native BackButton), eyebrow, title (24px), trailing actions that wrap below on narrow screens.
- The language switcher lives in Profile and first-run screens only.
- Budgets are formatted only with `formatBudgetRange` (`lib/currency.ts`).
- Tailwind aliases for tokens: `brand-surface`, `brand-soft` (surface-secondary), `brand-accent`, `brand-ink`, `brand-muted`.

## Buttons and choices (review 2026-11-07)

- **Picked choice, one look everywhere:** lime outline, `--bb-accent-subtle` fill and a lime tick (`.marketplace-chip--active`,
  `.brand-face-chip[aria-pressed="true"]`, `.choice-selected`). No solid-lime or dark-outline variants.
- **Dialog buttons:** `.modal-actions` keeps Cancel and the action side by side while both labels fit on one line; when they
  do not, each takes the full width and the action goes on top. A label is never broken over two lines.
- **Wizard Continue stays active.** A tap on an incomplete step marks its fields and brings the first error into view
  (`revealFirstWizardError`); a grey button that does not say why is not used.
- **Examples in fields start with "Например, …" / "Masalan, …"** (`form.example`), so an empty field never looks filled.
- **Report** is a quiet grey link at the end of a profile, campaign, offer or deal, not a red button in the header.
- **Filter fields** use the same white surface with a border as form fields.

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

## Direction "Bazar" (D61, D62)

The landing (D61) and, from 2026-11-07, the Mini App Home (D62) follow the "Bazar" direction on top of DS v2:

- `--bb-lapis` `#17307A` (the Telegram header, the Home hero), `--bb-lapis-text`, `--bb-ink`, `--bb-tile` `#19A59C`
  (links such as "view all", counters, the progress bar), `--bb-saffron` and `--bb-coral` (only inside the atlas
  strip), `--bb-quiet` (the "waiting for your decision" surface). Defined in `frontend/src/styles.css`, not in
  `shared/tokens.css`: the web admin stays on DS v2.
- `--bb-display`: Unbounded for titles and prices only (`frontend/src/fonts/`, SIL OFL); body text stays on the
  system font.
- Dark theme in the Mini App: night lapis `#0E1B45` page with `#142457` / `#1B2D66` surfaces instead of black.
- `components/AtlasStrip.tsx`: the abrband strip, 26 px on Home; the same drawing as the landing's `Ikat`. It scrolls
  sideways under the finger (native horizontal scroll, no scrollbar) and drifts slowly on its own; `prefers-reduced-motion`
  stops the drift.
- `components/AtlasBackground.tsx`: the full-screen atlas behind the first screen, the Telegram sign-in, phone and role screens
  (`.ftue-screen--atlas`), under a lapis veil that thickens towards the bottom; white text and white role cards on top,
  the same in both themes.
- Acid lime keeps its DS v2 meaning: the primary action, the selected tab, the edge of an item waiting for a decision.
- List screens (D63, package B): `components/ListHero.tsx` puts the page header, tabs, search and filters on lapis
  under the Telegram chrome with the atlas strip below, like the Home hero; `EmptyState` and the full-size `CatalogState`
  are price tags on a cord; bloggers in Search and Favorites are rows with the price on the right; my campaigns are price
  tags with the budget first.
- Profile and the public company page (D63, package C) reuse `ListHero`: the account and the role cards, or the logo,
  name and two fact tags, sit in the lapis head.
- Profile wizards (D63, package D): `WizardHeader` is a lapis head with the atlas strip; `ProgressIndicator` draws the
  steps as beads on a cord (lime done and current, saffron cord).
- Sheets (`Modal`, `BottomSheet`): edge to edge, title in the display face, a grip the finger can pull; a short pull
  snaps back, a long or fast one closes.
- Detail screens (D63, package G): `ListHero` with `detail-hero` on blogger, brand face, campaign, my campaign, deal,
  offer, application and settings; facts as tags (`FactGrid` with `hero-tags`), the price or status large in the head,
  text and actions below. Sums in narrow tags are compact (`compactMoney`, `compactBudgetRange` in `DetailBlocks`).
- Atlas switch (D63, package I): `lib/atlasPreference.ts`; off, the strip and the first-screen background are not
  drawn (the Settings previews still are). The strip is silent: no tick, no sound.
- Seasons (D63): `lib/season.ts` sets `data-season` on `<html>`: the calendar season by default, or the one the
  person pinned in Settings (five tiles: standard and the four seasons); the atlas is drawn from `--bb-atlas-*`
  tokens so a season recolours it without touching lime or tile. `components/SeasonArt.tsx` picks the season's
  drawing for the two layers every season shares (`components/season/FallingArt.tsx`: static pieces on the Home
  hero, falling or rising and catchable pieces on the first screens): autumn chinar leaves, winter snowflakes,
  spring apricot blossom, summer fireflies. The person switches the season off in
  Settings or from the note under the Home strip.
