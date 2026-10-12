import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import type { AdminFunnel as Funnel } from "../src/api/marketplace";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ getAdminFunnel: vi.fn() }));
vi.mock("../src/api/marketplace", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/marketplace")>()), ...api }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ isEmbedded: false, isEnvironmentReady: true, haptic: { impact: vi.fn(), success: vi.fn(), selection: vi.fn(), error: vi.fn(), warning: vi.fn() } }) }));
vi.mock("../src/components/ui", () => ({
  Badge: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
  BottomNav: () => null,
  Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>,
  ErrorState: ({ title }: { title: string }) => <h1>{title}</h1>,
  Icon: () => null,
  LoadingState: () => <p>loading</p>
}));

import { AdminFunnel } from "../src/pages/AdminFunnel";

const ru = (key: string, params?: Record<string, string | number>) => translate(key, params, "ru");
const funnel = (period: Funnel["period"], opened: number): Funnel => ({
  period,
  fromUtc: period === "all" ? null : "2026-09-12T00:00:00Z",
  roles: [
    { role: "blogger", steps: [{ key: "opened", count: opened }, { key: "roleSelected", count: 60 }, { key: "profile", count: 45 }, { key: "reached", count: 9 }, { key: "deal", count: 6 }, { key: "completed", count: 4 }, { key: "reviewed", count: 2 }] },
    { role: "brandFace", steps: [{ key: "opened", count: opened }, { key: "roleSelected", count: 5 }, { key: "profile", count: 5 }, { key: "reached", count: 0 }, { key: "deal", count: 0 }, { key: "completed", count: 0 }, { key: "reviewed", count: 0 }] },
    { role: "business", steps: [{ key: "opened", count: opened }, { key: "roleSelected", count: 20 }, { key: "profile", count: 12 }, { key: "posted", count: 8 }, { key: "answered", count: 6 }, { key: "deal", count: 6 }, { key: "completed", count: 4 }, { key: "reviewed", count: 3 }] }
  ]
});
const stepRow = (key: string) => screen.getByText(ru(`admin.funnelStep.${key}`)).closest("li") as HTMLElement;

describe("Admin funnel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.location.hash = "#/admin/funnel";
  });

  it("shows each step of the selected role with its share and marks the biggest loss", async () => {
    api.getAdminFunnel.mockResolvedValue(funnel("30d", 100));
    render(<I18nProvider><AdminFunnel /></I18nProvider>);

    expect(await screen.findByText(ru("admin.funnelStep.opened"))).toBeInTheDocument();
    expect(api.getAdminFunnel).toHaveBeenCalledWith("30d");
    expect(screen.getByRole("button", { name: ru("admin.funnelPeriod.30d") })).toHaveAttribute("aria-pressed", "true");
    expect(within(stepRow("profile")).getByText("45")).toBeInTheDocument();
    expect(within(stepRow("profile")).getByText(ru("admin.funnelRates", { share: 45, step: 75 }))).toBeInTheDocument();
    // 45 → 9 keeps 20%: the smallest share of the previous step, so people leave here first.
    expect(within(stepRow("reached")).getByText(ru("admin.funnelBiggestDrop"))).toBeInTheDocument();
    expect(screen.getAllByText(ru("admin.funnelBiggestDrop"))).toHaveLength(1);

    fireEvent.click(screen.getByRole("button", { name: ru("admin.funnelRole.business") }));
    expect(within(stepRow("posted")).getByText("8")).toBeInTheDocument();
    expect(screen.queryByText(ru("admin.funnelStep.reached"))).not.toBeInTheDocument();
    // Switching the role uses the loaded numbers; only the period asks the server again.
    expect(api.getAdminFunnel).toHaveBeenCalledTimes(1);

    // Nobody reached out, so the deal step has no previous-step rate (0 of 0), only its share.
    fireEvent.click(screen.getByRole("button", { name: ru("admin.funnelRole.brandFace") }));
    expect(within(stepRow("deal")).getByText(ru("admin.funnelShare", { share: 0 }))).toBeInTheDocument();
    expect(within(stepRow("reached")).getByText(ru("admin.funnelRates", { share: 0, step: 0 }))).toBeInTheDocument();
    expect(within(stepRow("reached")).getByText(ru("admin.funnelBiggestDrop"))).toBeInTheDocument();
  });

  it("asks the server again for another period and keeps the newest answer", async () => {
    let answerMonth: (value: Funnel) => void = () => {};
    api.getAdminFunnel
      .mockReturnValueOnce(new Promise<Funnel>((resolve) => { answerMonth = resolve; }))
      .mockResolvedValueOnce(funnel("7d", 10));
    render(<I18nProvider><AdminFunnel /></I18nProvider>);

    fireEvent.click(screen.getByRole("button", { name: ru("admin.funnelPeriod.7d") }));
    expect(await screen.findByText("10")).toBeInTheDocument();
    expect(api.getAdminFunnel).toHaveBeenLastCalledWith("7d");
    answerMonth(funnel("30d", 100));
    await waitFor(() => expect(screen.queryByText("100")).not.toBeInTheDocument());
    expect(screen.getByText("10")).toBeInTheDocument();
  });

  it("says when nobody opened the app in the period", async () => {
    api.getAdminFunnel.mockResolvedValue(funnel("7d", 0));
    render(<I18nProvider><AdminFunnel /></I18nProvider>);

    expect(await screen.findByText(ru("admin.funnelEmpty"))).toBeInTheDocument();
  });

  it("says access denied to a non-admin", async () => {
    api.getAdminFunnel.mockRejectedValue(new ApiError(403, "access_denied"));
    render(<I18nProvider><AdminFunnel /></I18nProvider>);

    expect(await screen.findByText(ru("ui.accessDenied"))).toBeInTheDocument();
  });
});
