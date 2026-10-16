import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { App } from "../src/App";
import { I18nProvider } from "../src/i18n";
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
    id: "k1", targetType: "blogger", targetId: "b1", targetName: "Aziz", reason: "fraud", comment: "Took money", status: "open",
    reporterTelegramUserId: 55, reporterName: "Dilnoza", createdAtUtc: "2026-10-10T10:00:00Z", resolvedAtUtc: null, resolutionNote: null,
    ownerTelegramUserId: 77, ownerIsBlocked: false, ownerIsPlatformOwner: false
  };

  it("closes a case with a note after confirming and lets the owner block the person behind it", async () => {
    const { calls } = open("/complaints", {
      "GET /api/admin/complaints": { status: 200, body: [complaint] },
      "PATCH /api/admin/complaints/k1": { status: 200, body: { id: "k1", status: "resolved", alreadyReported: false } }
    });

    expect(await screen.findByText("Мошенничество")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Dilnoza" })).toHaveAttribute("href", "/users/55");
    expect(screen.getByRole("link", { name: "Карточка владельца" })).toHaveAttribute("href", "/users/77");
    expect(screen.getByRole("button", { name: "Заблокировать" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Решено" }));
    const dialog = screen.getByRole("dialog", { name: "Закрыть жалобу как решённую?" });
    fireEvent.change(within(dialog).getByRole("textbox"), { target: { value: "Warned the blogger" } });
    expect(within(dialog).getByText("18 из 500")).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Решено" }));

    expect(await screen.findByRole("status")).toHaveTextContent("Жалоба закрыта как решённая");
    expect(requests(calls, "PATCH", "/api/admin/complaints/k1")[0].body).toEqual({ status: "resolved", note: "Warned the blogger" });
    expect(requests(calls, "GET", "/api/admin/complaints")[0].url).toBe("/api/admin/complaints?status=open&take=100");
  });

  it("shows no block button to an admin", async () => {
    open("/complaints", { "GET /api/admin/complaints": { status: 200, body: [complaint] } }, admin);
    expect(await screen.findByRole("button", { name: "Решено" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Заблокировать" })).not.toBeInTheDocument();
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

  it("names actions, people and details in the journal and filters by section", async () => {
    open("/audit-log", {
      "GET /api/admin/audit-logs": { status: 200, body: [
        { id: "a1", actorTelegramUserId: 5044343262, action: "platform-user.role-updated", targetType: "PlatformUser", targetId: "42", details: "Admin", createdAtUtc: "2026-10-15T19:05:00Z" },
        { id: "a2", actorTelegramUserId: 5044343262, action: "blogger-profile.needs-changes", targetType: "BloggerProfile", targetId: "0026d75b-e3f9", details: null, createdAtUtc: "2026-10-15T18:00:00Z" },
        { id: "a3", actorTelegramUserId: 5044343262, action: "something.new", targetType: "Thing", targetId: "x", details: "raw", createdAtUtc: "2026-10-15T17:00:00Z" }
      ] },
      "GET /api/admin/users": { status: 200, body: [member, { ...member, telegramUserId: 5044343262, firstName: "Umid" }] }
    });

    const role = (await screen.findByText("Изменена роль пользователя")).closest("tr")!;
    expect(role).toHaveTextContent("16.10.2026 00:05");
    expect(within(role).getByRole("link", { name: "Umid" })).toHaveAttribute("href", "/users/5044343262");
    expect(within(role).getByRole("link", { name: "Madina" })).toHaveAttribute("href", "/users/42");
    expect(role).toHaveTextContent("Администратор");
    expect(screen.getByText("Анкета блогера отправлена на доработку").closest("tr")).toHaveTextContent("Блогер 0026d75b");
    expect(screen.getByText("something.new")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Модерация" }));
    expect(screen.queryByText("Изменена роль пользователя")).not.toBeInTheDocument();
    expect(screen.getByText("Анкета блогера отправлена на доработку")).toBeInTheDocument();
  });

  it("lists what page protection reported", async () => {
    open("/security", { "GET /api/admin/csp-reports": { status: 200, body: [{ directive: "script-src", blocked: "https://evil.example/x.js", source: "https://app/", count: 3, firstSeenAtUtc: "2026-10-15T10:00:00Z", lastSeenAtUtc: "2026-10-15T19:05:00Z" }] } });
    const row = (await screen.findByRole("rowheader", { name: "script-src" })).closest("tr")!;
    expect(row).toHaveTextContent("https://evil.example/x.js");
    expect(row).toHaveTextContent("16.10.2026 00:05");
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
