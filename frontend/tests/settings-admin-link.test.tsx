import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getCurrentPlatformUser: vi.fn() }));
vi.mock("../src/api/marketplace", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/marketplace")>()), ...api }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { impact: vi.fn(), success: vi.fn(), selection: vi.fn(), error: vi.fn(), warning: vi.fn() } }) }));
vi.mock("../src/components/AccountActions", () => ({ AccountActions: () => null }));
vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => null }));
vi.mock("../src/components/ui", () => ({ BottomNav: () => null }));
vi.mock("../src/components/ManagementBackLink", () => ({ ManagementBackLink: () => null }));

import { Settings } from "../src/pages/Settings";

const ru = (key: string) => translate(key, undefined, "ru");
const user = (role: number) => ({ telegramUserId: 1, firstName: "Umid", role, isBlocked: false });

describe("Settings admin link", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows the admin panel to the owner", async () => {
    api.getCurrentPlatformUser.mockResolvedValue(user(4));
    render(<I18nProvider><Settings /></I18nProvider>);

    expect(await screen.findByRole("link", { name: new RegExp(ru("settings.adminPanel")) })).toHaveAttribute("href", "#/admin");
  });

  it("hides it from a regular member", async () => {
    api.getCurrentPlatformUser.mockResolvedValue(user(0));
    render(<I18nProvider><Settings /></I18nProvider>);

    await vi.waitFor(() => expect(api.getCurrentPlatformUser).toHaveBeenCalled());
    expect(screen.queryByRole("link", { name: new RegExp(ru("settings.adminPanel")) })).not.toBeInTheDocument();
  });
});
