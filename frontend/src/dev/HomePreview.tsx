import { useEffect } from "react";
import type { MarketplaceHome, MarketplaceRole } from "../api/marketplace";
import { FavoritesProvider } from "../features/favorites/FavoritesProvider";
import { Home } from "../pages/Home";

const previewData: MarketplaceHome = {
  promotedBloggers: [
    { id: "promoted-blogger", name: "Madina Karimova", city: "tashkent", categories: ["beauty", "lifestyle"], totalFollowers: 52000, averageReach: 112000, engagementRate: 9.1, storiesPrice: 350000, reelsPrice: 550000, barterEnabled: false, isVerified: true, isPromoted: true, rating: 4.9, reviewsCount: 128, completedDealsCount: 128 },
    { id: "promoted-blogger-two", name: "Asadbek N.", city: "samarkand", categories: ["food", "travel"], totalFollowers: 32100, averageReach: 85000, engagementRate: 7.2, storiesPrice: 450000, reelsPrice: 600000, barterEnabled: false, isVerified: true, isPromoted: true, rating: 4.8, reviewsCount: 96, completedDealsCount: 72 }
  ],
  promotedCampaigns: [
    { id: "promoted-campaign", businessId: "business", businessName: "Andijan Coffee", title: "Summer campaign", description: "Native promotion for a new drinks collection.", city: "andijan", categories: ["food", "lifestyle"], requirements: ["Reels"], budgetFrom: 1500000, budgetTo: 2500000, deadline: "2026-08-25T00:00:00Z", isPromoted: true, status: 1, applicationsCount: 12, createdAtUtc: "2026-08-01T00:00:00Z" },
    { id: "promoted-campaign-two", businessId: "business-two", businessName: "Ziyo School", title: "Product launch", description: "Introduce a new online learning product to your audience.", city: "tashkent", categories: ["technology", "finance"], requirements: ["Stories"], budgetFrom: 900000, budgetTo: 1200000, deadline: "2026-08-30T00:00:00Z", isPromoted: true, status: 1, applicationsCount: 6, createdAtUtc: "2026-08-02T00:00:00Z" }
  ],
  topRatedBloggers: [{ id: "top-blogger", name: "Diana Raximova", city: "tashkent", categories: ["fashion", "beauty"], totalFollowers: 71700, averageReach: 98000, engagementRate: 8.4, storiesPrice: 300000, reelsPrice: 500000, barterEnabled: false, isVerified: true, isPromoted: false, rating: 4.9, reviewsCount: 74, completedDealsCount: 103 }],
  newBloggers: [{ id: "new-blogger", name: "Rustam", city: "fergana", categories: ["fashion", "food"], totalFollowers: 83400, averageReach: 292005, engagementRate: 5.83, storiesPrice: 264287, reelsPrice: 474287, barterEnabled: true, isVerified: true, isPromoted: false, rating: 5, reviewsCount: 1, completedDealsCount: 1 }],
  newBrandFaces: [{ id: "brand-face", name: "Malika S.", city: "tashkent", languages: ["ru", "uz"], categories: ["fashion", "beauty"], experience: "Fashion", collaborationPrice: 400000, avatarUrl: null, isPromoted: false }],
  popularBusinesses: [],
  categories: ["lifestyle", "food", "beauty", "technology", "sport", "travel", "finance", "gaming", "fashion"],
  statistics: { approvedBloggers: 380, companies: 134, activeCampaigns: 336, completedDeals: 128, averageRating: 4.8 }
};

function queryRole(): MarketplaceRole {
  const value = new URLSearchParams(window.location.search).get("previewRole");
  return value === "Blogger" || value === "BrandFace" ? value : "Business";
}

export function HomePreview() {
  const params = new URLSearchParams(window.location.search);
  const state = params.get("previewState");
  const theme = params.get("previewTheme");

  useEffect(() => {
    if (theme !== "dark") return;
    const root = document.documentElement;
    const applyDarkTheme = () => {
      if (root.dataset.telegramTheme !== "dark") root.dataset.telegramTheme = "dark";
    };
    const observer = new MutationObserver(applyDarkTheme);
    observer.observe(root, { attributes: true, attributeFilter: ["data-telegram-theme"] });
    applyDarkTheme();
    return () => {
      observer.disconnect();
      delete root.dataset.telegramTheme;
    };
  }, [theme]);

  const data = state === "empty"
    ? { ...previewData, promotedBloggers: [], promotedCampaigns: [], topRatedBloggers: [], newBloggers: [], newBrandFaces: [], categories: [], statistics: { approvedBloggers: 0, companies: 0, activeCampaigns: 0, completedDeals: 0, averageRating: null } }
    : previewData;

  return <main className="app-shell"><FavoritesProvider enabled={false}>
    <Home initialData={state === "error" || state === "loading" ? null : data} initialError={state === "error"} initialLoading={state === "loading"} role={queryRole()} />
  </FavoritesProvider></main>;
}
