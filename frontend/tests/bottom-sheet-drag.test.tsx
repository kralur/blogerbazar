import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../src/i18n";

vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { impact: vi.fn(), success: vi.fn(), selection: vi.fn(), error: vi.fn(), warning: vi.fn() }, registerBackButtonHandler: () => () => undefined }) }));
vi.mock("../src/navigation/RootScreenVisibility", () => ({ useRootScreenVisibility: () => true }));

import { Modal } from "../src/components/ui";

// Owner, 2026-11-09: the sheet should follow the finger from its grip and close when pulled down far enough.
describe("bottom sheet drag", () => {
  beforeEach(() => { vi.useFakeTimers(); });
  afterEach(() => { vi.useRealTimers(); });

  const grip = () => screen.getByRole("dialog").querySelector(".bottom-sheet__grip") as HTMLElement;
  const sheet = () => screen.getByRole("dialog").querySelector(".bottom-sheet") as HTMLElement;

  it("follows the finger and snaps back after a short pull", () => {
    const onClose = vi.fn();
    render(<I18nProvider><Modal onClose={onClose} open title="Sheet"><p>body</p></Modal></I18nProvider>);
    fireEvent.pointerDown(grip(), { clientY: 100, pointerId: 1 });
    fireEvent.pointerMove(grip(), { clientY: 140, pointerId: 1 });
    expect(sheet().style.transform).toBe("translateY(40px)");
    fireEvent.pointerUp(grip(), { clientY: 140, pointerId: 1 });
    expect(sheet().style.transform).toBe("");
    vi.advanceTimersByTime(300);
    expect(onClose).not.toHaveBeenCalled();
  });

  it("closes after a long pull", () => {
    const onClose = vi.fn();
    render(<I18nProvider><Modal onClose={onClose} open title="Sheet"><p>body</p></Modal></I18nProvider>);
    fireEvent.pointerDown(grip(), { clientY: 100, pointerId: 1 });
    fireEvent.pointerMove(grip(), { clientY: 260, pointerId: 1 });
    fireEvent.pointerUp(grip(), { clientY: 260, pointerId: 1 });
    expect(sheet().classList.contains("bottom-sheet--leaving")).toBe(true);
    vi.advanceTimersByTime(300);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  // Owner, 2026-11-09: the cross in the sheet's head did nothing with a mouse, the grip had captured its pointer.
  it("leaves the close button alone: a press on it is not a drag, and it closes", () => {
    const onClose = vi.fn();
    render(<I18nProvider><Modal onClose={onClose} open title="Sheet"><p>body</p></Modal></I18nProvider>);
    const close = screen.getByRole("button", { name: "Закрыть" });
    fireEvent.pointerDown(close, { clientY: 100, pointerId: 1, pointerType: "mouse", button: 0 });
    expect(sheet().classList.contains("bottom-sheet--dragging")).toBe(false);
    fireEvent.pointerUp(close, { clientY: 100, pointerId: 1, pointerType: "mouse", button: 0 });
    fireEvent.click(close);
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
