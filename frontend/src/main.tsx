import React from "react";
import ReactDOM from "react-dom/client";
import { App } from "./App";
import { AppErrorBoundary } from "./components/AppErrorBoundary";
import { I18nProvider } from "./i18n";
import { VirtualKeyboardProvider } from "./layout/VirtualKeyboardProvider";
import { TelegramProvider } from "./telegram/TelegramProvider";
import "./styles.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <I18nProvider><TelegramProvider><VirtualKeyboardProvider><AppErrorBoundary><App /></AppErrorBoundary></VirtualKeyboardProvider></TelegramProvider></I18nProvider>
  </React.StrictMode>
);
