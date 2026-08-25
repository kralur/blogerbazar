import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { CampaignCard } from "../src/components/CampaignCard";
import { I18nProvider, translate } from "../src/i18n";

function renderCard(overrides: Record<string, unknown> = {}) {
  return render(<I18nProvider><CampaignCard campaign={{
    id: "campaign-a",
    title: "Coffee launch",
    businessName: "Coffee Co",
    businessAvatarUrl: null,
    city: null,
    categories: ["food", "beauty", "travel"],
    requirements: null,
    budgetFrom: null,
    budgetTo: null,
    deadline: null,
    isPromoted: false,
    ...overrides
  }} /></I18nProvider>);
}

describe("CampaignCard", () => {
  it("uses only catalog fields and does not invent facts for nullable values", () => {
    renderCard();

    const link = screen.getByRole("link", { name: translate("campaigns.openCampaignAria", { title: "Coffee launch" }, "ru") });
    expect(link).toHaveAttribute("href", "#/campaign/campaign-a");
    expect(screen.getByText("Coffee Co")).toBeInTheDocument();
    expect(screen.getByText(translate("taxonomy.category.food", undefined, "ru"))).toBeInTheDocument();
    expect(screen.queryByText(/0\s*сум/)).not.toBeInTheDocument();
    expect(screen.queryByText(translate("taxonomy.city.uzbekistan", undefined, "ru"))).not.toBeInTheDocument();
    expect(screen.queryByText(translate("common.applications", undefined, "ru"))).not.toBeInTheDocument();
  });

  it("renders the complete budget range on a dedicated fact row", () => {
    const { container } = renderCard({ budgetFrom: 500_000, budgetTo: 1_500_000, isPromoted: true });

    const budget = container.querySelector(".campaign-catalog-card__fact--budget dd");
    expect(budget).toHaveTextContent(/от 500.*до 1.*500.*сум/);
    expect(screen.getByText(translate("card.promoted", undefined, "ru"))).toBeInTheDocument();
  });

  it("renders min-only and max-only budgets without inventing a range", () => {
    const minOnly = renderCard({ budgetFrom: 500_000, budgetTo: null });
    expect(minOnly.container.querySelector(".campaign-catalog-card__fact--budget dd")).toHaveTextContent(/от 500.*сум/);
    minOnly.unmount();

    const maxOnly = renderCard({ budgetFrom: null, budgetTo: 1_500_000 });
    expect(maxOnly.container.querySelector(".campaign-catalog-card__fact--budget dd")).toHaveTextContent(/до 1.*500.*сум/);
  });
});
