import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  createComplaint: vi.fn(), getAdminComplaints: vi.fn(), closeAdminComplaint: vi.fn(),
  getAdminDashboard: vi.fn(), getAdminUsers: vi.fn(), getAdminAuditLogs: vi.fn(), getAdminBloggers: vi.fn(), getAdminBusinesses: vi.fn(), getAdminCampaigns: vi.fn()
}));

vi.mock("../src/api/marketplace", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/marketplace")>()), ...api }));
const openLink = vi.hoisted(() => vi.fn());
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ openLink, haptic: { impact: vi.fn(), success: vi.fn(), selection: vi.fn(), error: vi.fn(), warning: vi.fn() } }) }));
vi.mock("../src/hooks/useProfileDataRefresh", () => ({ useProfileDataRefresh: () => undefined }));
vi.mock("../src/components/ui", () => ({
  Avatar: ({ name }: { name: string }) => <span>{name}</span>,
  Badge: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
  BottomNav: () => null,
  Button: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button>,
  Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>,
  ErrorState: ({ title }: { title: string }) => <h1>{title}</h1>,
  Icon: () => <svg />,
  LoadingState: () => <p>loading</p>,
  Modal: ({ children, open, title }: { children: React.ReactNode; open: boolean; title: string }) => open ? <section aria-label={title}>{children}</section> : null,
  StatsCard: ({ label, value }: { label: string; value: string }) => <p>{label}: {value}</p>,
  Textarea: ({ label, ...props }: React.TextareaHTMLAttributes<HTMLTextAreaElement> & { label?: string }) => <label>{label}<textarea {...props} /></label>,
  Toast: ({ message }: { message: string }) => message ? <p role="status">{message}</p> : null
}));

import { ReportButton } from "../src/components/ReportButton";
import { Admin } from "../src/pages/Admin";

const ru = (key: string, params?: Record<string, string | number>) => translate(key, params, "ru");
const openForm = async () => {
  render(<I18nProvider><ReportButton targetId="blogger-1" targetType="blogger" /></I18nProvider>);
  fireEvent.click(screen.getByRole("button", { name: ru("report.action") }));
  return screen.getByLabelText(ru("report.title"));
};

describe("Complaints (D52)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.createComplaint.mockResolvedValue({ id: "c1", status: "open", alreadyReported: false });
  });

  it("needs a reason and words for other before sending", async () => {
    const dialog = await openForm();

    fireEvent.click(within(dialog).getByRole("button", { name: ru("report.send") }));
    expect(within(dialog).getByRole("alert")).toHaveTextContent(ru("report.reasonRequired"));

    fireEvent.click(within(dialog).getByLabelText(ru("report.reason.other")));
    fireEvent.click(within(dialog).getByRole("button", { name: ru("report.send") }));
    expect(within(dialog).getByRole("alert")).toHaveTextContent(ru("report.commentRequired"));
    expect(api.createComplaint).not.toHaveBeenCalled();
  });

  it("sends the chosen reason and thanks the reporter", async () => {
    const dialog = await openForm();

    fireEvent.click(within(dialog).getByLabelText(ru("report.reason.fake")));
    fireEvent.change(within(dialog).getByLabelText(ru("report.commentLabel")), { target: { value: "Bought followers" } });
    fireEvent.click(within(dialog).getByRole("button", { name: ru("report.send") }));

    await waitFor(() => expect(api.createComplaint).toHaveBeenCalledWith("blogger", "blogger-1", "fake", "Bought followers"));
    expect(await screen.findByText(ru("report.sent"))).toBeInTheDocument();
  });

  it("says when the same thing was already reported", async () => {
    api.createComplaint.mockResolvedValue({ id: "c1", status: "open", alreadyReported: true });
    const dialog = await openForm();

    fireEvent.click(within(dialog).getByLabelText(ru("report.reason.spam")));
    fireEvent.click(within(dialog).getByRole("button", { name: ru("report.send") }));

    expect(await screen.findByText(ru("report.already"))).toBeInTheDocument();
  });

  it("explains that the own profile cannot be reported", async () => {
    api.createComplaint.mockRejectedValue(new ApiError(409, "complaint_own"));
    const dialog = await openForm();

    fireEvent.click(within(dialog).getByLabelText(ru("report.reason.spam")));
    fireEvent.click(within(dialog).getByRole("button", { name: ru("report.send") }));

    expect(await screen.findByText(ru("error.complaint_own"))).toBeInTheDocument();
  });

  it("lists open complaints for the admin and closes one", async () => {
    api.getAdminDashboard.mockResolvedValue({ users: 1, bloggers: 1, businesses: 1, publishedCampaigns: 0, completedDeals: 0, promotedBloggers: 0, promotedCampaigns: 0, openComplaints: 1 });
    api.getAdminUsers.mockResolvedValue([]);
    api.getAdminAuditLogs.mockResolvedValue([]);
    api.getAdminBloggers.mockResolvedValue([]);
    api.getAdminComplaints.mockResolvedValue([{ id: "c1", targetType: "blogger", targetId: "b1", targetName: "Madina", reason: "fake", comment: "Bought followers", status: "open", reporterTelegramUserId: 22, reporterName: "Lumi", reporterRole: 1, createdAtUtc: "2026-10-12T08:00:00Z" }]);
    api.closeAdminComplaint.mockResolvedValue({ id: "c1", status: "resolved", alreadyReported: false });
    render(<I18nProvider><Admin section="complaints" /></I18nProvider>);

    expect(await screen.findByText("Madina")).toBeInTheDocument();
    expect(screen.getByText("Bought followers")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: ru("admin.complaintResolve") }));

    await waitFor(() => expect(api.closeAdminComplaint).toHaveBeenCalledWith("c1", "resolved"));
    expect(await screen.findByText(ru("admin.complaintsEmpty"))).toBeInTheDocument();
  });

  it("says access is denied only when the server refuses, and shows a load error otherwise", async () => {
    api.getAdminUsers.mockResolvedValue([]);
    api.getAdminAuditLogs.mockResolvedValue([]);
    api.getAdminBloggers.mockResolvedValue([]);
    api.getAdminComplaints.mockResolvedValue([]);
    api.getAdminDashboard.mockRejectedValue(new ApiError(403, "access_denied"));
    const denied = render(<I18nProvider><Admin /></I18nProvider>);
    expect(await screen.findByText(ru("ui.accessDenied"))).toBeInTheDocument();
    denied.unmount();

    api.getAdminDashboard.mockRejectedValue(new ApiError(500, "server_error"));
    render(<I18nProvider><Admin /></I18nProvider>);
    expect(await screen.findByText(ru("common.openFailed"))).toBeInTheDocument();
    expect(screen.queryByText(ru("ui.accessDenied"))).not.toBeInTheDocument();
  });

  it("shows tiles that open their own lists, with open complaints highlighted", async () => {
    api.getAdminDashboard.mockResolvedValue({ users: 8, bloggers: 5, businesses: 7, publishedCampaigns: 4, completedDeals: 6, promotedBloggers: 0, promotedCampaigns: 0, openComplaints: 2 });
    render(<I18nProvider><Admin /></I18nProvider>);

    expect(await screen.findByRole("link", { name: new RegExp(ru("admin.users")) })).toHaveAttribute("href", "#/admin/users");
    expect(screen.getByRole("link", { name: new RegExp(ru("admin.complaints")) })).toHaveAttribute("href", "#/admin/complaints");
    expect(screen.getByRole("link", { name: new RegExp(ru("admin.logTitle")) })).toHaveAttribute("href", "#/admin/log");
    expect(api.getAdminUsers).not.toHaveBeenCalled();
  });

  it("writes the journal in words with the person's name instead of raw ids", async () => {
    api.getAdminDashboard.mockResolvedValue({ users: 1, bloggers: 0, businesses: 0, publishedCampaigns: 0, completedDeals: 0, promotedBloggers: 0, promotedCampaigns: 0 });
    api.getAdminUsers.mockResolvedValue([{ telegramUserId: 7, firstName: "Umid", role: 4, isBlocked: false, isDeleted: false, createdAtUtc: "2026-10-01T00:00:00Z" }, { telegramUserId: 9, firstName: "Ali", role: 0, isBlocked: true, isDeleted: false, createdAtUtc: "2026-10-01T00:00:00Z" }]);
    api.getAdminAuditLogs.mockResolvedValue([{ id: "l1", actorTelegramUserId: 7, action: "platform-user.blocked", targetType: "PlatformUser", targetId: "9", createdAtUtc: "2026-10-12T08:00:00Z" }]);
    render(<I18nProvider><Admin section="log" /></I18nProvider>);

    expect(await screen.findByText(ru("admin.log.platform-user.blocked"))).toBeInTheDocument();
    expect(screen.getByText(/Umid → Ali/)).toBeInTheDocument();
  });

  it("searches users by phone, filters them and opens a Telegram chat", async () => {
    api.getAdminDashboard.mockResolvedValue({ users: 2, bloggers: 1, businesses: 0, publishedCampaigns: 0, completedDeals: 0, promotedBloggers: 0, promotedCampaigns: 0 });
    api.getAdminUsers.mockResolvedValue([{ telegramUserId: 9, firstName: "Ali", username: "miraskarov", role: 0, isBlocked: false, isDeleted: false, createdAtUtc: "2026-10-01T00:00:00Z", verifiedPhone: "+998901234567", hasBloggerProfile: true }]);
    render(<I18nProvider><Admin section="users" /></I18nProvider>);

    expect(await screen.findByText(/\+998901234567/)).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText(ru("admin.userSearch")), { target: { value: "90 123" } });
    await waitFor(() => expect(api.getAdminUsers).toHaveBeenLastCalledWith("90 123", "all"), { timeout: 2000 });
    fireEvent.click(screen.getByRole("button", { name: ru("admin.userFilter.blocked") }));
    await waitFor(() => expect(api.getAdminUsers).toHaveBeenLastCalledWith("90 123", "blocked"), { timeout: 2000 });

    fireEvent.click(screen.getByRole("button", { name: ru("admin.writeInTelegram") }));
    expect(openLink).toHaveBeenCalledWith("https://t.me/miraskarov");
  });

  it("shows activity for today and the last 7 days on the admin home", async () => {
    api.getAdminDashboard.mockResolvedValue({ users: 8, bloggers: 5, businesses: 7, publishedCampaigns: 4, completedDeals: 6, promotedBloggers: 0, promotedCampaigns: 0, newUsersToday: 2, newUsersWeek: 5, newDealsWeek: 3, completedDealsWeek: 1 });
    render(<I18nProvider><Admin /></I18nProvider>);

    const newUsers = await screen.findByRole("link", { name: new RegExp(translate("admin.activityUsers", { today: 2, week: 5 }, "ru")) });
    expect(newUsers).toHaveAttribute("href", "#/admin/users?filter=new");
    expect(screen.getByText(translate("admin.activityDeals", { started: 3, completed: 1 }, "ru"))).toBeInTheDocument();
  });

  it("opens new users from the activity link with their registration date", async () => {
    window.location.hash = "#/admin/users?filter=new";
    api.getAdminDashboard.mockResolvedValue({ users: 1, bloggers: 0, businesses: 0, publishedCampaigns: 0, completedDeals: 0, promotedBloggers: 0, promotedCampaigns: 0, newUsersToday: 1, newUsersWeek: 1 });
    api.getAdminUsers.mockResolvedValue([{ telegramUserId: 11, firstName: "Newcomer", role: 0, isBlocked: false, isDeleted: false, createdAtUtc: "2026-10-12T08:00:00Z" }]);
    render(<I18nProvider><Admin section="users" /></I18nProvider>);

    expect(await screen.findByText("Newcomer")).toBeInTheDocument();
    await waitFor(() => expect(api.getAdminUsers).toHaveBeenLastCalledWith("", "new"), { timeout: 2000 });
    expect(screen.getByRole("button", { name: ru("admin.userFilter.new") })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByText(/^Регистрация: /)).toBeInTheDocument();
    window.location.hash = "";
  });
});
