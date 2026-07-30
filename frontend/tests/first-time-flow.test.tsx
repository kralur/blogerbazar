import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { App } from "../src/App";
import { I18nProvider, translate } from "../src/i18n";

const telegram = vi.hoisted(() => ({ isTelegram: false, setBackButtonHandler: vi.fn() }));

vi.mock("../src/telegram/TelegramProvider", () => ({
  useTelegram: () => telegram,
  telegramBridge: { initData: "" }
}));

describe("first-time flow", () => {
  it("blocks a direct Home route until the user acknowledges Telegram access", async () => {
    const user = userEvent.setup();
    window.location.hash = "#/";
    render(<I18nProvider><App /></I18nProvider>);

    expect(await screen.findByText(translate("firstRun.welcomeTitle", undefined, "ru"))).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("firstRun.start", undefined, "ru") }));

    expect(await screen.findByText(translate("firstRun.telegramOutsideTitle", undefined, "ru"))).toBeInTheDocument();
    expect(screen.queryByText(translate("home.title", undefined, "ru"))).not.toBeInTheDocument();
  });
});
