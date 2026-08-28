import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const styles = readFileSync("src/styles.css", "utf8");
const myCampaigns = readFileSync("src/pages/MyCampaigns.tsx", "utf8");
const myCampaignDetails = readFileSync("src/pages/MyCampaignDetails.tsx", "utf8");
const app = readFileSync("src/App.tsx", "utf8");

describe("Telegram page header clearance", () => {
  it("uses one effective top token for regular forms, FTUE screens, and the Business wizard", () => {
    expect(styles).toContain("--app-content-top: max(var(--app-page-gutter), var(--tg-effective-content-top, 0px), env(safe-area-inset-top));");
    expect(styles).toMatch(/\.screen\s*\{[\s\S]*?padding-top: var\(--app-content-top\) !important;/);
    expect(styles).toMatch(/\.ftue-screen\s*\{[\s\S]*?padding-top: var\(--app-content-top\);/);
    expect(styles).toMatch(/\.wizard-screen\s*\{[\s\S]*?padding-top: var\(--app-content-top\);/);
    expect(styles).toMatch(/\.ftue-screen\s*\{[\s\S]*?padding-right: 1rem;[\s\S]*?padding-bottom:[\s\S]*?padding-left: 1rem;/);
  });

  it("collapses Blogger field grids and constrains their controls at the 390px mobile breakpoint", () => {
    expect(styles).toMatch(/@media \(max-width: 26\.25rem\) \{[\s\S]*?form\.screen \.grid\.grid-cols-2 \{[\s\S]*?grid-template-columns: minmax\(0, 1fr\);/);
    expect(styles).toMatch(/form\.screen \.grid\.grid-cols-2 > \* \{[\s\S]*?min-width: 0;[\s\S]*?width: 100%;/);
    expect(styles).toMatch(/form\.screen \.grid\.grid-cols-2 input,[\s\S]*?max-width: 100%;/);
  });

  it("keeps wizard title and progress on the same full-width left boundary", () => {
    expect(styles).toMatch(/\.wizard-header \{[\s\S]*?width: 100%;[\s\S]*?min-width: 0;/);
    expect(styles).toMatch(/\.wizard-header__copy \{ width: 100%; min-width: 0; \}/);
    expect(styles).toMatch(/\.wizard-progress \{ display: grid; gap: \.5rem; width: 100%; min-width: 0; \}/);
    expect(styles).not.toContain("wizard-header__back-placeholder");
  });

  it("uses the shared Telegram clearance and a single native BackButton policy for private campaign routes", () => {
    expect(myCampaigns).toContain('className="campaign-management-screen my-campaigns catalog-search screen screen--with-nav"');
    expect(myCampaignDetails).toContain('className="campaign-management-screen my-campaign-details screen screen--with-nav"');
    expect(myCampaigns).not.toContain("my-campaigns__back");
    expect(myCampaignDetails).not.toContain("my-campaigns__back");
    expect(app).toContain('useTelegramBackHandler(goBackFromNestedRoute, onboardingStep === "complete" && !rootRoutes.includes(route.path));');
    expect(styles).toMatch(/\.campaign-management-screen \{ scroll-padding-top: var\(--app-content-top\); \}/);
    expect(styles).toMatch(/html\[data-telegram-embedded="true"\] \.campaign-management-screen \{ padding-top: calc\(var\(--app-content-top\) \+ var\(--app-page-gutter\)\) !important; scroll-padding-top: calc\(var\(--app-content-top\) \+ var\(--app-page-gutter\)\); \}/);
  });

  it("stacks My Campaigns header controls at the narrow mobile breakpoint without changing the shared keyboard layout", () => {
    expect(styles).toMatch(/\.my-campaigns__header \{ display: grid; grid-template-columns: minmax\(0, 1fr\) auto; align-items: start; \}/);
    expect(styles).toMatch(/\.my-campaigns__header-actions \{ display: inline-flex; min-width: 0;/);
    expect(styles).toMatch(/@media \(max-width: 390px\) \{[\s\S]*?\.my-campaigns__header \{ grid-template-columns: minmax\(0, 1fr\); \}[\s\S]*?\.my-campaigns__header-actions \{ width: 100%; justify-content: space-between; \}/);
    expect(styles).toMatch(/\.screen--with-nav \{[\s\S]*?padding-bottom: max\(6rem, calc\(var\(--bb-bottom-nav-height\) \+ 1\.25rem \+ var\(--tg-content-safe-bottom/);
    expect(styles).toMatch(/html\[data-virtual-keyboard-open="true"\] \.screen--with-nav \{/);
  });
});
