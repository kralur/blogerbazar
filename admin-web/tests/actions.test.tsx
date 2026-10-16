import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { App } from "../src/App";
import { I18nProvider } from "../src/i18n";
import { tashkentDate } from "../src/lib/format";
import { fakeApi, session, watchNavigation, type FakeRoute } from "./helpers";

const owner = { ...session, role: "owner" };
const admin = { ...session, role: "admin" };

const member = {
  telegramUserId: 42, firstName: "Madina", username: "madina", role: 0, isBlocked: false, isDeleted: false, deletedAtUtc: null,
  deletedByTelegramUserId: null, createdAtUtc: "2026-10-01T10:00:00Z", verifiedPhone: "+998901234567", hasBloggerProfile: true,
  hasBusinessProfile: false, hasBrandFaceProfile: false, lastSeenAtUtc: "2026-10-15T19:05:00Z", source: "insta"
};
const details = (user = member) => ({
  user,
  profiles: [{ role: "blogger", id: "p1", name: "Madina Blog", isHidden: false, isDeleted: false }],
  activeDeals: 1, completedDeals: 2, cancelledDeals: 0,
  recentDeals: [{ id: "d1", role: "blogger", partnerName: "Cafe", title: "Launch", status: 1, agreedPrice: 500000, createdAtUtc: "2026-10-02T10:00:00Z" }],
  complaintsAgainst: 1, openComplaintsAgainst: 0, complaintsFiled: 0
});

function open(path: string, routes: Record<string, FakeRoute>, signedIn = owner) {
  window.history.replaceState(null, "", path);
  const calls = fakeApi({ "GET /api/admin-web/session": { status: 200, body: signedIn }, ...routes });
  const navigation = watchNavigation();
  render(<I18nProvider><App /></I18nProvider>);
  return { calls, navigation };
}

const requests = (calls: { url: string; method: string }[], method: string, prefix: string) =>
  calls.filter((call) => call.method === method && call.url.startsWith(prefix));

describe("Users", () => {
  it("search after a pause, filter, and open a card from the name", async () => {
    const { calls } = open("/users", { "GET /api/admin/users": { status: 200, body: [member] }, "GET /api/admin/users/42": { status: 200, body: details() } });

    const link = await screen.findByRole("link", { name: "Madina" });
    expect(link).toHaveAttribute("href", "/users/42");
    const row = link.closest("tr")!;
    expect(within(row).getByText("@madina")).toBeInTheDocument();
    expect(row).toHaveTextContent("Участник");
    expect(row).toHaveTextContent("Блогер");
    // 19:05 UTC is 00:05 the next day in Tashkent.
    expect(row).toHaveTextContent("16.10.2026 00:05");
    expect(within(row).getByText("активен")).toBeInTheDocument();

    fireEvent.change(screen.getByRole("searchbox", { name: "Номер телефона, имя, @ник или id" }), { target: { value: "madi" } });
    await waitFor(() => expect(requests(calls, "GET", "/api/admin/users?").at(-1)?.url).toBe("/api/admin/users?take=200&search=madi"));
    fireEvent.click(screen.getByRole("button", { name: "Заблокированные" }));
    await waitFor(() => expect(requests(calls, "GET", "/api/admin/users?").at(-1)?.url).toBe("/api/admin/users?take=200&search=madi&filter=blocked"));

    fireEvent.click(await screen.findByRole("link", { name: "Madina" }));
    expect(await screen.findByRole("heading", { level: 1, name: "Карточка пользователя" })).toBeInTheDocument();
    expect(window.location.pathname).toBe("/users/42");
    expect(screen.getByRole("link", { name: "Пользователи" })).toHaveAttribute("aria-current", "page");
  });
});

describe("Users sorting and activity", () => {
  const recent = { ...member, telegramUserId: 1, firstName: "Bekzod", createdAtUtc: "2026-10-10T10:00:00Z", lastSeenAtUtc: new Date(Date.now() - 86_400_000).toISOString() };
  const old = { ...member, telegramUserId: 2, firstName: "Anvar", createdAtUtc: "2026-09-01T10:00:00Z", lastSeenAtUtc: new Date(Date.now() - 40 * 86_400_000).toISOString() };
  const never = { ...member, telegramUserId: 3, firstName: "Vali", createdAtUtc: "2026-08-01T10:00:00Z", lastSeenAtUtc: null };
  const names = () => screen.getAllByRole("rowheader").map((cell) => within(cell).getByRole("link").textContent);

  it("sorts by a column and filters by the last app open", async () => {
    open("/users", { "GET /api/admin/users": { status: 200, body: [old, never, recent] } });
    await screen.findByRole("link", { name: "Bekzod" });

    // Newest registration first by default.
    expect(names()).toEqual(["Bekzod", "Anvar", "Vali"]);
    fireEvent.click(screen.getByRole("button", { name: /Имя/ }));
    expect(names()).toEqual(["Anvar", "Bekzod", "Vali"]);
    expect(screen.getByRole("button", { name: /Имя/ }).closest("th")).toHaveAttribute("aria-sort", "ascending");
    fireEvent.click(screen.getByRole("button", { name: /Последний заход/ }));
    expect(names()).toEqual(["Bekzod", "Anvar", "Vali"]);

    fireEvent.click(screen.getByRole("button", { name: "Не заходили больше 30 дней" }));
    expect(names()).toEqual(["Anvar"]);
    fireEvent.click(screen.getByRole("button", { name: "Заходов не видно" }));
    expect(names()).toEqual(["Vali"]);
    fireEvent.click(screen.getByRole("button", { name: "Заходили за 7 дней" }));
    expect(names()).toEqual(["Bekzod"]);
    expect(screen.getByText("Найдено: 1")).toBeInTheDocument();
  });
});

describe("User card", () => {
  it("shows the account and lets the owner change the role after confirming", async () => {
    const { calls } = open("/users/42", {
      "GET /api/admin/users/42": { status: 200, body: details() },
      "PATCH /api/admin/users/42/role": { status: 200, body: { ...member, role: 3 } }
    });

    expect(await screen.findByText("+998901234567")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "@madina" })).toHaveAttribute("href", "https://t.me/madina");
    expect(screen.getByText("insta")).toBeInTheDocument();
    expect(screen.getByText("В работе: 1 · Завершены: 2 · Отменены: 0")).toBeInTheDocument();
    expect(screen.getByRole("rowheader", { name: "02.10.2026" }).closest("tr")).toHaveTextContent("завершена");

    fireEvent.change(screen.getByRole("combobox", { name: "Роль" }), { target: { value: "3" } });
    fireEvent.click(screen.getByRole("button", { name: "Изменить роль" }));
    const dialog = screen.getByRole("dialog", { name: "Изменить роль?" });
    expect(dialog).toHaveTextContent("Madina: Участник станет «Администратор»");
    // A dangerous change starts on "Cancel".
    expect(within(dialog).getByRole("button", { name: "Отмена" })).toHaveFocus();
    fireEvent.click(within(dialog).getByRole("button", { name: "Изменить" }));

    expect(await screen.findByRole("status")).toHaveTextContent("Роль изменена");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(requests(calls, "PATCH", "/api/admin/users/42/role")[0].body).toEqual({ role: 3 });
    expect(requests(calls, "GET", "/api/admin/users/42").length).toBe(2);
  });

  it("closes the dialog on Escape without changing anything", async () => {
    const { calls } = open("/users/42", { "GET /api/admin/users/42": { status: 200, body: details() } });

    fireEvent.click(await screen.findByRole("button", { name: "Заблокировать" }));
    fireEvent.keyDown(screen.getByRole("dialog"), { key: "Escape" });

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(requests(calls, "PATCH", "/api/admin/users")).toHaveLength(0);
  });

  it("keeps the dialog open with a clear reason when the server refuses", async () => {
    open("/users/42", { "GET /api/admin/users/42": { status: 200, body: details() }, "PATCH /api/admin/users/42/blocked": { status: 403, body: { code: "access_denied" } } });

    fireEvent.click(await screen.findByRole("button", { name: "Заблокировать" }));
    const dialog = screen.getByRole("dialog", { name: "Заблокировать пользователя?" });
    fireEvent.click(within(dialog).getByRole("button", { name: "Заблокировать" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent("Недостаточно прав для этого действия.");
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });

  it("sends an ended session to the sign-in page when an action is refused with 401", async () => {
    const { navigation } = open("/users/42", { "GET /api/admin/users/42": { status: 200, body: details({ ...member, isBlocked: true }) }, "PATCH /api/admin/users/42/blocked": { status: 401 } });

    fireEvent.click(await screen.findByRole("button", { name: "Разблокировать" }));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Разблокировать" }));

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/login?error=expired"));
  });

  it("offers no role or block changes to an admin, or for the owner", async () => {
    open("/users/42", { "GET /api/admin/users/42": { status: 200, body: details() } }, admin);
    expect(await screen.findByText("Менять роль и блокировать может только владелец.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Заблокировать" })).not.toBeInTheDocument();
  });

  it("protects the platform owner", async () => {
    open("/users/42", { "GET /api/admin/users/42": { status: 200, body: details({ ...member, role: 4 }) } });
    expect(await screen.findByText("Владельца нельзя заблокировать или сменить ему роль.")).toBeInTheDocument();
    expect(screen.queryByRole("combobox", { name: "Роль" })).not.toBeInTheDocument();
  });
});

describe("Moderation", () => {
  const blogger = { id: "b1", name: "Aziz", city: "tashkent", categories: ["beauty"], avatarUrl: null, totalFollowers: 12000, status: 0, createdAtUtc: "2026-10-10T10:00:00Z", telegramUserId: 77 };

  it("sends a pending blogger back for changes after confirming, and lists every profile", async () => {
    const { calls } = open("/moderation/bloggers", {
      "GET /api/admin/bloggers/pending": { status: 200, body: [blogger] },
      "GET /api/admin/bloggers": { status: 200, body: [blogger] },
      "POST /api/admin/bloggers/b1/needs-changes": { status: 200, body: { ...blogger, status: 4 } }
    });

    const queue = (await screen.findByRole("heading", { name: "Ждут проверки" })).closest("section")!;
    fireEvent.click(await within(queue).findByRole("button", { name: "На доработку" }));
    const dialog = screen.getByRole("dialog", { name: "Отправить «Aziz» на доработку?" });
    expect(dialog).toHaveTextContent("Он получит сообщение в бот.");
    fireEvent.click(within(dialog).getByRole("button", { name: "На доработку" }));

    expect(await screen.findByRole("status")).toHaveTextContent("«Aziz» отправлен на доработку");
    expect(requests(calls, "POST", "/api/admin/bloggers/b1/needs-changes")).toHaveLength(1);
    expect(requests(calls, "GET", "/api/admin/bloggers/pending").length).toBe(2);
    const all = screen.getByRole("heading", { name: "Все блогеры" }).closest("section")!;
    expect(within(all).getByRole("link", { name: "Aziz" })).toHaveAttribute("href", "/users/77");
    expect(within(all).getByRole("link", { name: "Aziz" }).closest("tr")).toHaveTextContent("Ташкент");
  });

  it("sends only what changed for a campaign", async () => {
    const campaign = { id: "c1", title: "Launch", businessName: "Cafe", status: 1, isPromoted: false, applicationsCount: 3, createdAtUtc: "2026-10-10T10:00:00Z", businessTelegramUserId: 88 };
    const { calls } = open("/moderation/campaigns", {
      "GET /api/admin/campaigns": { status: 200, body: [campaign] },
      "PATCH /api/admin/campaigns/c1": { status: 200, body: { ...campaign, status: 3 } }
    });

    fireEvent.click(await screen.findByRole("button", { name: "Изменить" }));
    const dialog = screen.getByRole("dialog", { name: "Кампания «Launch»" });
    fireEvent.change(within(dialog).getByRole("combobox", { name: "Статус" }), { target: { value: "3" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Сохранить" }));

    expect(await screen.findByRole("status")).toHaveTextContent("Кампания обновлена");
    expect(requests(calls, "PATCH", "/api/admin/campaigns/c1")[0].body).toEqual({ status: 3 });
  });
});

describe("Complaints", () => {
  const complaint = {
    id: "0f8a2d4e-1b2c-4d5e-8f90-123456789abc", targetType: "blogger", targetId: "b1", targetName: "Aziz", reason: "fraud", comment: "Took money", status: "open",
    reporterTelegramUserId: 55, reporterName: "Dilnoza", createdAtUtc: "2026-10-10T10:00:00Z", resolvedAtUtc: null, resolutionNote: null,
    ownerTelegramUserId: 77, ownerIsBlocked: false, ownerIsPlatformOwner: false
  };
  const casePath = `/complaints/${complaint.id}`;
  const review = {
    complaint,
    accusedTelegramUserId: 77,
    accused: details({ ...member, telegramUserId: 77, firstName: "Aziz", username: "aziz" }),
    reviews: { count: 3, average: 2.7, recent: [{ rating: 1, comment: "Never posted", reviewerName: "Cafe", createdAtUtc: "2026-10-05T10:00:00Z", isPublished: true }] },
    otherComplaints: [{ ...complaint, id: "11111111-2222-4333-8444-555555555555", reason: "spam", reporterName: "Bobur", status: "resolved" }],
    reporter: { person: { telegramUserId: 55, firstName: "Dilnoza", username: "dilnoza", createdAtUtc: "2026-09-01T10:00:00Z", isBlocked: false, isDeleted: false }, telegramUserId: 55, filed: 5, open: 1, resolved: 1, dismissed: 3 },
    sharedHistory: [
      { kind: "deal", id: "d1", title: "Launch", status: 2, createdAtUtc: "2026-10-01T10:00:00Z" },
      { kind: "offer", id: "o1", title: null, status: 3, createdAtUtc: "2026-09-20T10:00:00Z" }
    ]
  };

  it("lists cases with a link to review each one", async () => {
    const { calls } = open("/complaints", { "GET /api/admin/complaints": { status: 200, body: [complaint] } });

    expect(await screen.findByText("Мошенничество")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Dilnoza" })).toHaveAttribute("href", "/users/55");
    expect(screen.getByRole("link", { name: "Разобрать жалобу" })).toHaveAttribute("href", casePath);
    expect(screen.queryByRole("button", { name: "Решено" })).not.toBeInTheDocument();
    expect(requests(calls, "GET", "/api/admin/complaints")[0].url).toBe("/api/admin/complaints?status=open&take=100");
  });

  it("shows the accused record, the reporter history and their shared deals, then closes the case with a note", async () => {
    const { calls } = open(casePath, {
      [`GET /api/admin/complaints/${complaint.id}/review`]: { status: 200, body: review },
      [`PATCH /api/admin/complaints/${complaint.id}`]: { status: 200, body: { id: complaint.id, status: "resolved", alreadyReported: false } }
    });

    expect(await screen.findByRole("heading", { level: 1, name: "Разбор жалобы" })).toBeInTheDocument();
    const accused = screen.getByRole("heading", { name: "На кого жалоба" }).closest("section")!;
    expect(within(accused).getByRole("link", { name: "Aziz" })).toHaveAttribute("href", "/users/77");
    expect(within(accused).getByText("2,7")).toBeInTheDocument();
    expect(within(accused).getByText("1 из 5 · Cafe · 05.10.2026")).toBeInTheDocument();
    expect(within(accused).getByRole("link", { name: "10.10.2026" })).toHaveAttribute("href", "/complaints/11111111-2222-4333-8444-555555555555");
    const reporter = screen.getByRole("heading", { name: "Кто пожаловался" }).closest("section")!;
    expect(within(reporter).getByText("Открытые: 1 · Решённые: 1 · Отклонённые: 3")).toBeInTheDocument();
    expect(within(reporter).getByText("75%")).toBeInTheDocument();
    const shared = screen.getByRole("heading", { name: "Что было между ними" }).closest("section")!;
    expect(within(shared).getByText("Launch").closest("tr")).toHaveTextContent("отменена");
    expect(within(shared).getByText("Предложение без сделки").closest("tr")).toHaveTextContent("отклонено");
    expect(screen.getByRole("link", { name: "Все жалобы" })).toHaveAttribute("href", "/complaints");
    expect(screen.getByRole("link", { name: "Жалобы" })).toHaveAttribute("aria-current", "page");

    fireEvent.click(screen.getByRole("button", { name: "Решено" }));
    const dialog = screen.getByRole("dialog", { name: "Закрыть жалобу как решённую?" });
    fireEvent.change(within(dialog).getByRole("textbox"), { target: { value: "Warned the blogger" } });
    expect(within(dialog).getByText("18 из 500")).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Решено" }));

    expect(await screen.findByRole("status")).toHaveTextContent("Жалоба закрыта как решённая");
    expect(requests(calls, "PATCH", `/api/admin/complaints/${complaint.id}`)[0].body).toEqual({ status: "resolved", note: "Warned the blogger" });
  });

  it("lets only the owner block the accused", async () => {
    const routes = { [`GET /api/admin/complaints/${complaint.id}/review`]: { status: 200, body: review } };
    open(casePath, routes);
    expect(await screen.findByRole("button", { name: "Заблокировать того, на кого жалоба" })).toBeInTheDocument();
  });

  it("shows no block button to an admin", async () => {
    open(casePath, { [`GET /api/admin/complaints/${complaint.id}/review`]: { status: 200, body: review } }, admin);
    expect(await screen.findByRole("button", { name: "Решено" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Заблокировать того, на кого жалоба" })).not.toBeInTheDocument();
  });

  it("explains when the accused cannot be determined", async () => {
    open(casePath, { [`GET /api/admin/complaints/${complaint.id}/review`]: { status: 200, body: { ...review, accusedTelegramUserId: null, accused: null, otherComplaints: [], sharedHistory: [] } } });
    expect((await screen.findAllByText("Не удалось определить человека: объект удалён или жалоба не от участника.")).length).toBeGreaterThan(0);
  });
});

describe("Deals, journal and security", () => {
  it("lists deals with both sides and filters by status", async () => {
    const deal = { id: "d1", title: null, fromOffer: true, businessName: "Cafe", businessTelegramUserId: 88, creatorRole: "brandFace", creatorName: "Lola", creatorTelegramUserId: 99, status: 2, agreedPrice: null, createdAtUtc: "2026-10-10T10:00:00Z", completedAtUtc: null };
    const { calls } = open("/deals", { "GET /api/admin/deals": { status: 200, body: [deal] } });

    const row = (await screen.findByRole("link", { name: "Cafe" })).closest("tr")!;
    expect(row).toHaveTextContent("Предложение");
    expect(row).toHaveTextContent("Бренд-фейс");
    expect(row).toHaveTextContent("отменена");
    expect(within(row).getByRole("link", { name: "Lola" })).toHaveAttribute("href", "/users/99");
    fireEvent.click(screen.getByRole("button", { name: "Завершённые" }));
    await waitFor(() => expect(requests(calls, "GET", "/api/admin/deals").at(-1)?.url).toBe("/api/admin/deals?take=200&status=completed"));
  });

  it("names actions, people and details in the journal, searches and filters by section and days", async () => {
    const { calls } = open("/audit-log", {
      "GET /api/admin/audit-logs": { status: 200, body: [
        { id: "a1", actorTelegramUserId: 5044343262, action: "platform-user.role-updated", targetType: "PlatformUser", targetId: "42", details: "Admin", createdAtUtc: "2026-10-15T19:05:00Z" },
        { id: "a2", actorTelegramUserId: 5044343262, action: "blogger-profile.needs-changes", targetType: "BloggerProfile", targetId: "0026d75b-e3f9", details: null, createdAtUtc: "2026-10-15T18:00:00Z" },
        { id: "a3", actorTelegramUserId: 42, action: "platform-user.account-deleted", targetType: "PlatformUser", targetId: "21382833-aaaa-bbbb-cccc-dddddddddddd", details: null, createdAtUtc: "2026-10-15T17:00:00Z" },
        { id: "a4", actorTelegramUserId: 5044343262, action: "complaint.dismissed", targetType: "Complaint", targetId: "04d9b7a8-0000-4000-8000-000000000000", details: "blogger:0e996114", createdAtUtc: "2026-10-15T16:00:00Z" },
        { id: "a5", actorTelegramUserId: 5044343262, action: "something.new", targetType: "Thing", targetId: "x", details: "raw", createdAtUtc: "2026-10-15T15:00:00Z" }
      ] },
      "GET /api/admin/users": { status: 200, body: [member, { ...member, telegramUserId: 5044343262, firstName: "Umid", username: "umidkb" }] }
    });

    const role = (await screen.findByText("Изменена роль пользователя")).closest("tr")!;
    expect(requests(calls, "GET", "/api/admin/audit-logs")[0].url).toBe(`/api/admin/audit-logs?take=500&from=${tashkentDate(-29)}&to=${tashkentDate()}`);
    expect(role).toHaveTextContent("16.10.2026 00:05");
    expect(within(role).getByRole("link", { name: "Umid" })).toHaveAttribute("href", "/users/5044343262");
    expect(within(role).getByRole("link", { name: "Madina" })).toHaveAttribute("href", "/users/42");
    expect(role).toHaveTextContent("Администратор");
    // A deleted account names the person, not the internal id.
    const deleted = screen.getByText("Пользователь удалил аккаунт").closest("tr")!;
    expect(within(deleted).getAllByRole("link", { name: "Madina" })).toHaveLength(2);
    expect(deleted).not.toHaveTextContent("21382833");
    const complaintRow = screen.getByText("Жалоба отклонена").closest("tr")!;
    expect(within(complaintRow).getByRole("link", { name: "Жалоба на: Блогер" })).toHaveAttribute("href", "/complaints/04d9b7a8-0000-4000-8000-000000000000");
    expect(complaintRow).not.toHaveTextContent("0e996114");
    expect(screen.getByText("something.new")).toBeInTheDocument();

    fireEvent.change(screen.getByRole("searchbox", { name: "Поиск: имя, @ник, id или действие" }), { target: { value: "@madina" } });
    expect(screen.getByText("Записей: 2")).toBeInTheDocument();
    expect(screen.queryByText("Жалоба отклонена")).not.toBeInTheDocument();
    fireEvent.change(screen.getByRole("searchbox", { name: "Поиск: имя, @ник, id или действие" }), { target: { value: "" } });

    fireEvent.click(screen.getByRole("button", { name: "Модерация" }));
    expect(screen.queryByText("Изменена роль пользователя")).not.toBeInTheDocument();
    expect(screen.getByText("Анкета блогера отправлена на доработку")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "7 дней" }));
    await waitFor(() => expect(requests(calls, "GET", "/api/admin/audit-logs").at(-1)?.url).toBe(`/api/admin/audit-logs?take=500&from=${tashkentDate(-6)}&to=${tashkentDate()}`));
  });

  it("lists what page protection reported", async () => {
    open("/security", { "GET /api/admin/csp-reports": { status: 200, body: [{ directive: "script-src", blocked: "https://evil.example/x.js", source: "https://app/", count: 3, firstSeenAtUtc: "2026-10-15T10:00:00Z", lastSeenAtUtc: "2026-10-15T19:05:00Z" }] } });
    const row = (await screen.findByRole("rowheader", { name: "script-src" })).closest("tr")!;
    expect(row).toHaveTextContent("https://evil.example/x.js");
    expect(row).toHaveTextContent("16.10.2026 00:05");
  });
});

describe("CSV export", () => {
  function captureDownloads() {
    const files: { name: string; blob: Blob }[] = [];
    vi.stubGlobal("URL", Object.assign(URL, { createObjectURL: (blob: Blob) => { files.push({ name: "", blob }); return "blob:test"; }, revokeObjectURL: () => undefined }));
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (this: HTMLAnchorElement) { files[files.length - 1].name = this.download; });
    return { files, click };
  }

  it("warns about people's data, writes the journal entry first and marks those columns", async () => {
    const downloads = captureDownloads();
    const { calls } = open("/users", { "GET /api/admin/users": { status: 200, body: [member] }, "POST /api/admin-web/exports": { status: 204 } });
    await screen.findByRole("link", { name: "Madina" });

    fireEvent.click(screen.getByRole("button", { name: "Скачать CSV" }));
    const dialog = screen.getByRole("dialog", { name: "Скачать файл с персональными данными?" });
    expect(dialog).toHaveTextContent("Строк: 1.");
    fireEvent.click(within(dialog).getByRole("button", { name: "Скачать" }));

    expect(await screen.findByText("Файл скачан")).toBeInTheDocument();
    expect(requests(calls, "POST", "/api/admin-web/exports")[0].body).toEqual({ kind: "users", rows: 1 });
    expect(downloads.files[0].name).toMatch(/^bloggerbazar-users-personal-data-\d{4}-\d{2}-\d{2}\.csv$/);
    const bytes = new Uint8Array(await downloads.files[0].blob.arrayBuffer());
    // UTF-8 byte order mark first, so Excel reads Cyrillic correctly.
    expect([...bytes.slice(0, 3)]).toEqual([0xef, 0xbb, 0xbf]);
    const text = new TextDecoder().decode(bytes);
    expect(text.split("\r\n")[0]).toBe("Имя;Ник (ПД);Telegram id (ПД);Телефон из Telegram (ПД);Роль;Профили;Регистрация;Последний заход;Метка, по которой пришёл;Состояние");
    expect(text.split("\r\n")[1]).toBe("Madina;'@madina;42;'+998901234567;Участник;Блогер;01.10.2026 15:00;16.10.2026 00:05;insta;активен");
  });

  it("saves nothing when the journal entry is refused", async () => {
    const downloads = captureDownloads();
    open("/deals", { "GET /api/admin/deals": { status: 200, body: [] }, "POST /api/admin-web/exports": { status: 403 } });

    fireEvent.click(await screen.findByRole("button", { name: "Скачать CSV" }));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Скачать" }));

    expect(await within(screen.getByRole("dialog")).findByRole("alert")).toHaveTextContent("Недостаточно прав");
    expect(downloads.click).not.toHaveBeenCalled();
  });
});

describe("Sign out on all devices", () => {
  it("asks first, ends every session on the server and opens the sign-in page", async () => {
    const { calls, navigation } = open("/security", { "GET /api/admin/csp-reports": { status: 200, body: [] }, "POST /api/admin-web/logout-all": { status: 204 } });

    fireEvent.click(await screen.findByRole("button", { name: "Выйти на всех устройствах" }));
    const dialog = screen.getByRole("dialog", { name: "Выйти на всех устройствах?" });
    fireEvent.click(within(dialog).getByRole("button", { name: "Выйти на всех устройствах" }));

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/login"));
    expect(requests(calls, "POST", "/api/admin-web/logout-all")).toHaveLength(1);
  });

  it("treats a malformed card address as an unknown page", async () => {
    open("/users/abc", {});
    expect(await screen.findByRole("heading", { level: 1, name: "Страница не найдена" })).toBeInTheDocument();
  });
});
