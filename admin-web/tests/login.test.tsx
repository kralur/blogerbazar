import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { I18nProvider } from "../src/i18n";
import { LoginPage, errorFromQuery, telegramWidgetUrl } from "../src/pages/LoginPage";
import { fakeApi, session, watchNavigation } from "./helpers";

function renderLogin() {
  return render(<I18nProvider><LoginPage /></I18nProvider>);
}

describe("Login page", () => {
  it("loads the Telegram widget for the configured bot in redirect mode", async () => {
    fakeApi({ "GET /api/admin-web/session": { status: 401 }, "GET /api/admin-web/config": { status: 200, body: { botUsername: "BloggerBazarBot" } } });
    watchNavigation();
    renderLogin();

    const container = await screen.findByTestId("telegram-login");
    await waitFor(() => expect(container.querySelector("script")).not.toBeNull());
    const script = container.querySelector("script")!;
    expect(script.src).toBe(telegramWidgetUrl);
    expect(script.dataset.telegramLogin).toBe("BloggerBazarBot");
    expect(script.dataset.authUrl).toBe(`${window.location.origin}/auth/telegram`);
    // Redirect mode only: the inline data-onauth callback would need 'unsafe-eval' in the CSP.
    expect(script.hasAttribute("data-onauth")).toBe(false);
    expect(screen.getByRole("heading", { name: "Вход в админ-панель" })).toBeInTheDocument();
  });

  it("opens the admin pages when a session is already there", async () => {
    fakeApi({ "GET /api/admin-web/session": { status: 200, body: session } });
    const navigation = watchNavigation();
    renderLogin();

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/"));
  });

  it("explains why the last sign-in failed", async () => {
    fakeApi({ "GET /api/admin-web/session": { status: 401 }, "GET /api/admin-web/config": { status: 200, body: { botUsername: "BloggerBazarBot" } } });
    watchNavigation();
    window.history.replaceState(null, "", "/login?error=denied");
    renderLogin();

    expect(await screen.findByRole("alert")).toHaveTextContent("У этого аккаунта Telegram нет доступа к админ-панели.");
  });

  it("says the web sign-in is not set up when the API has it switched off", async () => {
    fakeApi({ "GET /api/admin-web/session": { status: 404 }, "GET /api/admin-web/config": { status: 404 } });
    watchNavigation();
    renderLogin();

    expect(await screen.findByRole("alert")).toHaveTextContent("Вход через сайт ещё не настроен.");
    expect(screen.queryByTestId("telegram-login")).toBeNull();
  });

  it("speaks Uzbek when chosen", async () => {
    localStorage.setItem("bloggerbazar.admin.language", "uz");
    fakeApi({ "GET /api/admin-web/session": { status: 401 }, "GET /api/admin-web/config": { status: 200, body: { botUsername: "BloggerBazarBot" } } });
    watchNavigation();
    renderLogin();

    expect(await screen.findByRole("heading", { name: "Admin panelga kirish" })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId("telegram-login").querySelector("script")?.dataset.lang).toBe("uz"));
  });

  it("accepts only known error codes from the address", () => {
    expect(errorFromQuery("?error=rate")).toBe("rate");
    expect(errorFromQuery("?error=<script>")).toBeNull();
    expect(errorFromQuery("")).toBeNull();
  });
});
