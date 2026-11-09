import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { App } from "../src/App";
import { I18nProvider, translate } from "../src/i18n";
import { OnboardingSuccess } from "../src/pages/OnboardingSuccess";
import { TelegramAuthorization } from "../src/pages/TelegramAuthorization";

const telegram = vi.hoisted(() => ({ isTelegram: false, setBackButtonHandler: vi.fn() }));

vi.mock("../src/telegram/TelegramProvider", () => ({
  useTelegram: () => telegram,
  telegramBridge: { initData: "", requestBotMessages: () => undefined }
}));

describe("first-time flow", () => {
  // D64 stage B: outside Telegram the app is the companies' browser sign-in; Home needs a browser session first.
  it("blocks a direct Home route outside Telegram and offers the browser sign-in", async () => {
    window.location.hash = "#/";
    render(<I18nProvider><App /></I18nProvider>);

    expect(await screen.findByRole("heading", { name: translate("browser.title", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: translate("browser.signIn", undefined, "ru") })).toBeInTheDocument();
    expect(screen.queryByText(translate("firstRun.welcomeSubtitle", undefined, "ru"))).not.toBeInTheDocument();
    expect(screen.queryByText(translate("home.title", undefined, "ru"))).not.toBeInTheDocument();
  });

  it("keeps the Telegram continue action text-only and reserves the refresh icon for outside Telegram", () => {
    const { rerender } = render(<I18nProvider><TelegramAuthorization failed={false} isTelegram loading={false} onContinue={vi.fn()} /></I18nProvider>);
    const continueButton = screen.getByRole("button", { name: translate("firstRun.continueTelegram", undefined, "ru") });
    expect(continueButton.querySelector("svg")).toBeNull();

    rerender(<I18nProvider><TelegramAuthorization failed={false} isTelegram={false} loading={false} onContinue={vi.fn()} /></I18nProvider>);
    const retryButton = screen.getByRole("button", { name: translate("common.retry", undefined, "ru") });
    expect(retryButton.querySelector("svg")).not.toBeNull();
  });

  it("uses the neutral FTUE success layout and a text-only Home action", () => {
    render(<I18nProvider><OnboardingSuccess onContinue={vi.fn()} /></I18nProvider>);
    const action = screen.getByRole("button", { name: translate("firstRun.goHome", undefined, "ru") });
    expect(action.querySelector("svg")).toBeNull();
    expect(document.querySelector(".ftue-success")).toBeInTheDocument();
    expect(document.querySelector(".ftue-success__indicator")).toBeInTheDocument();
  });
});
