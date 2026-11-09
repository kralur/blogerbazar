import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { SeasonArt, hasSeasonArt } from "../src/components/SeasonArt";
import { SeasonNote } from "../src/components/SeasonNote";
import { activeSeason, applySeason, drawnSeasons, getSeasonPreference, seasonOf, setSeasonPreference } from "../src/lib/season";

const ru = (key: string) => translate(key, undefined, "ru");

// D63: the season follows the calendar; the person can switch the artefacts off on their phone.
describe("season", () => {
  beforeEach(() => { localStorage.clear(); vi.useFakeTimers(); vi.setSystemTime(new Date(2026, 10, 9)); });
  afterEach(() => { vi.useRealTimers(); delete document.documentElement.dataset.season; });

  it("maps months to seasons", () => {
    expect(seasonOf(new Date(2026, 8, 1))).toBe("autumn");
    expect(seasonOf(new Date(2026, 10, 30))).toBe("autumn");
    expect(seasonOf(new Date(2026, 11, 1))).toBe("winter");
    expect(seasonOf(new Date(2027, 1, 28))).toBe("winter");
    expect(seasonOf(new Date(2027, 2, 1))).toBe("spring");
    expect(seasonOf(new Date(2027, 5, 15))).toBe("summer");
  });

  it("follows the calendar by default", () => {
    expect(activeSeason(new Date(2026, 9, 10))).toBe("autumn");
    expect(activeSeason(new Date(2026, 11, 10))).toBe("winter");
    expect(activeSeason(new Date(2027, 3, 10))).toBe("spring");
    expect(activeSeason(new Date(2027, 6, 10))).toBe("summer");
  });

  // Package I: the person can pin any season; it then stays whatever the calendar says.
  it("keeps a pinned season through the year and marks <html> with it", () => {
    setSeasonPreference("summer");
    expect(getSeasonPreference()).toBe("summer");
    expect(activeSeason(new Date(2026, 10, 9))).toBe("summer");
    expect(activeSeason(new Date(2027, 0, 9))).toBe("summer");
    expect(document.documentElement.dataset.season).toBe("summer");
    const { container } = render(<SeasonArt variant="falling" />);
    expect(container.querySelector(".season-art--summer.season-art--rising")).toBeInTheDocument();
  });

  // Package H: a season is added in one place; every season the calendar can switch on has a drawing.
  it("has a drawing for every drawn season", () => {
    for (const season of drawnSeasons) expect(hasSeasonArt(season)).toBe(true);
  });

  it("is off when the person switched it off, and marks <html> accordingly", () => {
    applySeason();
    expect(document.documentElement.dataset.season).toBe("autumn");
    setSeasonPreference("off");
    expect(getSeasonPreference()).toBe("off");
    expect(activeSeason()).toBeNull();
    expect(document.documentElement.dataset.season).toBeUndefined();
    setSeasonPreference("auto");
    expect(document.documentElement.dataset.season).toBe("autumn");
  });

  it("draws the season's pieces only while the season is on", () => {
    const { container, rerender } = render(<SeasonArt variant="falling" />);
    expect(container.querySelector(".season-art--falling.season-art--autumn")).toBeInTheDocument();
    setSeasonPreference("off");
    rerender(<SeasonArt variant="falling" />);
    expect(container.querySelector(".season-art")).toBeNull();
    rerender(<SeasonArt force variant="static" />);
    expect(container.querySelector(".season-art--static.season-art--autumn")).toBeInTheDocument();
    rerender(<SeasonArt season="spring" variant="static" />);
    expect(container.querySelector(".season-art--static.season-art--spring")).toBeInTheDocument();
  });

  it("draws snow in winter on the same screens, with no screen changed", () => {
    vi.setSystemTime(new Date(2026, 11, 5));
    const { container, rerender } = render(<SeasonArt variant="falling" />);
    expect(container.querySelector(".season-art--falling.season-art--winter")).toBeInTheDocument();
    expect(container.querySelectorAll(".season-art--falling > g").length).toBe(8);
    rerender(<SeasonArt variant="static" />);
    expect(container.querySelector(".season-art--static.season-art--winter")).toBeInTheDocument();
  });

  it("lets the Home note switch the season off", () => {
    render(<I18nProvider><SeasonNote /></I18nProvider>);
    expect(screen.getByText(ru("home.season.autumn"))).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: ru("home.seasonOff") }));
    expect(getSeasonPreference()).toBe("off");
    expect(screen.queryByText(ru("home.season.autumn"))).toBeNull();
  });

  it("hides the note for good without touching the season", () => {
    const { unmount } = render(<I18nProvider><SeasonNote /></I18nProvider>);
    fireEvent.click(screen.getByRole("button", { name: ru("home.seasonHide") }));
    expect(screen.queryByText(ru("home.season.autumn"))).toBeNull();
    expect(getSeasonPreference()).toBe("auto");
    unmount();
    render(<I18nProvider><SeasonNote /></I18nProvider>);
    expect(screen.queryByText(ru("home.season.autumn"))).toBeNull();
  });
});

// A piece can be caught and carried (owner, 2026-11-09); released, it keeps falling from where the finger left it.
describe("dragging a piece", () => {
  beforeEach(() => { localStorage.clear(); vi.useFakeTimers(); vi.setSystemTime(new Date(2026, 10, 9)); });
  afterEach(() => { vi.useRealTimers(); });

  it("follows the pointer and resumes the fall on release", () => {
    const { container } = render(<SeasonArt variant="falling" />);
    const leaf = container.querySelector(".season-art--falling > g") as SVGGElement;
    fireEvent.pointerDown(leaf, { clientX: 40, clientY: 100, pointerId: 1 });
    expect(leaf.style.animation).toBe("none");
    fireEvent.pointerMove(leaf, { clientX: 90, clientY: 300, pointerId: 1 });
    expect(leaf.style.transform).toContain("translate(");
    fireEvent.pointerUp(leaf, { clientX: 90, clientY: 300, pointerId: 1 });
    expect(leaf.style.animation).toBe("");
    expect(leaf.style.transform).toBe("");
    expect(leaf.style.animationDelay).toMatch(/^-\d/);
  });
});
