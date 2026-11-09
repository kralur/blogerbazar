import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { SeasonLeaves } from "../src/components/SeasonLeaves";
import { SeasonNote } from "../src/components/SeasonNote";
import { activeSeason, applySeason, getSeasonPreference, seasonOf, setSeasonPreference } from "../src/lib/season";

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

  it("shows autumn in autumn and the plain design in a season without artefacts", () => {
    expect(activeSeason(new Date(2026, 9, 10))).toBe("autumn");
    expect(activeSeason(new Date(2026, 11, 10))).toBeNull();
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

  it("draws leaves only while the season is on", () => {
    const { container, rerender } = render(<SeasonLeaves variant="falling" />);
    expect(container.querySelector(".season-leaves--falling")).toBeInTheDocument();
    setSeasonPreference("off");
    rerender(<SeasonLeaves variant="falling" />);
    expect(container.querySelector(".season-leaves")).toBeNull();
    rerender(<SeasonLeaves force variant="static" />);
    expect(container.querySelector(".season-leaves--static")).toBeInTheDocument();
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
