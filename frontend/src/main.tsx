import React from "react";
import ReactDOM from "react-dom/client";
import { App } from "./App";
import { AppErrorBoundary } from "./components/AppErrorBoundary";
import { currentLanguage, I18nProvider, loadLanguage } from "./i18n";
import { VirtualKeyboardProvider } from "./layout/VirtualKeyboardProvider";
import { TelegramProvider } from "./telegram/TelegramProvider";
import { normalizeLaunchHash } from "./lib/launchHash";
import { installCspReporter } from "./security/cspReporter";
import "./styles.css";

normalizeLaunchHash(window.location, window.history);
installCspReporter();

// An Uzbek start waits for its dictionary (one small file); a failed download starts in Russian rather than not at all.
void loadLanguage(currentLanguage()).catch(() => undefined).then(() => {
  ReactDOM.createRoot(document.getElementById("root")!).render(
    <React.StrictMode>
      <I18nProvider><TelegramProvider><VirtualKeyboardProvider><AppErrorBoundary><App /></AppErrorBoundary></VirtualKeyboardProvider></TelegramProvider></I18nProvider>
    </React.StrictMode>
  );
});
