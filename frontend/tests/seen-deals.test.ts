import { beforeEach, describe, expect, it } from "vitest";
import { markDealSeen, unseenActiveDealIds } from "../src/data/seenDeals";

describe("New deal dot", () => {
  beforeEach(() => localStorage.clear());

  it("treats deals already there on the first look as seen and marks only later active ones as new", () => {
    expect(unseenActiveDealIds([{ id: "old", status: 0 }]).size).toBe(0);

    const unseen = unseenActiveDealIds([{ id: "old", status: 0 }, { id: "fresh", status: 0 }, { id: "done", status: 1 }]);

    expect([...unseen]).toEqual(["fresh"]);
  });

  it("clears the dot once the deal is opened", () => {
    unseenActiveDealIds([]);
    markDealSeen("fresh");

    expect(unseenActiveDealIds([{ id: "fresh", status: 0 }]).size).toBe(0);
  });
});
