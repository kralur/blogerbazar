import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ActionCountsProvider, useActionCounts } from "../src/features/actionCounts/ActionCountsProvider";

const api = vi.hoisted(() => ({ getMyCampaigns: vi.fn(), getMyDeals: vi.fn(), getMyOffers: vi.fn() }));
vi.mock("../src/api/marketplace", () => api);

function Counts() {
  const counts = useActionCounts();
  return <p>{`offers ${counts.offers} applications ${counts.applications} reviews ${counts.reviews} total ${counts.total}`}</p>;
}

describe("action counts", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getMyDeals.mockResolvedValue([{ id: "a", canReview: true }, { id: "b", canReview: false }]);
  });

  it("counts offers to answer and reviews to leave for a blogger", async () => {
    api.getMyOffers.mockResolvedValue([{ id: "o1", canRespond: true }, { id: "o2", canRespond: false }, { id: "o3", canRespond: true }]);
    render(<ActionCountsProvider enabled role="Blogger"><Counts /></ActionCountsProvider>);

    await waitFor(() => expect(screen.getByText("offers 2 applications 0 reviews 1 total 3")).toBeInTheDocument());
    expect(api.getMyCampaigns).not.toHaveBeenCalled();
  });

  it("counts applications waiting for a decision for a business", async () => {
    api.getMyCampaigns.mockResolvedValue({ items: [{ id: "c1", applicationsCount: 9, pendingApplicationsCount: 2 }, { id: "c2", applicationsCount: 4, pendingApplicationsCount: 0 }] });
    render(<ActionCountsProvider enabled role="Business"><Counts /></ActionCountsProvider>);

    await waitFor(() => expect(screen.getByText("offers 0 applications 2 reviews 1 total 3")).toBeInTheDocument());
  });

  it("asks for nothing before onboarding is complete", async () => {
    render(<ActionCountsProvider enabled={false} role="Blogger"><Counts /></ActionCountsProvider>);

    await waitFor(() => expect(screen.getByText("offers 0 applications 0 reviews 0 total 0")).toBeInTheDocument());
    expect(api.getMyDeals).not.toHaveBeenCalled();
  });
});
