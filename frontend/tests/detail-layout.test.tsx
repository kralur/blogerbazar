import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { DetailLayout } from "../src/components/DetailLayout";
import { DesktopLayoutContext } from "../src/lib/desktopLayout";

const page = <DetailLayout actions={<button type="button">offer</button>} side={<p>side</p>} top={<p>top</p>}><p>main</p></DetailLayout>;

describe("detail layout", () => {
  it("keeps the phone's single column in its order, without the computer-only actions", () => {
    const { container } = render(<div>{page}</div>);

    expect([...container.firstElementChild!.children].map((element) => element.textContent)).toEqual(["top", "main", "side"]);
    expect(container.querySelector(".detail-layout")).toBeNull();
  });

  it("puts the actions, top and side in a panel next to the content on a computer", () => {
    const { container } = render(<DesktopLayoutContext.Provider value>{page}</DesktopLayoutContext.Provider>);

    expect(container.querySelector(".detail-layout__main")?.textContent).toBe("main");
    expect([...container.querySelector(".detail-layout__side")!.children].map((element) => element.textContent)).toEqual(["offer", "top", "side"]);
  });
});
