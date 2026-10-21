import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { App } from "../src/App";
import { dictionaries, I18nProvider } from "../src/i18n";

const ru = (key: string) => dictionaries.ru[key];
const uz = (key: string) => dictionaries.uz[key];

async function renderLanding(botUsername = "BloggerBazarBot") {
  vi.resetModules();
  vi.stubEnv("VITE_BOT_USERNAME", botUsername);
  const { App: Fresh } = await import("../src/App");
  const { I18nProvider: Provider } = await import("../src/i18n");
  return render(<Provider><Fresh /></Provider>);
}

describe("landing", () => {
  it("opens the bot with a start label for each role", async () => {
    await renderLanding();

    expect(screen.getByRole("link", { name: ru("hero.blogger") })).toHaveAttribute("href", "https://t.me/BloggerBazarBot?start=site_blogger");
    expect(screen.getByRole("link", { name: ru("hero.business") })).toHaveAttribute("href", "https://t.me/BloggerBazarBot?start=site_business");
    for (const link of screen.getAllByRole("link", { name: new RegExp(ru("nav.open")) })) {
      expect(link.getAttribute("href")).toMatch(/^https:\/\/t\.me\/BloggerBazarBot\?start=site(_blogger|_business)?$/);
      expect(link).toHaveAttribute("rel", "noopener noreferrer");
    }
  });

  it("never builds a link from an invalid bot name", async () => {
    await renderLanding("bad name/../x");

    expect(screen.getByRole("link", { name: ru("hero.blogger") })).toHaveAttribute("href", "https://t.me/");
  });

  it("switches to Uzbek and remembers it", () => {
    render(<I18nProvider><App /></I18nProvider>);

    fireEvent.click(screen.getByRole("button", { name: ru("lang.uz") }));

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(uz("hero.title"));
    expect(document.documentElement.lang).toBe("uz");
    expect(localStorage.getItem("bloggerbazar.landing.language")).toBe("uz");
  });

  it("starts in Uzbek for an Uzbek browser", () => {
    vi.spyOn(navigator, "language", "get").mockReturnValue("uz-UZ");
    render(<I18nProvider><App /></I18nProvider>);

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(uz("hero.title"));
  });

  it("shows the business steps first and the blogger steps on their tab, also with the arrow keys", () => {
    render(<I18nProvider><App /></I18nProvider>);
    const panel = screen.getByRole("tabpanel");
    expect(within(panel).getByText(ru("how.business.1.title"))).toBeInTheDocument();

    const businessTab = screen.getByRole("tab", { name: ru("how.tab.business") });
    fireEvent.keyDown(businessTab, { key: "ArrowRight" });

    const bloggerTab = screen.getByRole("tab", { name: ru("how.tab.blogger") });
    expect(bloggerTab).toHaveAttribute("aria-selected", "true");
    expect(bloggerTab).toHaveFocus();
    expect(within(screen.getByRole("tabpanel")).getByText(ru("how.blogger.1.title"))).toBeInTheDocument();
  });

  it("toggles the theme and keeps the choice", () => {
    document.documentElement.dataset.theme = "light";
    render(<I18nProvider><App /></I18nProvider>);

    fireEvent.click(screen.getByRole("button", { name: ru("theme.toDark") }));

    expect(document.documentElement.dataset.theme).toBe("dark");
    expect(localStorage.getItem("bloggerbazar.landing.theme")).toBe("dark");
    expect(screen.getByRole("button", { name: ru("theme.toLight") })).toBeInTheDocument();
  });

  it("answers that it is free", () => {
    render(<I18nProvider><App /></I18nProvider>);

    expect(screen.getByText(ru("faq.price.question"))).toBeInTheDocument();
    expect(screen.getByText(ru("faq.price.answer"))).toBeInTheDocument();
  });
});

describe("dictionaries", () => {
  // Keys the page builds from parts (steps, reasons, audiences, questions) exist in both languages.
  const built = [
    ...["business", "blogger"].flatMap((side) => [1, 2, 3].flatMap((number) => [`how.${side}.${number}.title`, `how.${side}.${number}.text`, `how.tab.${side}`])),
    ...["moderation", "direct", "reviews", "telegram"].flatMap((key) => [`why.${key}.title`, `why.${key}.text`]),
    ...["blogger", "brandFace", "business"].flatMap((key) => [`who.${key}.title`, `who.${key}.text`]),
    ...["price", "check", "brandFace", "contact", "language", "problem"].flatMap((key) => [`faq.${key}.question`, `faq.${key}.answer`]),
    "lang.ru", "lang.uz"
  ];

  it.each(built)("has %s in Russian and Uzbek", (key) => {
    expect(dictionaries.ru[key]).toBeTruthy();
    expect(dictionaries.uz[key]).toBeTruthy();
  });

  it("has the same keys in both languages", () => {
    expect(Object.keys(dictionaries.uz).sort()).toEqual(Object.keys(dictionaries.ru).sort());
  });
});
