import { beforeEach, describe, expect, it } from "vitest";
import { isAtlasSoundOn, noteForTile, pluckForTile, setAtlasSound } from "../src/lib/atlasSound";

// D63, package F: the strip sound is off by default and walks a pentatonic motif by tile.
describe("atlas sound", () => {
  beforeEach(() => { localStorage.clear(); });

  it("is off until switched on", () => {
    expect(isAtlasSoundOn()).toBe(false);
    setAtlasSound(true);
    expect(isAtlasSoundOn()).toBe(true);
    setAtlasSound(false);
    expect(isAtlasSoundOn()).toBe(false);
  });

  it("maps tiles to notes of the scale, repeating the motif", () => {
    const notes = Array.from({ length: 8 }, (_, index) => noteForTile(index));
    expect(new Set(notes).size).toBeGreaterThan(3);
    expect(noteForTile(8)).toBe(noteForTile(0));
    expect(noteForTile(-1)).toBe(noteForTile(7));
  });

  it("stays silent without Web Audio and when off", () => {
    expect(() => pluckForTile(3)).not.toThrow();
    setAtlasSound(true);
    expect(() => pluckForTile(3)).not.toThrow();
  });
});
