import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const styles = readFileSync("src/styles.css", "utf8");

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
});
