import { readFileSync } from "node:fs";
import { useState } from "react";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { CategoryMultiSelect } from "../src/components/CategoryMultiSelect";
import { I18nProvider } from "../src/i18n";

vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { error: vi.fn() } }) }));

function CategoryHarness() {
  const [value, setValue] = useState<string[]>([]);
  return <><CategoryMultiSelect onChange={setValue} required value={value} /><output>{value.join(",")}</output></>;
}

function choices() {
  return screen.getAllByRole("button").filter((button) => button.classList.contains("category-multi-select__choice"));
}

describe("CategoryMultiSelect", () => {
  it("uses the shared Acid Lime selected state without legacy blue or cyan classes", async () => {
    const user = userEvent.setup();
    render(<I18nProvider><CategoryHarness /></I18nProvider>);

    await user.click(choices()[0]);
    const selected = screen.getAllByRole("button").find((button) => button.classList.contains("category-multi-select__selected"));

    expect(selected).toBeDefined();
    expect(selected).toHaveClass("category-multi-select__selected");
    expect(selected?.className).not.toMatch(/blue|cyan/);
    expect(document.querySelector("button[aria-pressed=\"true\"] .marketplace-chip--active")).toBeInTheDocument();
  });

  it("keeps the existing one-to-five selection limit", async () => {
    const user = userEvent.setup();
    render(<I18nProvider><CategoryHarness /></I18nProvider>);

    for (const choice of choices().slice(0, 6)) await user.click(choice);

    expect(screen.getByText("5/5")).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent("lifestyle,beauty,food,technology,sport");
  });

  it("does not retain legacy blue or cyan selected styling in the shared implementation", () => {
    const component = readFileSync("src/components/CategoryMultiSelect.tsx", "utf8");
    const ui = readFileSync("src/components/ui.tsx", "utf8");
    expect(component).not.toMatch(/bg-blue|text-brand-blue|cyan/i);
    expect(ui).toContain("marketplace-chip--active");
  });
});
