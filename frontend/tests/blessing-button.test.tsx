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

  it("sends emoji floating up on every tap and a firework on five quick taps", () => {
    const { container } = render(<I18nProvider><BlessingButton /></I18nProvider>);

    fireEvent.pointerDown(button());
    expect(haptic.impact).toHaveBeenCalledTimes(1);
    expect(container.querySelectorAll(".deal-blessing__particle--float")).toHaveLength(2);
    expect(container.querySelector(".deal-blessing__particle--burst")).toBeNull();

    for (let tap = 0; tap < 4; tap += 1) fireEvent.pointerDown(button());
    expect(haptic.impact).toHaveBeenCalledTimes(5);
    expect(haptic.success).toHaveBeenCalledTimes(1);
    expect(container.querySelectorAll(".deal-blessing__particle--burst").length).toBeGreaterThan(0);

    act(() => { vi.advanceTimersByTime(1700); });
    expect(container.querySelector(".deal-blessing__particle")).toBeNull();
  });

  it("keeps the number of flying emoji capped however fast it is tapped", () => {
    const { container } = render(<I18nProvider><BlessingButton /></I18nProvider>);

    for (let tap = 0; tap < 60; tap += 1) fireEvent.pointerDown(button());

    expect(container.querySelectorAll(".deal-blessing__particle").length).toBeLessThanOrEqual(60);
  });

  it("works from the keyboard too", () => {
    const { container } = render(<I18nProvider><BlessingButton /></I18nProvider>);

    fireEvent.keyDown(button(), { key: "Enter" });

    expect(haptic.impact).toHaveBeenCalledTimes(1);
    expect(container.querySelectorAll(".deal-blessing__particle--float")).toHaveLength(2);
  });
});
