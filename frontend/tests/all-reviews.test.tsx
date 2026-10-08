import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getBloggerReviews: vi.fn(), getBusinessReviews: vi.fn() }));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getBloggerReviews: api.getBloggerReviews,
  getBusinessReviews: api.getBusinessReviews
}));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ openLink: vi.fn() }) }));
vi.mock("../src/components/ui", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/components/ui")>()),
  BottomNav: () => null
}));

import { AllReviews } from "../src/pages/AllReviews";
import { clearPublicDetailCache } from "../src/data/publicDetailCache";

const ru = (key: string) => translate(key, undefined, "ru");
const review = (n: number) => ({ id: `r${n}`, dealId: `d${n}`, targetType: 0, rating: 5, comment: `Review ${n}`, reviewerName: "Lumi", createdAtUtc: "2026-09-10T00:00:00Z" });

describe("All reviews", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    clearPublicDetailCache();
  });

  afterEach(() => cleanup());

  it("loads a blogger's reviews page by page", async () => {
    api.getBloggerReviews
      .mockResolvedValueOnce(Array.from({ length: 20 }, (_, i) => review(i)))
      .mockResolvedValueOnce([review(20)]);
    render(<I18nProvider><AllReviews id="blogger-a" target="blogger" /></I18nProvider>);

    expect(await screen.findByText("Review 0")).toBeInTheDocument();
    expect(api.getBloggerReviews).toHaveBeenCalledWith("blogger-a", { skip: 0, take: 20 });
    fireEvent.click(screen.getByRole("button", { name: ru("applications.loadMore") }));

    expect(await screen.findByText("Review 20")).toBeInTheDocument();
    expect(api.getBloggerReviews).toHaveBeenLastCalledWith("blogger-a", { skip: 20, take: 20 });
    expect(screen.queryByRole("button", { name: ru("applications.loadMore") })).not.toBeInTheDocument();
  });

  it("shows a business's rating and count with its reviews", async () => {
    api.getBusinessReviews.mockResolvedValue({ rating: 4.5, reviewsCount: 2, items: [review(1), review(2)] });
    render(<I18nProvider><AllReviews id="business-a" target="business" /></I18nProvider>);

    expect(await screen.findByText("Review 2")).toBeInTheDocument();
    expect(api.getBusinessReviews).toHaveBeenCalledWith("business-a", undefined, { skip: 0, take: 20 });
    expect(document.querySelector(".all-reviews__summary")?.textContent).toContain("4,5");
    expect(screen.queryByRole("button", { name: ru("applications.loadMore") })).not.toBeInTheDocument();
  });

  it("offers a retry when the reviews do not load", async () => {
    api.getBusinessReviews.mockRejectedValue(new Error("offline"));
    render(<I18nProvider><AllReviews id="business-a" target="business" /></I18nProvider>);

    expect(await screen.findByText(ru("common.openFailed"))).toBeInTheDocument();
  });
});
