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

  it("renders real budget variants and a small promoted indicator", () => {
    renderCard({ budgetFrom: 100_000, budgetTo: 300_000, isPromoted: true });

    expect(screen.getByText(/от 100.*до 300.*сум/)).toBeInTheDocument();
    expect(screen.getByText(translate("card.promoted", undefined, "ru"))).toBeInTheDocument();
  });
});
