import { render } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { RootScreenVisibility } from "../src/navigation/RootScreenVisibility";
import { notifyProfileDataChanged, useProfileDataRefresh } from "../src/hooks/useProfileDataRefresh";

function Probe({ refresh }: { refresh: () => void }) {
  useProfileDataRefresh(refresh);
  return null;
}

describe("profile data refresh", () => {
  it("refreshes a cached root screen once when it becomes visible after an avatar update", () => {
    const refresh = vi.fn();
    const view = render(<RootScreenVisibility active={false}><Probe refresh={refresh} /></RootScreenVisibility>);

    notifyProfileDataChanged();
    expect(refresh).not.toHaveBeenCalled();

    view.rerender(<RootScreenVisibility active><Probe refresh={refresh} /></RootScreenVisibility>);
    expect(refresh).toHaveBeenCalledTimes(1);
  });
});
