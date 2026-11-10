import { describe, expect, it } from "vitest";
import { canGoBackInApp, noteHistoryReplace, startAppHistory } from "../src/navigation/appHistory";

const go = (hash: string) => {
  window.location.hash = hash;
  window.dispatchEvent(new HashChangeEvent("hashchange"));
};

describe("app history (website Back)", () => {
  it("knows a step back exists only after moving inside the app", () => {
    window.location.hash = "#/search";
    startAppHistory();
    expect(canGoBackInApp()).toBe(false);

    go("#/blogger/1");
    expect(canGoBackInApp()).toBe(true);

    // Returning to the previous page uses up the step.
    go("#/search");
    expect(canGoBackInApp()).toBe(false);

    // A replaced route does not add a step.
    noteHistoryReplace();
    go("#/search?type=brand-face");
    expect(canGoBackInApp()).toBe(false);
  });
});
