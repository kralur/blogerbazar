import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getCurrentPlatformUser: vi.fn(), setBotTips: vi.fn(), peekCurrentPlatformUser: vi.fn(() => null) }));
const browser = vi.hoisted(() => ({ getBrowserSessions: vi.fn(), endBrowserSession: vi.fn(), endAllBrowserSessions: vi.fn(), signOutBrowser: vi.fn() }));
vi.mock("../src/api/marketplace", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/marketplace")>()), ...api }));
vi.mock("../src/api/browserSessions", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/browserSessions")>()), ...browser }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { impact: vi.fn(), success: vi.fn(), selection: vi.fn(), error: vi.fn(), warning: vi.fn() }, registerBackButtonHandler: () => () => undefined }) }));
vi.mock("../src/components/AccountActions", () => ({ AccountActions: () => <p>Account actions</p> }));
vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => null }));

import { setBrowserSession } from "../src/lib/browserSession";
import { Settings } from "../src/pages/Settings";

const ru = (key: string, values?: Record<string, string | number>) => translate(key, values, "ru");
const owner = { telegramUserId: 1, firstName: "Umid", role: 4, isBlocked: false, selectedMarketplaceRole: "Business" };
const session = (id: string, isCurrent: boolean, browserName = "chrome") => ({ id, browser: browserName, os: "windows", createdAtUtc: "2026-11-09T09:05:00Z", lastSeenAtUtc: "2026-11-09T10:00:00Z", expiresAtUtc: "2026-11-09T21:05:00Z", isCurrent });

describe("Settings: my browsers (D64 stage B)", () => {
  beforeEach(() => { vi.clearAllMocks(); setBrowserSession(false); });
  afterEach(() => setBrowserSession(false));

  it("lists a company member's browsers in the Mini App and ends them all after confirmation", async () => {
    api.getCurrentPlatformUser.mockResolvedValue(owner);
    browser.getBrowserSessions.mockResolvedValueOnce([session("a", false), session("b", false, "edge")]).mockResolvedValue([]);
    browser.endAllBrowserSessions.mockResolvedValue(undefined);
    render(<I18nProvider><Settings /></I18nProvider>);

    const list = await screen.findByRole("region", { name: ru("browser.sessions.title") });
    expect(await within(list).findByText("Chrome, Windows")).toBeInTheDocument();
    expect(within(list).getByText("Edge, Windows")).toBeInTheDocument();
    expect(screen.getByText("Account actions")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: new RegExp(ru("settings.adminPanel")) })).toBeInTheDocument();

    fireEvent.click(within(list).getByRole("button", { name: ru("browser.sessions.endAll") }));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: ru("browser.sessions.endAll") }));

    await waitFor(() => expect(browser.endAllBrowserSessions).toHaveBeenCalledTimes(1));
    expect(await within(list).findByText(ru("browser.sessions.none"))).toBeInTheDocument();
  });

  it("does not show the list to somebody who is not working as a company", async () => {
    api.getCurrentPlatformUser.mockResolvedValue({ ...owner, selectedMarketplaceRole: "Blogger" });
    render(<I18nProvider><Settings /></I18nProvider>);

    await waitFor(() => expect(api.getCurrentPlatformUser).toHaveBeenCalled());
    expect(screen.queryByRole("region", { name: ru("browser.sessions.title") })).not.toBeInTheDocument();
    expect(browser.getBrowserSessions).not.toHaveBeenCalled();
  });

  it("in a browser keeps only what works there: no admin panel, no account deletion, a sign-out", async () => {
    setBrowserSession(true);
    api.getCurrentPlatformUser.mockResolvedValue(owner);
    browser.getBrowserSessions.mockResolvedValue([session("a", true)]);
    browser.signOutBrowser.mockResolvedValue(undefined);
    const onBrowserSignedOut = vi.fn();
    render(<I18nProvider><Settings onBrowserSignedOut={onBrowserSignedOut} /></I18nProvider>);

    expect(await screen.findByText(ru("browser.sessions.current"))).toBeInTheDocument();
    expect(screen.queryByText("Account actions")).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: new RegExp(ru("settings.adminPanel")) })).not.toBeInTheDocument();
    expect(screen.getByText(ru("browser.telegramOnly"))).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: ru("browser.signOut") }));
    await waitFor(() => expect(onBrowserSignedOut).toHaveBeenCalled());
    expect(browser.signOutBrowser).toHaveBeenCalled();
  });
});
