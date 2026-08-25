import React from "react";
import ReactDOM from "react-dom/client";
import { App } from "./App";
import { I18nProvider } from "./i18n";
import { VirtualKeyboardProvider } from "./layout/VirtualKeyboardProvider";
import { TelegramProvider } from "./telegram/TelegramProvider";
import "./styles.css";

const root = ReactDOM.createRoot(document.getElementById("root")!);
const previewHome = import.meta.env.DEV && new URLSearchParams(window.location.search).get("preview") === "home";

const renderApplication = () => root.render(
  <React.StrictMode>
    <I18nProvider><TelegramProvider><VirtualKeyboardProvider><App /></VirtualKeyboardProvider></TelegramProvider></I18nProvider>
  </React.StrictMode>
);

if (previewHome) {
  void import("./dev/HomePreview").then(({ HomePreview }) => root.render(
    <React.StrictMode>
      <I18nProvider><TelegramProvider><VirtualKeyboardProvider><HomePreview /></VirtualKeyboardProvider></TelegramProvider></I18nProvider>
    </React.StrictMode>
  ));
} else {
  renderApplication();
}
