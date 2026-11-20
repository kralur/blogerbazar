import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, beforeEach } from "vitest";
import { loadLanguage, setUzbekScript } from "../src/i18n";

// The app loads the Uzbek texts on demand; tests read both languages at once.
await loadLanguage("uz");
// The Uzbek texts are written (and asserted) in the 1995 alphabet; tests/uzbek-script.test.ts checks the new one,
// which is the app's default.
beforeEach(() => setUzbekScript("old"));

afterEach(() => {
  cleanup();
  localStorage.clear();
  window.location.hash = "#/";
  delete window.Telegram;
});
