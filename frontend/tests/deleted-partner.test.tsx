import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { ReviewList } from "../src/components/details/DetailBlocks";

describe("Deleted partner", () => {
  it("keeps a review from a deleted account and shows the author as deleted, without a link", () => {
    render(<I18nProvider><ReviewList emptyText="none" reviewerRoute={(id) => `#/company/${id}`} reviews={[{ id: "r1", rating: 5, comment: "Great reel", reviewerName: null, reviewerDeleted: true, createdAtUtc: "2026-09-10T00:00:00Z" }]} /></I18nProvider>);

    expect(screen.getByText("Great reel")).toBeInTheDocument();
    expect(screen.getByText(translate("common.deletedAccount", undefined, "ru"))).toBeInTheDocument();
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });
});
