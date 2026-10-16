import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  createComplaint: vi.fn(), getAdminComplaints: vi.fn(), closeAdminComplaint: vi.fn(),
  getAdminDashboard: vi.fn(), getAdminBloggers: vi.fn(), getAdminBusinesses: vi.fn(), setAdminUserBlocked: vi.fn(), sendAdminTipPreview: vi.fn()
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

  // Sources, the funnel, the journal and security moved to the admin site (D57); the Mini App only links to it.
  it("shows who opened the app and opens the full admin site in the browser", async () => {
    api.getAdminDashboard.mockResolvedValue({ users: 3, bloggers: 1, businesses: 1, publishedCampaigns: 0, completedDeals: 0, promotedBloggers: 0, promotedCampaigns: 0, activeToday: 2, activeWeek: 3, sources: [{ source: "insta", users: 12 }], adminWebUrl: "https://admin.example" });
    render(<I18nProvider><Admin /></I18nProvider>);

    expect(await screen.findByText(ru("admin.activityActive", { today: 2, week: 3 }))).toBeInTheDocument();
    expect(screen.queryByText("insta")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("link", { name: new RegExp(ru("admin.siteTitle")) }));
    expect(openLink).toHaveBeenCalledWith("https://admin.example");
  });

  it("shows no site link while the admin site is off", async () => {
    api.getAdminDashboard.mockResolvedValue({ users: 1, bloggers: 0, businesses: 0, publishedCampaigns: 0, completedDeals: 0, promotedBloggers: 0, promotedCampaigns: 0, adminWebUrl: null });
    render(<I18nProvider><Admin /></I18nProvider>);

    expect(await screen.findByText(ru("admin.tipsTitle"))).toBeInTheDocument();
    expect(screen.queryByText(ru("admin.siteTitle"))).not.toBeInTheDocument();
  });

  it("sends the admin the next bot tip from the admin home", async () => {
    api.getAdminDashboard.mockResolvedValue({ users: 1, bloggers: 0, businesses: 0, publishedCampaigns: 0, completedDeals: 0, promotedBloggers: 0, promotedCampaigns: 0 });
    api.sendAdminTipPreview.mockResolvedValue({ number: 2, total: 14, sent: true });
    render(<I18nProvider><Admin /></I18nProvider>);

    fireEvent.click(await screen.findByRole("button", { name: ru("admin.tipPreview") }));

    await waitFor(() => expect(api.sendAdminTipPreview).toHaveBeenCalledTimes(1));
    expect(await screen.findByRole("status")).toHaveTextContent("2");
  });

  // QA 2026-10-15: the owner can never be blocked, so a case about the owner's own profile offers no block button.
  it("offers no block button when the profile belongs to the platform owner", async () => {
    api.getAdminDashboard.mockResolvedValue({ users: 1, bloggers: 1, businesses: 0, publishedCampaigns: 0, completedDeals: 0, promotedBloggers: 0, promotedCampaigns: 0 });
    api.getAdminComplaints.mockResolvedValue([{ id: "c9", targetType: "blogger", targetId: "b9", targetName: "Owner blog", reason: "spam", status: "open", reporterTelegramUserId: 22, reporterRole: 1, createdAtUtc: "2026-10-12T08:00:00Z", ownerTelegramUserId: 4, ownerIsBlocked: false, ownerIsPlatformOwner: true }]);
    render(<I18nProvider><Admin section="complaints" /></I18nProvider>);

    expect(await screen.findByRole("link", { name: ru("admin.complaintOpenTarget") })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: ru("common.block") })).not.toBeInTheDocument();
  });

  it("opens the reported profile, blocks its owner and shows closed complaints", async () => {
    api.getAdminDashboard.mockResolvedValue({ users: 1, bloggers: 1, businesses: 0, publishedCampaigns: 0, completedDeals: 0, promotedBloggers: 0, promotedCampaigns: 0, openComplaints: 1 });
    const complaint = { id: "c1", targetType: "blogger", targetId: "b1", targetName: "Madina", reason: "fake", status: "open", reporterTelegramUserId: 22, reporterRole: 1, createdAtUtc: "2026-10-12T08:00:00Z", ownerTelegramUserId: 5, ownerIsBlocked: false };
    api.getAdminComplaints.mockResolvedValue([complaint]);
    api.setAdminUserBlocked.mockResolvedValue({ telegramUserId: 5, firstName: "Madina", role: 0, isBlocked: true, isDeleted: false, createdAtUtc: "2026-10-01T00:00:00Z" });
    render(<I18nProvider><Admin section="complaints" /></I18nProvider>);

    expect(await screen.findByRole("link", { name: ru("admin.complaintOpenTarget") })).toHaveAttribute("href", "#/blogger/b1");
    fireEvent.click(screen.getByRole("button", { name: ru("common.block") }));
    await waitFor(() => expect(api.setAdminUserBlocked).toHaveBeenCalledWith(5, true));
    expect(await screen.findByRole("button", { name: ru("common.unblock") })).toBeInTheDocument();

    api.getAdminComplaints.mockResolvedValue([{ ...complaint, id: "c2", status: "resolved", resolvedAtUtc: "2026-10-12T09:00:00Z", targetType: "deal", ownerTelegramUserId: null }]);
    fireEvent.click(screen.getByRole("button", { name: ru("admin.complaintStatus.resolved") }));
    await waitFor(() => expect(api.getAdminComplaints).toHaveBeenLastCalledWith("resolved"));
    expect(await screen.findByText(ru("admin.complaintTarget.deal"), { exact: false })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: ru("admin.complaintResolve") })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: ru("admin.complaintOpenTarget") })).not.toBeInTheDocument();
  });

  it("says access is denied only when the server refuses, and shows a load error otherwise", async () => {
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

  it("opens complaints and moderation from tiles; users and campaigns are counts only", async () => {
    api.getAdminDashboard.mockResolvedValue({ users: 8, bloggers: 5, businesses: 7, publishedCampaigns: 4, completedDeals: 6, promotedBloggers: 0, promotedCampaigns: 0, openComplaints: 2 });
    render(<I18nProvider><Admin /></I18nProvider>);

    expect(await screen.findByRole("link", { name: new RegExp(ru("admin.complaints")) })).toHaveAttribute("href", "#/admin/complaints");
    expect(screen.getByRole("link", { name: new RegExp(ru("admin.bloggers")) })).toHaveAttribute("href", "#/admin/bloggers");
    expect(screen.getByRole("link", { name: new RegExp(ru("admin.businesses")) })).toHaveAttribute("href", "#/admin/businesses");
    expect(screen.queryByRole("link", { name: new RegExp(ru("admin.users")) })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: new RegExp(ru("admin.campaigns")) })).not.toBeInTheDocument();
    expect(screen.getByText("8")).toBeInTheDocument();
  });

  it("shows activity for today and the last 7 days on the admin home", async () => {
    api.getAdminDashboard.mockResolvedValue({ users: 8, bloggers: 5, businesses: 7, publishedCampaigns: 4, completedDeals: 6, promotedBloggers: 0, promotedCampaigns: 0, newUsersToday: 2, newUsersWeek: 5, newDealsWeek: 3, completedDealsWeek: 1 });
    render(<I18nProvider><Admin /></I18nProvider>);

    expect(await screen.findByText(translate("admin.activityUsers", { today: 2, week: 5 }, "ru"))).toBeInTheDocument();
    expect(screen.getByText(translate("admin.activityDeals", { started: 3, completed: 1 }, "ru"))).toBeInTheDocument();
  });
});
