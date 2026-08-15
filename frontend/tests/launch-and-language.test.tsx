import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { LanguageSwitcher } from "../src/components/LanguageSwitcher";
import { LaunchScreen } from "../src/components/LaunchScreen";
import { currentLanguage, I18nProvider, useI18n } from "../src/i18n";
import { TelegramAuthorization } from "../src/pages/TelegramAuthorization";

function LanguagePreview() {
  const { language, t } = useI18n();
  return <><LanguageSwitcher /><p>{language}:{t("firstRun.start")}</p></>;
}

describe("launch and language foundation", () => {
  it("uses the official launch screen instead of the legacy splash markup", () => {
    render(<I18nProvider><LaunchScreen /></I18nProvider>);

    expect(screen.getByRole("main", { name: "Загружаем BloggerBazar…" })).toHaveClass("launch-screen");
    expect(document.querySelector(".splash-screen")).not.toBeInTheDocument();
    expect(screen.getByRole("img", { name: "BloggerBazar" })).toHaveClass("launch-screen__logo");
    expect(screen.queryByRole("group", { name: "Язык интерфейса" })).not.toBeInTheDocument();
  });

  it("switches RU and UZ immediately without a route change", async () => {
    const user = userEvent.setup();
    render(<I18nProvider><LanguagePreview /></I18nProvider>);

    await user.click(screen.getByRole("button", { name: "O‘zbekcha" }));

    expect(screen.getByText("uz:Boshlash")).toBeInTheDocument();
    expect(window.location.hash).toBe("#/");
    expect(localStorage.getItem("bloggerbazar.language")).toBe("uz");
  });

  it("uses a saved explicit language before Telegram and only accepts RU or UZ", () => {
    window.Telegram = { WebApp: { initDataUnsafe: { user: { id: 1, language_code: "uz" } } } };
    localStorage.setItem("bloggerbazar.language", "ru");
    expect(currentLanguage()).toBe("ru");

    localStorage.removeItem("bloggerbazar.language");
    expect(currentLanguage()).toBe("uz");

    window.Telegram.WebApp!.initDataUnsafe!.user!.language_code = "en";
    expect(currentLanguage()).toBe("ru");
  });

  it("keeps the saved language when the application is initialized again", async () => {
    const user = userEvent.setup();
    const first = render(<I18nProvider><LanguagePreview /></I18nProvider>);
    await user.click(screen.getByRole("button", { name: "O‘zbekcha" }));
    first.unmount();

    render(<I18nProvider><LanguagePreview /></I18nProvider>);
    expect(screen.getByText("uz:Boshlash")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "O‘zbekcha" })).toHaveAttribute("aria-pressed", "true");
  });

  it("shows the shared switcher on Telegram Authorization with localized state", () => {
    render(<I18nProvider><TelegramAuthorization failed={false} isTelegram={false} loading={false} onContinue={() => undefined} /></I18nProvider>);

    expect(screen.getByRole("group", { name: "Язык интерфейса" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Русский" })).toHaveAttribute("aria-pressed", "true");
  });
});
