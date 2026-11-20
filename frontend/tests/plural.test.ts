import { describe, expect, it } from "vitest";
import { translate } from "../src/i18n";
import { reviewsLabel } from "../src/lib/plural";

const ru = (key: string, values?: Record<string, string | number>) => translate(key, values, "ru");

describe("review counts", () => {
  it("use the Russian number forms", () => {
    expect([1, 2, 5, 11, 21, 22, 112].map((count) => reviewsLabel(count, "ru", ru))).toEqual([
      "1 отзыв", "2 отзыва", "5 отзывов", "11 отзывов", "21 отзыв", "22 отзыва", "112 отзывов"
    ]);
  });
});
