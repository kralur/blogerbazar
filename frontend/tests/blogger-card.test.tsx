import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

vi.mock("../src/components/FavoriteButton", () => ({ FavoriteButton: () => <button type="button">favorite</button> }));

import { BloggerCard } from "../src/components/BloggerCard";

const ru = (key: string) => translate(key, undefined, "ru");
const blogger = { id: "blogger-a", name: "Madina", city: "tashkent", categories: ["beauty", "food", "travel"], totalFollowers: 52000, reviewsCount: 0, completedDealsCount: 0 };

describe("BloggerCard", () => {
  it("shows promotion in the chip row, not next to the name and favorite button", () => {
    const { container } = render(<I18nProvider><BloggerCard blogger={{ ...blogger, isPromoted: true }} /></I18nProvider>);

    const promoted = screen.getByText(ru("card.promoted"));
    expect(promoted.closest(".catalog-blogger-card__categories")).not.toBeNull();
    expect(container.querySelector(".catalog-blogger-card__name-row")).not.toHaveTextContent(ru("card.promoted"));
  });

  it("shows the lowest known price as a starting price and omits unknown facts", () => {
    const { rerender } = render(<I18nProvider><BloggerCard blogger={{ ...blogger, storiesPrice: 300000, reelsPrice: 250000, engagementRate: 8.4 }} /></I18nProvider>);
    expect(screen.getByText(/^250.*000 сум$/)).toBeInTheDocument();
    expect(screen.getByText(translate("card.priceFrom", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getByText(ru("search.er"))).toBeInTheDocument();

    rerender(<I18nProvider><BloggerCard blogger={blogger} /></I18nProvider>);
    expect(screen.queryByText(ru("search.er"))).not.toBeInTheDocument();
    expect(screen.queryByText(ru("card.priceFrom"))).not.toBeInTheDocument();
    expect(screen.queryByText("—")).not.toBeInTheDocument();
  });

  it("uses the same layout on Home rails", () => {
    const { container } = render(<I18nProvider><BloggerCard blogger={blogger} variant="home" /></I18nProvider>);
    expect(container.querySelector(".catalog-blogger-card.catalog-card--rail")).not.toBeNull();
    expect(screen.getByRole("link", { name: translate("home.openBlogger", { name: "Madina" }, "ru") })).toHaveAttribute("href", "#/blogger/blogger-a");
  });
});
