import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { App } from "../src/App";
import { I18nProvider } from "../src/i18n";
import { fakeApi, session, watchNavigation } from "./helpers";

function renderApp() {
  return render(<I18nProvider><App /></I18nProvider>);
}

describe("Admin pages", () => {
  it("send a browser without a session to the sign-in page", async () => {
    fakeApi({ "GET /api/admin-web/session": { status: 401 } });
    const navigation = watchNavigation();
    renderApp();

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/login"));
  });

  it("send a refused account to the sign-in page with the reason", async () => {
    fakeApi({ "GET /api/admin-web/session": { status: 403 } });
    const navigation = watchNavigation();
    renderApp();

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/login?error=denied"));
  });

  it("show the overview, every section of the menu and the session end in Tashkent time", async () => {
    fakeApi({ "GET /api/admin-web/session": { status: 200, body: session } });
    watchNavigation();
    renderApp();

    expect(await screen.findByRole("heading", { level: 1, name: "Обзор" })).toBeInTheDocument();
    const menu = screen.getByRole("navigation", { name: "Разделы админ-панели" });
    for (const label of ["Аналитика", "Воронка", "Пользователи", "Блогеры", "Компании", "Кампании", "Жалобы", "Сделки", "Журнал действий", "Безопасность"]) {
      expect(within(menu).getByText(label)).toBeInTheDocument();
    }
    expect(within(menu).getByRole("link", { name: "Обзор" })).toHaveAttribute("aria-current", "page");
    expect(within(menu).getByRole("link", { name: "Аналитика" })).toHaveAttribute("href", "/analytics");
    expect(within(menu).getByRole("link", { name: "Воронка" })).toHaveAttribute("href", "/funnel");
    expect(within(menu).getAllByText("скоро")).toHaveLength(8);
    expect(screen.getByText("Umid")).toBeInTheDocument();
    expect(screen.getByText("Владелец")).toBeInTheDocument();
    // 16:30 UTC is 21:30 in Tashkent (UTC+5).
    expect(screen.getByText("Сессия до 21:30 (Ташкент)")).toBeInTheDocument();
  });

  it("sign out on the server and open the sign-in page", async () => {
    const calls = fakeApi({ "GET /api/admin-web/session": { status: 200, body: session }, "POST /api/admin-web/logout": { status: 204 } });
    const navigation = watchNavigation();
    renderApp();

    fireEvent.click(await screen.findByRole("button", { name: "Выйти" }));

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/login"));
    expect(calls.some((call) => call.method === "POST" && call.url === "/api/admin-web/logout")).toBe(true);
  });

  it("open and close the menu on a narrow screen", async () => {
    fakeApi({ "GET /api/admin-web/session": { status: 200, body: { ...session, role: "admin" } } });
    watchNavigation();
    renderApp();

    const toggle = await screen.findByRole("button", { name: "Открыть меню" });
    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "true");
    expect(toggle).toHaveAccessibleName("Закрыть меню");
    fireEvent.keyDown(document, { key: "Escape" });
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(screen.getByText("Администратор")).toBeInTheDocument();
  });

  it("offer a retry when the API cannot be reached", async () => {
    fakeApi({ "GET /api/admin-web/session": { status: 500 } });
    watchNavigation();
    renderApp();

    expect(await screen.findByRole("button", { name: "Повторить" })).toBeInTheDocument();
  });
});
