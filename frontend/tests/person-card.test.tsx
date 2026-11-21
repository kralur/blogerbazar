import { render } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../src/i18n";

vi.mock("../src/components/FavoriteButton", () => ({ FavoriteButton: ({ className }: { className?: string }) => <span className={className}><button type="button">save</button></span> }));

import { BloggerCard } from "../src/components/BloggerCard";
import { BrandFaceCard } from "../src/components/BrandFaceCard";

// Owner, 2026-11-21: a person's card is the same everywhere, in Search, on Home and in Saved, for a blogger and for
// a brand face. Before this there were three looks: a short row on Home, a row for bloggers, a block for brand faces.
const blogger = { id: "blogger-a", name: "Madina", city: "tashkent", categories: ["beauty", "food", "travel"], totalFollowers: 52000, reviewsCount: 0, completedDealsCount: 0, platform: "Instagram", reelsPrice: 250000, isPromoted: true };
const brandFace = { id: "face-a", name: "Dilnoza", city: "tashkent", categories: ["beauty", "food", "travel"], languages: ["ru", "uz"], collaborationPrice: 700000, avatarUrl: null, isPromoted: true, age: 24, gender: "female" };

// The skeleton of a card: its elements and classes, without the texts.
function shape(element: Element): string {
  const children = [...element.children].map(shape).join("");
  return `<${element.tagName.toLowerCase()} class="${element.className}">${children}</${element.tagName.toLowerCase()}>`;
}

function card(node: React.ReactNode) {
  const { container } = render(<I18nProvider>{node}</I18nProvider>);
  return container.querySelector("article")!;
}

describe("one card for a person", () => {
  it("a brand face is built exactly like a blogger", () => {
    const bloggerCard = card(<BloggerCard blogger={blogger} />);
    const brandFaceCard = card(<BrandFaceCard profile={brandFace} />);

    expect(bloggerCard.className).toContain("person-card");
    expect(shape(brandFaceCard)).toBe(shape(bloggerCard));
  });

  it("Home shows the same card as Search", () => {
    expect(shape(card(<BloggerCard blogger={blogger} variant="home" />))).toBe(shape(card(<BloggerCard blogger={blogger} />)));
    expect(shape(card(<BrandFaceCard profile={brandFace} variant="home" />))).toBe(shape(card(<BrandFaceCard profile={brandFace} />)));
  });

  it("a brand face shows who the person is, the city and the languages in the facts line", () => {
    const facts = card(<BrandFaceCard profile={brandFace} />).querySelector(".person-card__facts")!;
    expect(facts.textContent).toMatch(/Женщина · 24 .* · Ташкент · Русский, Узбекский/);
  });
});
