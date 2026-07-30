import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { I18nProvider, translate, useI18n } from "../src/i18n";

function LanguageProbe() {
  const { language, setLanguage, t } = useI18n();
  return <>
    <p>{t("home.title")}</p>
    <output>{language}</output>
    <button onClick={() => setLanguage("uz")} type="button">switch</button>
  </>;
}

describe("i18n", () => {
  it("updates translated content without a page reload", async () => {
    const user = userEvent.setup();
    render(<I18nProvider><LanguageProbe /></I18nProvider>);

    expect(screen.getByText(translate("home.title", undefined, "ru"))).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "switch" }));

    expect(screen.getByText(translate("home.title", undefined, "uz"))).toBeInTheDocument();
    expect(screen.getByText("uz")).toBeInTheDocument();
  });
});
