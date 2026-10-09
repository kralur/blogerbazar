import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getCurrentPlatformUser: vi.fn(), getMyBloggerProfile: vi.fn(), getMyBusinessProfile: vi.fn(), selectMarketplaceRole: vi.fn() }));
vi.mock("../src/api/marketplace", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/marketplace")>()), ...api }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { selection: vi.fn(), success: vi.fn(), error: vi.fn(), warning: vi.fn(), impact: vi.fn() } }) }));

import { SwitchRoleHint } from "../src/components/SwitchRoleHint";

const action = (role: string) => translate("roleSwitch.action", { role: translate(role, undefined, "ru") }, "ru");

describe("Switch role hint", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getMyBloggerProfile.mockResolvedValue({ id: "blogger-a" });
    api.getMyBusinessProfile.mockResolvedValue({ id: "business-a" });
    api.selectMarketplaceRole.mockResolvedValue({});
  });

  it("offers the person's other role when an offer is opened from the wrong one", async () => {
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Business" });
    const reload = vi.fn();
    Object.defineProperty(window, "location", { configurable: true, value: { ...window.location, reload } });
    render(<I18nProvider><SwitchRoleHint /></I18nProvider>);

    fireEvent.click(await screen.findByRole("button", { name: action("profile.blogger") }));

    await waitFor(() => expect(api.selectMarketplaceRole).toHaveBeenCalledWith("Blogger"));
    expect(reload).toHaveBeenCalled();
    expect(screen.queryByRole("button", { name: action("profile.business") })).not.toBeInTheDocument();
  });

  it("says nothing when the person has no other profile", async () => {
    api.getCurrentPlatformUser.mockResolvedValue({ selectedMarketplaceRole: "Blogger" });
    api.getMyBusinessProfile.mockRejectedValue(new ApiError(404));
    render(<I18nProvider><SwitchRoleHint /></I18nProvider>);

    await waitFor(() => expect(api.getMyBusinessProfile).toHaveBeenCalled());
    expect(screen.queryByText(translate("roleSwitch.hint", undefined, "ru"))).not.toBeInTheDocument();
  });
});
