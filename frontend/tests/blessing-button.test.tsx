import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const haptic = vi.hoisted(() => ({ impact: vi.fn(), success: vi.fn(), selection: vi.fn(), error: vi.fn(), warning: vi.fn() }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic }) }));

import { BlessingButton } from "../src/components/BlessingButton";

const button = () => screen.getByRole("button", { name: translate("deals.blessing", undefined, "ru") });

describe("Blessing button", () => {
  beforeEach(() => { vi.clearAllMocks(); vi.useFakeTimers(); });
  afterEach(() => vi.useRealTimers());

  it("buzzes on a tap and launches a short emoji firework on three quick taps", () => {
    const { container } = render(<I18nProvider><BlessingButton /></I18nProvider>);

    fireEvent.click(button());
    expect(haptic.impact).toHaveBeenCalledTimes(1);
    expect(container.querySelector(".deal-blessing__particle")).toBeNull();

    fireEvent.click(button());
    fireEvent.click(button());
    expect(haptic.success).toHaveBeenCalledTimes(1);
    expect(container.querySelectorAll(".deal-blessing__particle").length).toBeGreaterThan(0);

    act(() => { vi.advanceTimersByTime(1500); });
    expect(container.querySelector(".deal-blessing__particle")).toBeNull();
  });

  it("keeps the number of flying emoji capped however fast it is tapped", () => {
    const { container } = render(<I18nProvider><BlessingButton /></I18nProvider>);

    for (let tap = 0; tap < 30; tap += 1) fireEvent.click(button());

    expect(container.querySelectorAll(".deal-blessing__particle").length).toBeLessThanOrEqual(42);
  });
});
