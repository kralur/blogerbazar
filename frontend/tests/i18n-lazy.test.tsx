import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

// A fresh copy of the module: the shared test setup preloads Uzbek for every other test.
async function freshI18n() {
  vi.resetModules();
  return import("../src/i18n");
}

describe("Uzbek dictionary on demand", () => {
  it("starts with Russian only, answers in Russian until Uzbek has loaded, then in Uzbek", async () => {
    const i18n = await freshI18n();
    expect(i18n.isLanguageLoaded("ru")).toBe(true);
    expect(i18n.isLanguageLoaded("uz")).toBe(false);
    const russian = i18n.translate("home.title", undefined, "ru");
    expect(i18n.translate("home.title", undefined, "uz")).toBe(russian);
    // Shared city and category names ship with the app in both languages.
    expect(i18n.translate("taxonomy.city.tashkent", undefined, "uz")).not.toBe(i18n.translate("taxonomy.city.tashkent", undefined, "ru"));

    await i18n.loadLanguage("uz");
    expect(i18n.isLanguageLoaded("uz")).toBe(true);
    expect(i18n.translate("home.title", undefined, "uz")).not.toBe(russian);
    await expect(i18n.loadLanguage("ru")).resolves.toBeUndefined();
  });

  it("switches the screens to Uzbek once the dictionary has arrived", async () => {
    const i18n = await freshI18n();
    function Probe() {
      const { language, setLanguage, t } = i18n.useI18n();
      return <><p>{t("home.title")}</p><output>{language}</output><button onClick={() => setLanguage("uz")} type="button">switch</button></>;
    }
    const user = userEvent.setup();
    render(<i18n.I18nProvider><Probe /></i18n.I18nProvider>);
    await user.click(screen.getByRole("button", { name: "switch" }));
    await screen.findByText(i18n.translate("home.title", undefined, "uz"));
    expect(screen.getByText("uz")).toBeInTheDocument();
    expect(localStorage.getItem("bloggerbazar.language")).toBe("uz");
  });
});
