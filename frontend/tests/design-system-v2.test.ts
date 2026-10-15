import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const styles = readFileSync("src/styles.css", "utf8");
const ui = readFileSync("src/components/ui.tsx", "utf8");
const tailwind = readFileSync("tailwind.config.js", "utf8");
const i18n = readFileSync("src/i18n/index.tsx", "utf8");
// The tokens and Tailwind aliases live in the shared workspace (also used by admin-web); the Mini App imports them.
const tokens = readFileSync("../shared/tokens.css", "utf8");
const tailwindPreset = readFileSync("../shared/tailwind-preset.js", "utf8");

describe("Design System v2 contract", () => {
  it("defines semantic light and dark theme foundations", () => {
    for (const token of [
      "--bb-background",
      "--bb-surface-elevated",
      "--bb-text-secondary",
      "--bb-accent",
      "--bb-action",
      "--bb-success-subtle",
      "--bb-warning-subtle",
      "--bb-error-subtle",
      "--bb-error-text",
      "--bb-focus",
      "--bb-radius-control",
      "--bb-shadow-overlay"
    ]) {
      expect(tokens).toContain(`${token}:`);
    }

    expect(tokens).toContain('html[data-telegram-theme="dark"]');
    expect(styles).toContain('@import "@bloggerbazar/shared/tokens.css";');
  });

  it("routes legacy Tailwind brand aliases through semantic tokens", () => {
    expect(tailwindPreset).toContain('blue: "var(--bb-action)"');
    expect(tailwindPreset).toContain('cyan: "var(--bb-accent)"');
    expect(tailwindPreset).toContain('card: "var(--bb-shadow-card)"');
    expect(tailwind).toContain("presets: [designSystem]");
  });

  it("uses shared primitive classes for controls, states, and overlays", () => {
    for (const className of [
      "ds-button--primary",
      "ds-button--secondary",
      "ds-field",
      "ds-badge--success",
      "ds-state__icon",
      "ds-dialog__handle"
    ]) {
      expect(ui).toContain(className);
      expect(styles).toContain(`.${className}`);
    }
  });

  it("keeps fixed feedback and overlays aware of Telegram safe areas", () => {
    expect(ui).toContain("--tg-content-safe-bottom");
    expect(styles).toContain("--tg-content-safe-top");
    expect(styles).toContain("--tg-viewport-height");
  });

  it("uses semantic focus styling without a global Telegram dark input override", () => {
    expect(styles).toContain("outline: 2px solid var(--bb-focus)");
    expect(styles).toContain(".ds-field:focus-within");
    expect(styles).not.toContain('html[data-telegram-theme="dark"] input');
    expect(styles).not.toContain('html[data-telegram-theme="dark"] textarea');
    expect(styles).not.toContain('html[data-telegram-theme="dark"] select');
  });

  it("allows long localized navigation labels and exposes the active document language", () => {
    expect(styles).toContain("-webkit-line-clamp: 2");
    expect(styles).toContain("overflow-wrap: anywhere");
    expect(i18n).toContain("document.documentElement.lang = language");
  });
});
