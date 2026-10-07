import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { categoryLabel, I18nProvider, translate, useI18n } from "../src/i18n";

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

describe("russian plural forms", () => {
  it.each([[1, "1 отзыв"], [2, "2 отзыва"], [4, "4 отзыва"], [5, "5 отзывов"], [11, "11 отзывов"], [12, "12 отзывов"], [21, "21 отзыв"], [22, "22 отзыва"], [112, "112 отзывов"], [0, "0 отзывов"]])("%i -> %s", (count, expected) => {
    expect(translate("common.reviews", { count }, "ru")).toBe(expected);
  });

  it("keeps the single Uzbek form", () => {
    expect(translate("common.deals", { count: 1 }, "uz")).toBe(translate("common.deals", { count: 5 }, "uz").replace("5", "1"));
  });
});

describe("category labels", () => {
  it("shows a typed-in category as the user wrote it", () => {
    expect(categoryLabel("other:психология", "ru")).toBe("психология");
  });

  it("never shows a raw dictionary key", () => {
    expect(categoryLabel("unknown-category", "ru")).toBe(translate("common.notSpecified", undefined, "ru"));
    expect(categoryLabel("beauty", "ru")).toBe(translate("taxonomy.category.beauty", undefined, "ru"));
  });
});
