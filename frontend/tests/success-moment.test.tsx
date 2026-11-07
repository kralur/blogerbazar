import { act, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const haptic = vi.hoisted(() => ({ impact: vi.fn(), success: vi.fn(), selection: vi.fn(), error: vi.fn(), warning: vi.fn() }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic }) }));

import { SuccessMoment, successMomentDurationMs } from "../src/components/SuccessMoment";

describe("Success moment", () => {
  beforeEach(() => { vi.clearAllMocks(); vi.useFakeTimers(); });
  afterEach(() => vi.useRealTimers());

  it("shows the check with the message, buzzes once and goes away on its own", () => {
    const onDone = vi.fn();
    render(<SuccessMoment message="Отклик отправлен" onDone={onDone} />);
    const status = screen.getByRole("status");
    expect(status).toHaveTextContent("Отклик отправлен");
    expect(status.querySelector(".success-moment__check")).not.toBeNull();
    expect(haptic.success).toHaveBeenCalledTimes(1);
    expect(onDone).not.toHaveBeenCalled();

    act(() => { vi.advanceTimersByTime(successMomentDurationMs); });
    expect(screen.queryByRole("status")).toBeNull();
    expect(onDone).toHaveBeenCalledTimes(1);
    expect(haptic.success).toHaveBeenCalledTimes(1);
  });

  it("renders nothing without a message and starts again for a new one", () => {
    const { rerender } = render(<SuccessMoment message="" />);
    expect(screen.queryByRole("status")).toBeNull();
    expect(haptic.success).not.toHaveBeenCalled();

    rerender(<SuccessMoment message="Готово" />);
    expect(screen.getByRole("status")).toHaveTextContent("Готово");
    act(() => { vi.advanceTimersByTime(successMomentDurationMs); });
    rerender(<SuccessMoment key="second" message="Готово" />);
    expect(screen.getByRole("status")).toBeInTheDocument();
    expect(haptic.success).toHaveBeenCalledTimes(2);
  });
});
