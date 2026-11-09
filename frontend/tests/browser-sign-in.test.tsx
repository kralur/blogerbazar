import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  startBrowserSignIn: vi.fn(),
  checkBrowserSignIn: vi.fn(),
  previewBrowserSignIn: vi.fn(),
  confirmBrowserSignIn: vi.fn(),
  declineBrowserSignIn: vi.fn()
}));
const marketplace = vi.hoisted(() => ({ selectMarketplaceRole: vi.fn() }));
vi.mock("../src/api/browserSessions", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/browserSessions")>()), ...api }));
vi.mock("../src/api/marketplace", () => marketplace);
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { selection: vi.fn(), success: vi.fn(), error: vi.fn(), warning: vi.fn() } }) }));
vi.mock("../src/components/AtlasBackground", () => ({ AtlasBackground: () => null }));
vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => null }));

import { browserSignInCodeFrom, deviceLabel } from "../src/api/browserSessions";
import { ApiError } from "../src/api/client";
import { BrowserSignIn } from "../src/pages/BrowserSignIn";
import { BrowserSignInConfirm } from "../src/pages/BrowserSignInConfirm";

const ru = (key: string, values?: Record<string, string | number>) => translate(key, values, "ru");
const code = "0123456789abcdefghijAB";
const preview = (eligibility: string, extra: Record<string, unknown> = {}) => ({
  browser: "chrome", os: "windows", createdAtUtc: "2026-11-09T09:05:00Z", expiresAtUtc: "2026-11-09T09:07:00Z",
  options: [17, 42, 88], eligibility, companyName: "Kofe Hona", ...extra
});

describe("browser sign-in page", () => {
  beforeEach(() => { vi.clearAllMocks(); vi.useFakeTimers({ shouldAdvanceTime: true }); });
  afterEach(() => { vi.useRealTimers(); });

  it("shows the Telegram link and the number, then signs in once Telegram confirms", async () => {
    api.startBrowserSignIn.mockResolvedValue({ code, number: 42, link: `https://t.me/BloggerBazarBot?startapp=web_${code}`, expiresAtUtc: new Date(Date.now() + 120_000).toISOString() });
    api.checkBrowserSignIn.mockResolvedValueOnce({ status: "pending" }).mockResolvedValue({ status: "approved", firstName: "Umid" });
    const onSignedIn = vi.fn();
    render(<I18nProvider><BrowserSignIn onSignedIn={onSignedIn} /></I18nProvider>);

    fireEvent.click(screen.getByRole("button", { name: ru("browser.signIn") }));

    expect(await screen.findByRole("img", { name: ru("browser.numberLabel", { number: 42 }) })).toHaveTextContent("42");
    expect(screen.getByRole("link", { name: ru("browser.openTelegram") })).toHaveAttribute("href", `https://t.me/BloggerBazarBot?startapp=web_${code}`);
    // The same link as a QR code for the phone's camera: a square of modules with a quiet zone.
    const qr = screen.getByRole("img", { name: ru("browser.qrLabel") });
    expect(qr.getAttribute("viewBox")).toMatch(/^0 0 (\d+) \1$/);
    expect(qr.querySelector("path")?.getAttribute("d")).toMatch(/^M\d+ \d+h1v1h-1z/);
    await act(async () => { await vi.advanceTimersByTimeAsync(3_000); });
    expect(onSignedIn).not.toHaveBeenCalled();
    await act(async () => { await vi.advanceTimersByTimeAsync(3_000); });
    await waitFor(() => expect(onSignedIn).toHaveBeenCalledTimes(1));
  });

  it("offers a new code once the two minutes are over", async () => {
    api.startBrowserSignIn.mockResolvedValue({ code, number: 42, link: "https://t.me/b?startapp=web_x", expiresAtUtc: new Date(Date.now() + 2_000).toISOString() });
    api.checkBrowserSignIn.mockResolvedValue({ status: "pending" });
    render(<I18nProvider><BrowserSignIn onSignedIn={vi.fn()} /></I18nProvider>);

    fireEvent.click(screen.getByRole("button", { name: ru("browser.signIn") }));
    await screen.findByRole("img", { name: ru("browser.numberLabel", { number: 42 }) });
    await act(async () => { await vi.advanceTimersByTimeAsync(3_000); });

    expect(screen.getByText(ru("browser.expired"))).toBeInTheDocument();
    expect(screen.getByRole("button", { name: ru("browser.again") })).toBeInTheDocument();
  });

  it("says so when the sign-in was declined in Telegram", async () => {
    api.startBrowserSignIn.mockResolvedValue({ code, number: 42, link: "https://t.me/b?startapp=web_x", expiresAtUtc: new Date(Date.now() + 120_000).toISOString() });
    api.checkBrowserSignIn.mockResolvedValue({ status: "declined" });
    render(<I18nProvider><BrowserSignIn onSignedIn={vi.fn()} /></I18nProvider>);

    fireEvent.click(screen.getByRole("button", { name: ru("browser.signIn") }));
    await screen.findByRole("img", { name: ru("browser.numberLabel", { number: 42 }) });
    await act(async () => { await vi.advanceTimersByTimeAsync(3_000); });

    expect(screen.getByText(ru("browser.declined"))).toBeInTheDocument();
  });
});

describe("browser sign-in confirmation in the Mini App", () => {
  beforeEach(() => { vi.clearAllMocks(); api.declineBrowserSignIn.mockResolvedValue(undefined); });

  it("names the browser and the company and confirms with the number tapped", async () => {
    api.previewBrowserSignIn.mockResolvedValue(preview("ok"));
    api.confirmBrowserSignIn.mockResolvedValue(undefined);
    render(<I18nProvider><BrowserSignInConfirm code={code} onClose={vi.fn()} onRoleChanged={vi.fn()} /></I18nProvider>);

    expect(await screen.findByText("Chrome, Windows")).toBeInTheDocument();
    expect(screen.getByText(ru("browser.confirm.company", { name: "Kofe Hona" }))).toBeInTheDocument();
    expect(screen.getByText(ru("browser.confirm.warning"))).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "42" }));

    expect(await screen.findByText(ru("browser.confirm.done"))).toBeInTheDocument();
    expect(api.confirmBrowserSignIn).toHaveBeenCalledWith(code, 42);
  });

  it("tells that a wrong number cancelled the sign-in", async () => {
    api.previewBrowserSignIn.mockResolvedValue(preview("ok"));
    api.confirmBrowserSignIn.mockRejectedValue(new ApiError(409, "browser_number_mismatch"));
    render(<I18nProvider><BrowserSignInConfirm code={code} onClose={vi.fn()} onRoleChanged={vi.fn()} /></I18nProvider>);

    fireEvent.click(await screen.findByRole("button", { name: "17" }));

    expect(await screen.findByText(ru("browser.confirm.mismatch"))).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "42" })).not.toBeInTheDocument();
  });

  it("refuses a link somebody sent in a chat and cancels it at once", async () => {
    api.previewBrowserSignIn.mockResolvedValue(preview("sharedLink", { options: [], companyName: null }));
    const onClose = vi.fn();
    render(<I18nProvider><BrowserSignInConfirm code={code} onClose={onClose} onRoleChanged={vi.fn()} /></I18nProvider>);

    expect(await screen.findByRole("alert")).toHaveTextContent(ru("browser.confirm.sharedLink"));
    expect(api.declineBrowserSignIn).toHaveBeenCalledWith(code);
    expect(screen.queryByRole("button", { name: ru("browser.confirm.notMe") })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: ru("browser.confirm.close") }));
    expect(onClose).toHaveBeenCalled();
  });

  it("switches a member with another role to Business before the numbers appear", async () => {
    api.previewBrowserSignIn.mockResolvedValueOnce(preview("switchToBusiness")).mockResolvedValue(preview("ok"));
    marketplace.selectMarketplaceRole.mockResolvedValue(undefined);
    const onRoleChanged = vi.fn();
    render(<I18nProvider><BrowserSignInConfirm code={code} onClose={vi.fn()} onRoleChanged={onRoleChanged} /></I18nProvider>);

    expect(screen.queryByRole("button", { name: "42" })).not.toBeInTheDocument();
    fireEvent.click(await screen.findByRole("button", { name: ru("browser.confirm.switch") }));

    expect(await screen.findByRole("button", { name: "42" })).toBeInTheDocument();
    expect(marketplace.selectMarketplaceRole).toHaveBeenCalledWith("Business");
    expect(onRoleChanged).toHaveBeenCalled();
  });

  it("declines with Not me", async () => {
    api.previewBrowserSignIn.mockResolvedValue(preview("ok"));
    render(<I18nProvider><BrowserSignInConfirm code={code} onClose={vi.fn()} onRoleChanged={vi.fn()} /></I18nProvider>);

    fireEvent.click(await screen.findByRole("button", { name: ru("browser.confirm.notMe") }));

    expect(await screen.findByText(ru("browser.confirm.declinedNote"))).toBeInTheDocument();
    expect(api.declineBrowserSignIn).toHaveBeenCalledWith(code);
  });
});

describe("browser sign-in links and names", () => {
  it("reads the code only from a web_ start parameter", () => {
    expect(browserSignInCodeFrom(`web_${code}`)).toBe(code);
    expect(browserSignInCodeFrom("instagram_ads")).toBeNull();
    expect(browserSignInCodeFrom("web_short")).toBeNull();
    expect(browserSignInCodeFrom(undefined)).toBeNull();
  });

  it("names browsers and systems, with a word for the unknown ones", () => {
    expect(deviceLabel({ browser: "edge", os: "macos" }, ru)).toBe("Edge, macOS");
    expect(deviceLabel({ browser: "yandex", os: "other" }, ru)).toBe(ru("browser.yandex"));
    expect(deviceLabel({ browser: "other", os: "linux" }, ru)).toBe(`${ru("browser.otherBrowser")}, Linux`);
  });
});
