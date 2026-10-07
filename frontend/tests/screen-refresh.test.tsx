import { act, render } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { requestScreenRefresh, useScreenRefresh } from "../src/hooks/useScreenRefresh";
import { RootScreenVisibility } from "../src/navigation/RootScreenVisibility";

function Screen({ refresh }: { refresh: () => unknown }) {
  useScreenRefresh(refresh);
  return null;
}

describe("screen refresh", () => {
  it("reloads the visible screen and waits for it", async () => {
    let finish: () => void = () => undefined;
    const refresh = vi.fn(() => new Promise<void>((resolve) => { finish = resolve; }));
    render(<Screen refresh={refresh} />);

    let done = false;
    const request = requestScreenRefresh().then(() => { done = true; });
    expect(refresh).toHaveBeenCalledTimes(1);
    await Promise.resolve();
    expect(done).toBe(false);
    finish();
    await request;
    expect(done).toBe(true);
  });

  it("refreshes a hidden root screen when it is shown again", async () => {
    const refresh = vi.fn();
    const { rerender } = render(<RootScreenVisibility active={false}><Screen refresh={refresh} /></RootScreenVisibility>);

    await act(() => requestScreenRefresh());
    expect(refresh).not.toHaveBeenCalled();

    rerender(<RootScreenVisibility active><Screen refresh={refresh} /></RootScreenVisibility>);
    expect(refresh).toHaveBeenCalledTimes(1);
  });
});
