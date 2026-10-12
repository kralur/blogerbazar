import { describe, expect, it } from "vitest";
import { ApiError } from "../src/api/client";
import { translate } from "../src/i18n";

describe("API error messages", () => {
  it("explains a rate limit instead of a generic error", () => {
    expect(new ApiError(429, "rate_limited").message).toBe(translate("error.rate_limited"));
  });
});
