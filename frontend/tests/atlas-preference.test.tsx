import { render } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import { AtlasBackground } from "../src/components/AtlasBackground";
import { AtlasStrip } from "../src/components/AtlasStrip";
import { isAtlasOn, setAtlas } from "../src/lib/atlasPreference";

// Owner, 2026-11-09 (package I): the atlas pattern can be switched off; the strip and the first-screen background go,
// the previews in Settings stay so the choice can still be seen.
describe("atlas switch", () => {
  beforeEach(() => { localStorage.clear(); });

  it("is on by default and draws the strip and the background", () => {
    expect(isAtlasOn()).toBe(true);
    const { container } = render(<><AtlasStrip /><AtlasBackground /></>);
    expect(container.querySelector(".atlas-strip")).toBeInTheDocument();
    expect(container.querySelector(".atlas-background")).toBeInTheDocument();
  });

  it("removes the strip and the background when off, except the fixed previews", () => {
    setAtlas(false);
    expect(isAtlasOn()).toBe(false);
    const { container } = render(<><AtlasStrip /><AtlasBackground /><AtlasStrip fixed height={18} /></>);
    expect(container.querySelectorAll(".atlas-strip").length).toBe(1);
    expect(container.querySelector(".atlas-strip--fixed")).toBeInTheDocument();
    expect(container.querySelector(".atlas-background")).toBeNull();
  });

  it("comes back live when switched on again", () => {
    setAtlas(false);
    const { container } = render(<AtlasStrip />);
    expect(container.querySelector(".atlas-strip")).toBeNull();
    setAtlas(true);
    expect(isAtlasOn()).toBe(true);
  });
});
