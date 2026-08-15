import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const styles = readFileSync("src/styles.css", "utf8");

describe("Telegram page header clearance", () => {
  it("uses one effective top token for regular forms, FTUE screens, and the Business wizard", () => {
    expect(styles).toContain("--tg-page-content-top: max(var(--app-page-gutter), var(--tg-effective-content-top, 0px), env(safe-area-inset-top));");
    expect(styles).toMatch(/\.screen\s*\{[\s\S]*?padding-top: var\(--tg-page-content-top\) !important;/);
    expect(styles).toMatch(/\.ftue-screen\s*\{[\s\S]*?padding: var\(--tg-page-content-top\) 1rem/);
    expect(styles).toMatch(/\.wizard-screen\s*\{[\s\S]*?padding: var\(--tg-page-content-top\) 1rem/);
  });
});
