import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";
import { loadLanguage } from "../src/i18n";

// The app loads the Uzbek texts on demand; tests read both languages at once.
await loadLanguage("uz");

afterEach(() => {
  cleanup();
  localStorage.clear();
  window.location.hash = "#/";
  delete window.Telegram;
});
