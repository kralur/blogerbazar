import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const haptic = vi.hoisted(() => ({ impact: vi.fn(), selection: vi.fn() }));

vi.mock("../src/telegram/TelegramProvider", () => ({
  useTelegram: () => ({ haptic })
}));

import { BottomNav } from "../src/components/ui";

describe("BottomNav", () => {
  it("returns the user home through the central action", async () => {
    const user = userEvent.setup();
    window.location.hash = "#/search";
    render(<I18nProvider><BottomNav /></I18nProvider>);

    await user.click(screen.getByRole("button", { name: translate("ui.home", undefined, "ru") }));

    expect(window.location.hash).toBe("#/");
    expect(haptic.impact).toHaveBeenCalledTimes(1);
  });
});
