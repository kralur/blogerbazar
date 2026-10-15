import React from "react";
import ReactDOM from "react-dom/client";
import { I18nProvider } from "./i18n";
import { LoginPage } from "./pages/LoginPage";
import "./styles.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <I18nProvider>
      <LoginPage />
    </I18nProvider>
  </React.StrictMode>
);
