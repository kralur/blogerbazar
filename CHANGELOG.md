# Changelog

## v1.0.0

- Completed Telegram-first onboarding with Blogger, Brand Face, and Business roles; valid profiles publish automatically and contacts are publicly available.
- Added database-backed platform roles, Owner-only user management, blocking, and audit logging.
- Added public Brand Face catalog details and Home navigation while preserving the App Store-style horizontal showcase rails.
- Completed RU/UZ localization coverage and added `npm run i18n:audit` for dictionary parity, duplicate keys, missing key references, and hardcoded UI text.
- Improved translated payment and Telegram error handling without changing existing Click or legacy contact-unlock compatibility endpoints.

## v0.5.0

- Expanded the deterministic development marketplace to 360 unique blogger profiles, 120 businesses, 360 campaigns, 1,200 applications, 500 completed deals, and 1,000 reviews.
- Added Uzbek-city coverage, complete marketplace categories, varied pricing and metrics, diverse review copy, and unique avatar, logo, cover, and portfolio placeholder URLs.
- Added truthful promoted blogger seed data and removed the marketplace home query's 100-record cap so the showcase and statistics use the full demo dataset.

## v0.4.1

- Added a centralized, browser-safe Telegram Mini App provider for startup, expansion, theme values, user data, BackButton, invoice access, and haptic feedback.
- Added an Apple-style 600 ms launch splash screen and safe Telegram BackButton behavior: hidden on Home and routed back elsewhere.
- Removed direct frontend SDK access and the unused `@twa-dev/sdk` dependency; browser fallback now runs without Telegram globals.
- Refined bot `/start` copy and preserved the existing inline `web_app` launch button.

## v0.4.0

- Refined the shared Mini App UI into a cohesive iOS-inspired system with motion-safe transitions, shimmer skeletons, safe-area sheets, focus treatment, and a more expressive central Home control.
- Rebuilt blogger and campaign cards around truthful API data with structured metrics, pricing, rating, deadline, promotion disclosure, and clear tap feedback.
- Added reusable presentation primitives for section headers, status badges, prices, dividers, containers, and bottom sheets.
- Expanded development seed targets to 150 bloggers, 60+ businesses, 120 campaigns, 500 applications, 200 completed deals, and 300 reviews.

## v0.3.1

- Removed misleading catalog and promotion purchase claims until an entitlement and promotion-product model exists; the unavailable promotion CTA is explicitly marked as coming soon.
- Added a separate HTTPS-only business website field, a migration-safe payment-order expiry lifecycle, and future `auth_date` rejection for Telegram Mini App authentication.
- Moved blogger catalog filtering, sorting, pagination, and result counts into PostgreSQL; added payment-expiry and localization-key parity coverage.
- Made campaign creation require a business profile, surfaced real blogger moderation status, and normalized campaign categories around canonical stored codes.

## v0.3.0

- Expanded the development marketplace to production-scale Uzbek data and strengthened catalog, campaign, and profile empty/error states.
- Added reusable loading, offline, permission, error, and no-data presentation components for a consistent Telegram Mini App experience.
- Completed campaign creation UX with requirements, deadline, retry, empty, and client-side discovery states.
- Extended the profile dashboard with completion, application, and deal indicators; retained safe role switching and local logout.

## v0.2.0

- Added API-driven marketplace home sections, statistics, taxonomy, and truthful promotion labels.
- Refined navigation, profile flows, category selection, numeric formatting, loading, empty, and error states.
- Improved campaign lifecycle, catalog filtering, privacy boundaries, payment-provider error handling, and Telegram notifications.
- Expanded development marketplace data and release verification coverage.
