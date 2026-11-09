import { describe, expect, it } from "vitest";
import { plainName, plainNames } from "../src/lib/personName";

// Owner, 2026-11-09: one list, one font. Fancy Unicode letters in names are folded to plain ones.
describe("plain names", () => {
  it("folds mathematical, fullwidth and small-capital letters and drops invisible characters", () => {
    expect(plainName("\u{1D400}\u{1D425}\u{1D422}")).toBe("Ali");
    expect(plainName("\u{1D49C}\u{1D4C1}\u{1D4BE}")).toBe("Ali");
    expect(plainName("Ａｌｉ")).toBe("Ali");
    expect(plainName("ᴀʟɪ")).toBe("ali");
    expect(plainName("Ali​‍  Valiyev ")).toBe("Ali Valiyev");
    expect(plainName("Умиджон")).toBe("Умиджон");
    expect(plainName("Ali \u{1F600}")).toBe("Ali \u{1F600}");
  });

  it("folds only name fields of an API answer, deep, leaving other text as typed", () => {
    const answer = { items: [{ name: "\u{1D400}\u{1D425}\u{1D422}", counterpartyName: "Ｍ", company: "Ｋｏｆｅ", description: "Ａ text" }], businessName: "ᴀ" };
    plainNames(answer);
    expect(answer.items[0].name).toBe("Ali");
    expect(answer.items[0].counterpartyName).toBe("M");
    expect(answer.items[0].company).toBe("Kofe");
    expect(answer.items[0].description).toBe("Ａ text");
    expect(answer.businessName).toBe("a");
  });
});
