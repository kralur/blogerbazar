import { describe, expect, it } from "vitest";
import { formatMessage } from "@bloggerbazar/shared/i18n";
import { dictionaries } from "../src/i18n";
import { daysInRange, formatDate, formatMoney, formatMonth, percent, tashkentDate, taxonomyLabel } from "../src/lib/format";
import { biggestDrop } from "../src/pages/FunnelPage";
import { presetPeriod } from "../src/components/PeriodPicker";

const ru = (key: string, values?: Record<string, string | number>) => formatMessage(dictionaries, "ru", key, values);
const uz = (key: string, values?: Record<string, string | number>) => formatMessage(dictionaries, "uz", key, values);

describe("Formatting", () => {
  it("counts days in Tashkent: 19:00 UTC is already the next day there", () => {
    expect(tashkentDate(0, new Date("2026-10-15T18:59:00Z"))).toBe("2026-10-15");
    expect(tashkentDate(0, new Date("2026-10-15T19:00:00Z"))).toBe("2026-10-16");
    expect(presetPeriod(7, new Date("2026-10-15T19:00:00Z"))).toEqual({ preset: "7", from: "2026-10-10", to: "2026-10-16" });
    expect(daysInRange("2026-01-01", "2026-12-31")).toBe(365);
  });

  it("writes sums, months, shares and dates the way the owner reads them", () => {
    expect(formatMoney(1_250_000, ru)).toBe("1 250 000 сум");
    expect(formatMoney(1_250_000, uz)).toBe("1 250 000 so‘m");
    expect(formatMonth("2026-01", ru)).toBe("янв 2026");
    expect(formatMonth("2026-12", uz)).toBe("dek 2026");
    expect(formatDate("2026-10-05")).toBe("05.10.2026");
    expect(percent(1, 3)).toBe("33%");
    expect(percent(1, 0)).toBeNull();
  });

  it("names cities and categories from the shared list, a custom category by its own text", () => {
    expect(taxonomyLabel("city", "Tashkent", ru)).toBe("Ташкент");
    expect(taxonomyLabel("category", "beauty", uz)).not.toBe("beauty");
    expect(taxonomyLabel("category", "other: Котики", ru)).toBe("Котики");
    expect(taxonomyLabel("city", "Nukus-2", ru)).toBe("Nukus-2");
  });

  it("finds the step that loses the largest share of people", () => {
    expect(biggestDrop([100, 80, 20, 15])).toBe(2);
    expect(biggestDrop([0, 0, 0])).toBe(-1);
    expect(biggestDrop([10, 10, 0])).toBe(2);
  });
});
