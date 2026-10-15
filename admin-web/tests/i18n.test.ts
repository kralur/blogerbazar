import { describe, expect, it } from "vitest";
import ru from "../src/i18n/ru.json";
import uz from "../src/i18n/uz.json";
import { navigation, type NavItem } from "../src/layout/navigation";
import { formatTashkentTime } from "../src/lib/time";

const flatten = (items: NavItem[]): NavItem[] => items.flatMap((item) => [item, ...flatten(item.children ?? [])]);

describe("Admin web texts", () => {
  it("have the same keys in Russian and Uzbek, including every menu item", () => {
    expect(Object.keys(uz).sort()).toEqual(Object.keys(ru).sort());
    for (const item of flatten(navigation)) {
      expect(ru).toHaveProperty([item.labelKey]);
      expect(uz).toHaveProperty([item.labelKey]);
    }
  });

  it("show times in Tashkent", () => {
    expect(formatTashkentTime("2026-10-15T19:05:00Z")).toBe("00:05");
    expect(formatTashkentTime("not a date")).toBe("");
  });
});
