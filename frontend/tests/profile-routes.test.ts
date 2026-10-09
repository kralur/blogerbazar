import { describe, expect, it } from "vitest";
import { profileRoute } from "../src/lib/profileRoutes";

describe("profile routes", () => {
  it("opens the public page of each profile kind (D46)", () => {
    expect(profileRoute("brandFace", "a")).toBe("#/brand-face-detail/a");
    expect(profileRoute("business", "b")).toBe("#/company/b");
    expect(profileRoute("blogger", "c")).toBe("#/blogger/c");
    // An older API without the kind keeps the blogger link it always had.
    expect(profileRoute(undefined, "d")).toBe("#/blogger/d");
  });
});
