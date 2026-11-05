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

  // Audit 2026-10-22: a program that splits on commas (Excel with English settings, Google Sheets) must still see one
  // cell, so a formula hidden after a comma never starts a cell of its own.
  it("quotes cells with a comma or a tab, so nothing after them becomes a cell", () => {
    expect(csvCell("x,=1+1")).toBe('"x,=1+1"');
    expect(csvCell("x\t=1+1")).toBe('"x\t=1+1"');
    expect(csvCell("Tashkent, Chilonzor")).toBe('"Tashkent, Chilonzor"');
  });
});
