import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getAdminUserDetails: vi.fn(), setAdminUserBlocked: vi.fn() }));
vi.mock("../src/api/marketplace", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/marketplace")>()), ...api }));
const openLink = vi.hoisted(() => vi.fn());
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ openLink, haptic: { impact: vi.fn(), success: vi.fn(), selection: vi.fn(), error: vi.fn(), warning: vi.fn() } }) }));
vi.mock("../src/components/ui", () => ({
  Badge: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
  BottomNav: () => null,
  Button: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button>,
  Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>,
  ErrorState: ({ title }: { title: string }) => <h1>{title}</h1>,
  LoadingState: () => <p>loading</p>,
  Toast: ({ message }: { message: string }) => message ? <p role="status">{message}</p> : null
}));

import { AdminUserCard } from "../src/pages/AdminUserCard";

const ru = (key: string, params?: Record<string, string | number>) => translate(key, params, "ru");
const user = { telegramUserId: 9, firstName: "Ali", username: "ali", role: 0, isBlocked: false, isDeleted: false, createdAtUtc: "2026-10-01T00:00:00Z", verifiedPhone: "+998901234567" };

describe("Admin user card", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.location.hash = "#/admin/user?id=9";
  });

  it("shows profiles, deals and complaints of one person and blocks them", async () => {
    api.getAdminUserDetails.mockResolvedValue({
      user, profiles: [{ role: "blogger", id: "b1", name: "Ali Blog", isHidden: true, isDeleted: false }],
      activeDeals: 1, completedDeals: 2, cancelledDeals: 0,
      recentDeals: [{ id: "d1", role: "blogger", partnerName: "Lumi", title: "Launch", status: 1, agreedPrice: 500000, createdAtUtc: "2026-10-05T00:00:00Z" }],
      complaintsAgainst: 3, openComplaintsAgainst: 1, complaintsFiled: 0
    });
    api.setAdminUserBlocked.mockResolvedValue({ ...user, isBlocked: true });
    render(<I18nProvider><AdminUserCard /></I18nProvider>);

    expect(await screen.findByText("Ali")).toBeInTheDocument();
    expect(api.getAdminUserDetails).toHaveBeenCalledWith(9);
    expect(screen.getByRole("link", { name: /Ali Blog/ })).toHaveAttribute("href", "#/blogger/b1");
    expect(screen.getByText(ru("admin.userDealCounts", { active: 1, completed: 2, cancelled: 0 }))).toBeInTheDocument();
    expect(screen.getByText("Lumi")).toBeInTheDocument();
    expect(screen.getByText(ru("admin.userComplaintCounts", { against: 3, open: 1, filed: 0 }))).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: ru("admin.writeInTelegram") }));
    expect(openLink).toHaveBeenCalledWith("https://t.me/ali");
    fireEvent.click(screen.getByRole("button", { name: ru("common.block") }));
    await waitFor(() => expect(api.setAdminUserBlocked).toHaveBeenCalledWith(9, true));
    expect(await screen.findByRole("button", { name: ru("common.unblock") })).toBeInTheDocument();
  });

  it("says access denied to a non-admin", async () => {
    api.getAdminUserDetails.mockRejectedValue(new ApiError(403, "access_denied"));
    render(<I18nProvider><AdminUserCard /></I18nProvider>);

    expect(await screen.findByText(ru("ui.accessDenied"))).toBeInTheDocument();
  });
});
