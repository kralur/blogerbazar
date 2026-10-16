import { describe, expect, it } from "vitest";
import { csvCell, toCsv } from "../src/lib/csv";

describe("CSV", () => {
  it("is ready for Excel: byte order mark, semicolons, CRLF", () => {
    expect(toCsv(["Имя", "Сумма"], [["Madina", 1500000], [null, undefined]])).toBe("﻿Имя;Сумма\r\nMadina;1500000\r\n;\r\n");
  });

  it("quotes cells with separators, quotes and line breaks", () => {
    expect(csvCell('Cafe; "Darvoza"')).toBe('"Cafe; ""Darvoza"""');
    expect(csvCell("line 1\nline 2")).toBe('"line 1\nline 2"');
    expect(csvCell(Number.NaN)).toBe("");
  });

  it("keeps names that look like formulas as text", () => {
    expect(csvCell("=HYPERLINK(\"http://evil\")")).toBe('"\'=HYPERLINK(""http://evil"")"');
    expect(csvCell("+998901234567")).toBe("'+998901234567");
    expect(csvCell("-5")).toBe("'-5");
    expect(csvCell("@madina")).toBe("'@madina");
    expect(csvCell("Madina")).toBe("Madina");
  });
});
