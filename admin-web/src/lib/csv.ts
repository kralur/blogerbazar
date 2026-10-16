// CSV for Excel: UTF-8 with a byte order mark, ";" between cells (what Excel expects with Russian and Uzbek regional
// settings), CRLF line ends. A cell that starts like a formula gets a leading apostrophe, so a name such as
// "=HYPERLINK(...)" stays text when the file is opened.
export type CsvCell = string | number | null | undefined;

const formulaStart = /^[=+\-@\t\r]/;

export function csvCell(value: CsvCell) {
  if (value === null || value === undefined) return "";
  if (typeof value === "number") return Number.isFinite(value) ? String(value) : "";
  const text = formulaStart.test(value) ? `'${value}` : value;
  return /[";\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

export function toCsv(head: string[], rows: CsvCell[][]) {
  return "﻿" + [head, ...rows].map((row) => row.map(csvCell).join(";")).join("\r\n") + "\r\n";
}

// Saves the text as a file through a temporary object URL (no inline script, so the strict CSP allows it).
export function saveFile(fileName: string, text: string) {
  const url = URL.createObjectURL(new Blob([text], { type: "text/csv;charset=utf-8" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.append(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
