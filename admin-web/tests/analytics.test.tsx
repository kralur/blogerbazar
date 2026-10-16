import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { App } from "../src/App";
import { I18nProvider } from "../src/i18n";
import { formatNumber, tashkentDate } from "../src/lib/format";
import { fakeApi, session, watchNavigation, type FakeRoute } from "./helpers";

const day = (date: string, values: Partial<Record<string, number>> = {}) => ({
  date, newUsers: 0, lastSeen: 0, campaigns: 0, applications: 0, offers: 0, deals: 0, completedDeals: 0, ...values
});

const daily = {
  from: "2026-10-14",
  to: "2026-10-16",
  days: [day("2026-10-14", { newUsers: 2, deals: 1 }), day("2026-10-15", { newUsers: 3 }), day("2026-10-16", { newUsers: 4, lastSeen: 6, deals: 2, completedDeals: 1 })]
};

const dashboard = {
  users: 1250, bloggers: 300, businesses: 40, publishedCampaigns: 12, completedDeals: 9, promotedBloggers: 2, promotedCampaigns: 1,
  openComplaints: 3, newUsersToday: 0, newUsersWeek: 70, newDealsWeek: 5, completedDealsWeek: 4, activeToday: 0, activeWeek: 210, sources: []
};

const reports: Record<string, FakeRoute> = {
  "GET /api/admin-web/session": { status: 200, body: session },
  "GET /api/admin/dashboard": { status: 200, body: dashboard },
  "GET /api/admin/analytics/daily": { status: 200, body: daily },
  "GET /api/admin/analytics/retention": { status: 200, body: { from: "2026-07-19", to: "2026-10-16", cohort: 3, steps: [{ days: 1, eligible: 3, returned: 2 }, { days: 7, eligible: 2, returned: 1 }, { days: 30, eligible: 0, returned: 0 }] } },
  "GET /api/admin/analytics/money": { status: 200, body: { months: [
    { month: "2026-09", completedDeals: 1, pricedDeals: 0, total: 0, average: null },
    { month: "2026-10", completedDeals: 5, pricedDeals: 4, total: 3_500_001, average: 875_000 }
  ] } },
  "GET /api/admin/analytics/geography": { status: 200, body: {
    cities: [{ key: "Tashkent", bloggers: 5, brandFaces: 1, campaigns: 2, deals: 3 }],
    categories: [{ key: "other:Котики", bloggers: 1, brandFaces: 0, campaigns: 0, deals: 0 }]
  } },
  "GET /api/admin/analytics/sources": { status: 200, body: { sources: [{ source: null, users: 10, withProfile: 4, withDeal: 1 }, { source: "insta", users: 4, withProfile: 2, withDeal: 2 }] } },
  "GET /api/admin/funnel": { status: 200, body: { period: "30d", fromUtc: null, roles: [
    { role: "blogger", steps: [{ key: "opened", count: 100 }, { key: "roleSelected", count: 80 }, { key: "profile", count: 20 }, { key: "reached", count: 15 }] },
    { role: "brandFace", steps: [{ key: "opened", count: 0 }, { key: "roleSelected", count: 0 }] },
    { role: "business", steps: [{ key: "opened", count: 100 }, { key: "roleSelected", count: 50 }] }
  ] } }
};

function open(path: string, routes: Record<string, FakeRoute> = reports) {
  window.history.replaceState(null, "", path);
  const calls = fakeApi(routes);
  const navigation = watchNavigation();
  render(<I18nProvider><App /></I18nProvider>);
  return { calls, navigation };
}

// The number under a tile's name; numbers use the app's own grouping (non-breaking spaces).
async function tile(label: string, scope: HTMLElement = document.body) {
  const [name] = await within(scope).findAllByText(label, { selector: "p" });
  return name.nextElementSibling?.textContent;
}

const urls = (calls: { url: string }[], prefix: string) => calls.map((call) => call.url).filter((url) => url.startsWith(prefix));

describe("Overview", () => {
  it("shows the totals, the last 7 days, today in Tashkent and two 30-day charts", async () => {
    const { calls } = open("/");

    expect(await tile("Пользователи")).toBe(formatNumber(1250));
    expect(await tile("Открытые жалобы")).toBe("3");
    const week = screen.getByRole("heading", { name: "За последние 7 дней" }).nextElementSibling as HTMLElement;
    expect(await tile("Заходили в приложение", week)).toBe("210");
    // Today comes from the last day of the daily report, a Tashkent day: 6 people opened the app.
    const today = screen.getByRole("heading", { name: "Сегодня" }).closest("section")!;
    expect(await tile("Заходили в приложение", today)).toBe("6");
    expect(screen.getByRole("img", { name: "Новые пользователи: 9 с 14.10.2026 по 16.10.2026" })).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Сделки: 3 с 14.10.2026 по 16.10.2026" })).toBeInTheDocument();
    expect(urls(calls, "/api/admin/analytics/daily")).toEqual([`/api/admin/analytics/daily?from=${tashkentDate(-29)}&to=${tashkentDate()}`]);
  });
});

describe("Analytics", () => {
  it("asks for the last 30 Tashkent days first and again for 7 days on a click", async () => {
    const { calls } = open("/analytics");

    expect(await screen.findByRole("heading", { level: 1, name: "Аналитика" })).toBeInTheDocument();
    await screen.findByRole("img", { name: /^Активные пользователи: 6/ });
    expect(screen.getByRole("link", { name: "Аналитика" })).toHaveAttribute("aria-current", "page");
    fireEvent.click(screen.getByRole("button", { name: "7 дней" }));

    await waitFor(() => expect(urls(calls, "/api/admin/analytics/daily")).toEqual([
      `/api/admin/analytics/daily?from=${tashkentDate(-29)}&to=${tashkentDate()}`,
      `/api/admin/analytics/daily?from=${tashkentDate(-6)}&to=${tashkentDate()}`
    ]));
    expect(urls(calls, "/api/admin/analytics/retention").at(-1)).toBe(`/api/admin/analytics/retention?from=${tashkentDate(-6)}&to=${tashkentDate()}`);
    expect(screen.getByRole("button", { name: "7 дней" })).toHaveAttribute("aria-pressed", "true");
  });

  it("shows the daily numbers as a table, newest day first", async () => {
    open("/analytics");
    const report = (await screen.findByRole("heading", { name: "По дням" })).closest("section")!;
    await within(report).findAllByRole("img");

    fireEvent.click(within(report).getByRole("button", { name: "Таблица" }));

    const rows = within(report).getAllByRole("row");
    expect(rows[1]).toHaveTextContent("16.10.2026");
    expect(rows[3]).toHaveTextContent("14.10.2026");
    expect(within(report).queryByRole("img")).not.toBeInTheDocument();
  });

  it("refuses a custom period that ends before it starts, without asking the server", async () => {
    const { calls } = open("/analytics");
    await screen.findAllByRole("img");
    fireEvent.click(screen.getByRole("button", { name: "Свои даты" }));
    fireEvent.change(screen.getByLabelText("Начало"), { target: { value: "2026-10-10" } });
    fireEvent.change(screen.getByLabelText("Конец"), { target: { value: "2026-10-01" } });
    const before = urls(calls, "/api/admin/analytics/daily").length;

    fireEvent.click(screen.getByRole("button", { name: "Показать" }));

    expect(screen.getByRole("alert")).toHaveTextContent("Начало периода должно быть не позже конца.");
    expect(urls(calls, "/api/admin/analytics/daily")).toHaveLength(before);
  });

  it("asks for a custom period once it is valid", async () => {
    const { calls } = open("/analytics");
    await screen.findAllByRole("img");
    fireEvent.click(screen.getByRole("button", { name: "Свои даты" }));
    fireEvent.change(screen.getByLabelText("Начало"), { target: { value: "2026-01-01" } });
    fireEvent.change(screen.getByLabelText("Конец"), { target: { value: "2026-03-31" } });
    fireEvent.click(screen.getByRole("button", { name: "Показать" }));

    await waitFor(() => expect(urls(calls, "/api/admin/analytics/daily").at(-1)).toBe("/api/admin/analytics/daily?from=2026-01-01&to=2026-03-31"));
    expect(screen.getByText("С 01.01.2026 по 31.03.2026, по времени Ташкента")).toBeInTheDocument();
  });

  it("shows retention as an estimate, money in sums, translated cities and the ad labels", async () => {
    open("/analytics");

    const retention = (await screen.findByRole("heading", { name: "Возвращаемость" })).closest("section")!;
    expect(within(retention).getByText(/^Примерно:/)).toBeInTheDocument();
    expect(await within(retention).findByText("Вернулись через 1 день")).toBeInTheDocument();
    expect(within(retention).getByText("67%")).toBeInTheDocument();
    expect(within(retention).getByText("2 из 3")).toBeInTheDocument();
    expect(within(retention).getByText("Вернулись через 7 дней")).toBeInTheDocument();
    expect(within(retention).getByText("Пока рано: никто не пришёл так давно")).toBeInTheDocument();

    const money = screen.getByRole("heading", { name: "Деньги по сделкам" }).closest("section")!;
    expect(await tile("Сумма", money)).toBe(`${formatNumber(3_500_001)} сум`);
    // The average counts only the four deals with a price.
    expect(await tile("Средняя цена", money)).toBe(`${formatNumber(875_000)} сум`);
    fireEvent.click(within(money).getByRole("button", { name: "Таблица" }));
    expect(within(money).getAllByRole("row")[1]).toHaveTextContent("окт 2026");
    expect(within(money).getAllByRole("row")[2]).toHaveTextContent("–");

    const geography = screen.getByRole("heading", { name: "Города и категории" }).closest("section")!;
    expect(await within(geography).findByRole("rowheader", { name: "Ташкент" })).toBeInTheDocument();
    expect(within(geography).getByRole("rowheader", { name: "Котики" })).toBeInTheDocument();

    const sources = screen.getByRole("heading", { name: "Источники" }).closest("section")!;
    expect(await within(sources).findByRole("rowheader", { name: "Без метки" })).toBeInTheDocument();
    expect(within(sources).getByRole("rowheader", { name: "insta" }).closest("tr")).toHaveTextContent("50%");
  });

  it("speaks Uzbek with Uzbek month names and sums", async () => {
    localStorage.setItem("bloggerbazar.admin.language", "uz");
    open("/analytics");

    expect(await tile("Jami")).toBe(`${formatNumber(3_500_001)} so‘m`);
    expect(screen.getByRole("heading", { name: "Qaytish" })).toBeInTheDocument();
    expect(screen.getByRole("rowheader", { name: "Toshkent" })).toBeInTheDocument();
  });

  it("sends an expired session to the sign-in page", async () => {
    const { navigation } = open("/analytics", { ...reports, "GET /api/admin/analytics/daily": { status: 401 } });

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/login?error=expired"));
  });

  it("offers a retry for one report that failed and keeps the others", async () => {
    open("/analytics", { ...reports, "GET /api/admin/analytics/sources": { status: 500 } });

    const sources = (await screen.findByRole("heading", { name: "Источники" })).closest("section")!;
    expect(await within(sources).findByRole("alert")).toHaveTextContent("Не удалось загрузить данные");
    expect(within(sources).getByRole("button", { name: "Повторить" })).toBeInTheDocument();
    expect(await screen.findByRole("rowheader", { name: "Ташкент" })).toBeInTheDocument();
  });
});

describe("Funnel", () => {
  it("shows each step against the first one and marks the biggest drop", async () => {
    const { calls } = open("/funnel");

    expect(await screen.findByText("Заполнили профиль")).toBeInTheDocument();
    expect(screen.getByText("80% от открывших · 80% от прошлого шага")).toBeInTheDocument();
    expect(screen.getByText("20% от открывших · 25% от прошлого шага")).toBeInTheDocument();
    const profile = screen.getByText("Заполнили профиль").closest("li")!;
    expect(within(profile).getByText("Больше всего уходят здесь")).toBeInTheDocument();
    expect(urls(calls, "/api/admin/funnel")).toEqual(["/api/admin/funnel?period=30d"]);

    fireEvent.click(screen.getByRole("button", { name: "7 дней" }));
    await waitFor(() => expect(urls(calls, "/api/admin/funnel").at(-1)).toBe("/api/admin/funnel?period=7d"));
  });

  it("switches roles, says when nobody came and shows the steps as a table", async () => {
    open("/funnel");
    await screen.findByText("Заполнили профиль");

    fireEvent.click(screen.getByRole("button", { name: "Бренд-фейсы" }));
    expect(screen.getByText("За этот период новых людей не было")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Бизнес" }));
    fireEvent.click(screen.getByRole("button", { name: "Таблица" }));
    const rows = screen.getAllByRole("row");
    expect(rows[1]).toHaveTextContent("Открыли приложение");
    expect(rows[2]).toHaveTextContent("50%");
  });
});
