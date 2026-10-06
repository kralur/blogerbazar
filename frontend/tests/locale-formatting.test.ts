import { afterEach, describe, expect, it } from "vitest";
import { formatBudgetRange, formatCompactNumber, formatCurrency, formatPercentage, formatShortDate, isPastDay, localDay } from "../src/lib/currency";

const plain = (value: string) => value.replace(/\s/g, " ");

describe("locale formatting", () => {
  afterEach(() => localStorage.clear());

  it("spells Uzbek dates without relying on WebView locale data", () => {
    const date = new Date(2026, 10, 1, 9, 5);
    expect(formatShortDate(date, "uz")).toBe("1-noy");
    expect(formatShortDate(date, "uz", { year: true })).toBe("1-noy, 2026");
    expect(formatShortDate(date, "uz", { time: true })).toBe("1-noy, 09:05");
    expect(formatShortDate(date, "ru", { year: true })).not.toMatch(/M11/);
  });

  it("groups Uzbek numbers with spaces and uses Uzbek compact units", () => {
    localStorage.setItem("bloggerbazar.language", "uz");
    expect(plain(formatCurrency(1500000))).toBe("1 500 000 so‘m");
    expect(plain(formatBudgetRange(1500000, 3000000) ?? "")).not.toContain(",");
    expect(plain(formatCompactNumber(52100))).toBe("52,1 ming");
    expect(plain(formatCompactNumber(1200000))).toBe("1,2 mln");
    expect(formatPercentage(8.4)).toBe("8,4%");
  });

  it("keeps a deadline open through its own day", () => {
    expect(isPastDay(`${localDay(-1)}T00:00:00Z`)).toBe(true);
    expect(isPastDay(`${localDay(0)}T00:00:00Z`)).toBe(false);
    expect(isPastDay(null)).toBe(false);
  });
});
