import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  createComplaint: vi.fn(), getAdminComplaints: vi.fn(), closeAdminComplaint: vi.fn(),
  getAdminDashboard: vi.fn(), getAdminUsers: vi.fn(), getAdminAuditLogs: vi.fn(), getAdminBloggers: vi.fn()
}));

vi.mock("../src/api/marketplace", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/marketplace")>()), ...api }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { impact: vi.fn(), success: vi.fn(), selection: vi.fn(), error: vi.fn(), warning: vi.fn() } }) }));
vi.mock("../src/hooks/useProfileDataRefresh", () => ({ useProfileDataRefresh: () => undefined }));
vi.mock("../src/components/ui", () => ({
  Avatar: ({ name }: { name: string }) => <span>{name}</span>,
  Badge: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
  BottomNav: () => null,
  Button: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button>,
  Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>,
  ErrorState: ({ title }: { title: string }) => <h1>{title}</h1>,
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
    render(<I18nProvider><Admin /></I18nProvider>);

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
});
