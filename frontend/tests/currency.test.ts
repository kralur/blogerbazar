import { describe, expect, it } from "vitest";
import { formatPhoneInput } from "../src/lib/currency";

describe("formatPhoneInput", () => {
  it("keeps the Uzbekistan country prefix when no local digits are entered", () => {
    expect(formatPhoneInput("")).toBe("+998");
  });

  it("formats an 88 operator code without rewriting it", () => {
    expect(formatPhoneInput("88")).toBe("+998 88");
    expect(formatPhoneInput("881234567")).toBe("+998 88 123 45 67");
  });

  it("accepts an already formatted number and limits local input to nine digits", () => {
    expect(formatPhoneInput("+998 90 123 45 67")).toBe("+998 90 123 45 67");
    expect(formatPhoneInput("99877123456799")).toBe("+998 77 123 45 67");
  });
});
