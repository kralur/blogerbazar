import React from "react";
import ReactDOM from "react-dom/client";
import { App } from "./App";
import { I18nProvider } from "./i18n";
import { TelegramProvider } from "./telegram/TelegramProvider";
import "./styles.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <I18nProvider><TelegramProvider><App /></TelegramProvider></I18nProvider>
  </React.StrictMode>
);
