import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { readFile } from "node:fs/promises";
import { afterEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const haptic = vi.hoisted(() => ({ impact: vi.fn(), selection: vi.fn() }));

vi.mock("../src/telegram/TelegramProvider", () => ({
  useTelegram: () => ({ haptic })
}));

import { BottomNav } from "../src/components/ui";

afterEach(() => cleanup());

describe("BottomNav", () => {
  it("returns the user home through the central action", async () => {
    const user = userEvent.setup();
    window.location.hash = "#/search";
    render(<I18nProvider><BottomNav /></I18nProvider>);

    await user.click(screen.getByRole("button", { name: translate("nav.home", undefined, "ru") }));

    expect(window.location.hash).toBe("#/");
    expect(haptic.impact).toHaveBeenCalledTimes(1);
  });

  it.each([
    ["#/search", "nav.search"],
    ["#/blogger/123", "nav.search"],
    ["#/campaigns", "nav.campaigns"],
    ["#/campaign/123", "nav.campaigns"],
    ["#/my-campaigns", "nav.campaigns"],
    ["#/my-campaign/123", "nav.campaigns"],
    ["#/requests", "nav.requests"],
    ["#/favorites", "nav.profile"],
    ["#/brand-face-detail/123", "nav.search"],
    ["#/brand-face", "nav.profile"],
    ["#/blogger-form", "nav.profile"]
  ] as const)("marks %s as active in its parent tab", (hash, key) => {
    window.location.hash = hash;
    render(<I18nProvider><BottomNav /></I18nProvider>);
    expect(screen.getByRole("link", { name: translate(key, undefined, "ru") })).toHaveAttribute("aria-current", "page");
  });

  it("does not mark Profile active for public Brand Face Details", () => {
    window.location.hash = "#/brand-face-detail/123";
    render(<I18nProvider><BottomNav /></I18nProvider>);

    expect(screen.getByRole("link", { name: translate("nav.search", undefined, "ru") })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: translate("nav.profile", undefined, "ru") })).not.toHaveAttribute("aria-current");
  });

  it("keeps five localized routes and a neutral token-based visual system", async () => {
    window.location.hash = "#/";
    render(<I18nProvider><BottomNav /></I18nProvider>);
    expect(screen.getByRole("link", { name: translate("nav.search", undefined, "ru") })).toHaveAttribute("href", "#/search");
    expect(screen.getByRole("link", { name: translate("nav.campaigns", undefined, "ru") })).toHaveAttribute("href", "#/campaigns");
    expect(screen.getByRole("link", { name: translate("nav.requests", undefined, "ru") })).toHaveAttribute("href", "#/requests");
    expect(screen.getByRole("link", { name: translate("nav.profile", undefined, "ru") })).toHaveAttribute("href", "#/profile");
    expect(screen.getByRole("button", { name: translate("nav.home", undefined, "ru") })).toBeInTheDocument();
    expect(translate("nav.campaigns", undefined, "uz")).toBe("Kampaniyalar");

    const styles = await readFile("src/styles.css", "utf8");
    const bottomNavStyles = styles.slice(styles.indexOf("  .bottom-nav {"), styles.indexOf("  .fixed-action-bar {"));
    expect(bottomNavStyles).toContain("background: var(--bb-surface)");
    expect(bottomNavStyles).toContain("border-top: 1px solid var(--bb-border)");
    expect(bottomNavStyles).toContain("background: var(--bb-accent-subtle)");
    expect(bottomNavStyles).not.toMatch(/blue|cyan|gradient|backdrop-blur/i);
  });
});
