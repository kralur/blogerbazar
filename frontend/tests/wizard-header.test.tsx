import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { WizardHeader } from "../src/components/Wizard";

function renderHeader(showBackButton: boolean) {
  render(<WizardHeader backLabel="Back" onBack={vi.fn()} progressLabel="Step 1 of 3" showBackButton={showBackButton} step={1} stepTitle="Company" totalSteps={3} />);
}

describe("WizardHeader", () => {
  it("does not reserve a native-back placeholder inside Telegram", () => {
    renderHeader(false);

    expect(screen.getByRole("heading", { level: 1, name: "Company" }).closest(".wizard-header")).toHaveAttribute("data-content-header");
    expect(screen.queryByRole("button", { name: "Back" })).not.toBeInTheDocument();
    expect(document.querySelector(".wizard-header__back-placeholder")).not.toBeInTheDocument();
  });

  it("keeps browser Back action separate from the title and progress content", () => {
    renderHeader(true);

    expect(screen.getByRole("button", { name: "Back" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1, name: "Company" })).toBeInTheDocument();
    expect(screen.getByRole("progressbar", { name: "Step 1 of 3" })).toBeInTheDocument();
  });
});
