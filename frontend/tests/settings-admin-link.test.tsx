import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getCurrentPlatformUser: vi.fn(), setBotTips: vi.fn(), peekCurrentPlatformUser: vi.fn(() => null) }));
vi.mock("../src/api/marketplace", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/marketplace")>()), ...api }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { impact: vi.fn(), success: vi.fn(), selection: vi.fn(), error: vi.fn(), warning: vi.fn() } }) }));
vi.mock("../src/components/AccountActions", () => ({ AccountActions: () => null }));
vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => null }));
vi.mock("../src/components/ui", () => ({ BottomNav: () => null, Toast: ({ message }: { message: string }) => message ? <p role="status">{message}</p> : null }));
vi.mock("../src/components/ManagementBackLink", () => ({ ManagementBackLink: () => null }));

import { Settings } from "../src/pages/Settings";

const ru = (key: string) => translate(key, undefined, "ru");
const user = (role: number) => ({ telegramUserId: 1, firstName: "Umid", role, isBlocked: false });

describe("Settings admin link", () => {
  beforeEach(() => { vi.clearAllMocks(); api.peekCurrentPlatformUser.mockReturnValue(null); });

  it("shows the admin panel to the owner", async () => {
    api.getCurrentPlatformUser.mockResolvedValue(user(4));
    render(<I18nProvider><Settings /></I18nProvider>);

    expect(await screen.findByRole("link", { name: new RegExp(ru("settings.adminPanel")) })).toHaveAttribute("href", "#/admin");
  });

  // QA 2026-10-15: the rows used to appear a second after the screen; the account loaded at start draws them at once.
  it("draws the tips switch and admin link at once from the account already loaded", () => {
    api.peekCurrentPlatformUser.mockReturnValue({ ...user(4), tipsEnabled: false });
    api.getCurrentPlatformUser.mockReturnValue(new Promise(() => {}));
    render(<I18nProvider><Settings /></I18nProvider>);

    expect(screen.getByRole("link", { name: new RegExp(ru("settings.adminPanel")) })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: ru("settings.tipsOff") })).toHaveAttribute("aria-pressed", "true");
  });

  it("hides it from a regular member", async () => {
    api.getCurrentPlatformUser.mockResolvedValue(user(0));
    render(<I18nProvider><Settings /></I18nProvider>);

    await vi.waitFor(() => expect(api.getCurrentPlatformUser).toHaveBeenCalled());
    expect(screen.queryByRole("link", { name: new RegExp(ru("settings.adminPanel")) })).not.toBeInTheDocument();
  });

  it("switches bot tips off and rolls back when saving fails", async () => {
    api.getCurrentPlatformUser.mockResolvedValue({ ...user(0), tipsEnabled: true });
    api.setBotTips.mockResolvedValueOnce({ ...user(0), tipsEnabled: false }).mockRejectedValueOnce(new Error("offline"));
    render(<I18nProvider><Settings /></I18nProvider>);

    const off = await screen.findByRole("button", { name: ru("settings.tipsOff") });
    expect(screen.getByRole("button", { name: ru("settings.tipsOn") })).toHaveAttribute("aria-pressed", "true");
    fireEvent.click(off);
    await waitFor(() => expect(api.setBotTips).toHaveBeenCalledWith(false));
    expect(off).toHaveAttribute("aria-pressed", "true");

    fireEvent.click(screen.getByRole("button", { name: ru("settings.tipsOn") }));
    expect(await screen.findByRole("status")).toHaveTextContent(ru("settings.tipsFailed"));
    expect(off).toHaveAttribute("aria-pressed", "true");
  });
});
