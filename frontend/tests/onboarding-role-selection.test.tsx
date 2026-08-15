import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const roleApi = vi.hoisted(() => ({ selectMarketplaceRole: vi.fn().mockResolvedValue(undefined) }));
const haptic = vi.hoisted(() => ({ selection: vi.fn() }));

vi.mock("../src/api/marketplace", () => ({
  selectMarketplaceRole: roleApi.selectMarketplaceRole
}));

vi.mock("../src/telegram/TelegramProvider", () => ({
  useTelegram: () => ({ haptic })
}));

import { Onboarding } from "../src/pages/Onboarding";

describe("role selection", () => {
  it("requires an explicit role choice and persists only the confirmed role", async () => {
    const user = userEvent.setup();
    const onRoleSelected = vi.fn();
    render(<I18nProvider><Onboarding onRoleSelected={onRoleSelected} /></I18nProvider>);

    const continueButton = screen.getByRole("button", { name: translate("onboarding.selectRole", undefined, "ru") });
    expect(continueButton).toBeDisabled();
    await user.click(continueButton);
    expect(roleApi.selectMarketplaceRole).not.toHaveBeenCalled();

    await user.click(screen.getByRole("radio", { name: /Бренд-фейс/i }));
    expect(screen.getByRole("radio", { name: /Бренд-фейс/i })).toHaveAttribute("aria-checked", "true");
    expect(continueButton).toBeEnabled();

    await user.click(continueButton);

    expect(roleApi.selectMarketplaceRole).toHaveBeenCalledWith("BrandFace");
    expect(onRoleSelected).toHaveBeenCalledWith("BrandFace");
    expect(haptic.selection).toHaveBeenCalledTimes(1);
  });

  it("keeps focus separate from selection and only activates the CTA after a role is chosen", async () => {
    const user = userEvent.setup();
    render(<I18nProvider><Onboarding /></I18nProvider>);

    const bloggerCard = screen.getByRole("radio", { name: /Блогер/i });
    const continueButton = screen.getByRole("button", { name: translate("onboarding.selectRole", undefined, "ru") });
    await user.tab();

    expect(bloggerCard).toHaveAttribute("aria-checked", "false");
    expect(bloggerCard).not.toHaveClass("ftue-role-card--selected");
    expect(continueButton).toBeDisabled();

    await user.click(bloggerCard);
    expect(bloggerCard).toHaveAttribute("aria-checked", "true");
    expect(bloggerCard).toHaveClass("ftue-role-card--selected");
    expect(continueButton).toBeEnabled();
  });
});
