import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { defaultSchedule, formatScheduled, scheduleProblem, tashkentParts, tashkentToUtc } from "../src/lib/campaignSchedule";

const api = vi.hoisted(() => ({
  getMyCampaign: vi.fn(),
  scheduleMyCampaign: vi.fn(),
  cancelMyCampaignSchedule: vi.fn(),
  getMyBusinessProfile: vi.fn(() => Promise.resolve({ id: "business-a", teamRole: "owner" }))
}));

vi.mock("../src/api/marketplace", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../src/api/marketplace")>()),
  getMyCampaign: api.getMyCampaign,
  scheduleMyCampaign: api.scheduleMyCampaign,
  cancelMyCampaignSchedule: api.cancelMyCampaignSchedule,
  getMyBusinessProfile: api.getMyBusinessProfile
}));

vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { selection: vi.fn() } }) }));
vi.mock("../src/components/ui", () => ({
  Badge: ({ children }: { children: React.ReactNode }) => <span data-testid="badge">{children}</span>,
  BottomNav: () => <nav aria-label="bottom-nav" />,
  Button: ({ children, onClick, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button onClick={onClick} {...props}>{children}</button>,
  Card: ({ children }: { children: React.ReactNode }) => <section>{children}</section>,
  ErrorState: ({ title }: { title: string }) => <h1>{title}</h1>,
  Icon: () => <svg />,
  Input: ({ label, ...props }: React.InputHTMLAttributes<HTMLInputElement> & { label?: string }) => <label>{label}<input aria-label={label} {...props} /></label>,
  LoadingState: ({ title }: { title: string }) => <p>{title}</p>,
  Modal: ({ children, open, title }: { children: React.ReactNode; open: boolean; title: string }) => open ? <section aria-label={title}>{children}</section> : null,
  BottomSheet: ({ children, open, title }: { children: React.ReactNode; open: boolean; title: string }) => open ? <section aria-label={title}>{children}</section> : null,
  Toast: ({ message }: { message: string }) => message ? <p role="status">{message}</p> : null
}));

import { MyCampaignDetails } from "../src/pages/MyCampaignDetails";
import { resetBusinessCanOperate } from "../src/hooks/useBusinessCanOperate";
import { removeCachedMyCampaign } from "../src/data/myCampaignCache";

// 2026-11-21 20:00 UTC is 01:00 on 22 November in Tashkent.
const now = new Date("2026-11-21T20:00:00Z");

const draft = {
  id: "campaign-a",
  title: "Autumn menu",
  description: "Five stories",
  requirements: [],
  city: "tashkent",
  categories: ["food"],
  minBudget: 500_000,
  maxBudget: 1_500_000,
  deadline: null,
  status: 0 as const,
  isPromoted: false,
  createdAtUtc: "2026-11-20T00:00:00Z",
  updatedAtUtc: "2026-11-20T00:00:00Z",
  applicationsCount: 0,
  scheduledPublishAtUtc: null as string | null
};

describe("D69 campaign schedule: Tashkent time", () => {
  it("turns a Tashkent day and time into the moment and back", () => {
    expect(tashkentToUtc("2026-11-22", "11:00")?.toISOString()).toBe("2026-11-22T06:00:00.000Z");
    expect(tashkentParts("2026-11-22T06:00:00Z")).toEqual({ day: "2026-11-22", time: "11:00" });
    expect(tashkentToUtc("", "11:00")).toBeNull();
  });

  it("offers tomorrow 11:00 by the Tashkent calendar, not the browser's", () => {
    expect(defaultSchedule(now)).toEqual({ day: "2026-11-23", time: "11:00" });
  });

  it("checks the same window as the server and the campaign deadline", () => {
    expect(scheduleProblem(new Date("2026-11-21T19:00:00Z"), null, now)).toBe("past");
    expect(scheduleProblem(new Date("2027-02-01T06:00:00Z"), null, now)).toBe("tooFar");
    expect(scheduleProblem(new Date("2026-11-25T06:00:00Z"), "2026-11-24T18:59:59.000Z", now)).toBe("expired");
    expect(scheduleProblem(new Date("2026-11-24T06:00:00Z"), "2026-11-24T18:59:59.000Z", now)).toBeNull();
    expect(scheduleProblem(null, null, now)).toBe("invalid");
  });
});

describe("D69 campaign schedule on the campaign page", () => {
  // The server's copy: a reload after a change returns what the change saved.
  let stored: typeof draft & { status: 0 | 1 | 2 | 3 } = draft;
  const serve = (campaign: typeof stored) => {
    stored = campaign;
    api.getMyCampaign.mockImplementation(() => Promise.resolve(stored));
  };
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(now);
    localStorage.setItem("bloggerbazar.language", "ru");
    resetBusinessCanOperate();
    removeCachedMyCampaign(draft.id);
    api.getMyCampaign.mockReset();
    api.scheduleMyCampaign.mockReset();
    api.cancelMyCampaignSchedule.mockReset();
    api.scheduleMyCampaign.mockImplementation((_id: string, at: string) => { stored = { ...stored, scheduledPublishAtUtc: at }; return Promise.resolve(stored); });
    api.cancelMyCampaignSchedule.mockImplementation(() => { stored = { ...stored, scheduledPublishAtUtc: null }; return Promise.resolve(stored); });
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("plans a draft for tomorrow 11:00 and shows when it goes out", async () => {
    serve(draft);
    render(<I18nProvider><MyCampaignDetails id="campaign-a" /></I18nProvider>);

    fireEvent.click(await screen.findByRole("button", { name: translate("schedule.action") }));
    expect(screen.getByLabelText(translate("schedule.day"))).toHaveValue("2026-11-23");
    expect(screen.getByLabelText(translate("schedule.time"))).toHaveValue("11:00");
    fireEvent.click(screen.getByRole("button", { name: translate("schedule.submit") }));

    await waitFor(() => expect(api.scheduleMyCampaign).toHaveBeenCalledWith("campaign-a", "2026-11-23T06:00:00.000Z"));
    const date = formatScheduled("2026-11-23T06:00:00.000Z", "ru");
    expect(await screen.findByText(translate("schedule.plannedNote", { date }))).toBeInTheDocument();
    expect(screen.getByTestId("badge")).toHaveTextContent(translate("schedule.badge"));
    expect(screen.getByRole("button", { name: translate("schedule.change") })).toBeInTheDocument();
  });

  it("does not send a time that has already passed", async () => {
    serve(draft);
    render(<I18nProvider><MyCampaignDetails id="campaign-a" /></I18nProvider>);

    fireEvent.click(await screen.findByRole("button", { name: translate("schedule.action") }));
    fireEvent.change(screen.getByLabelText(translate("schedule.day")), { target: { value: "2026-11-22" } });
    fireEvent.change(screen.getByLabelText(translate("schedule.time")), { target: { value: "00:30" } });

    expect(screen.getByRole("alert")).toHaveTextContent(translate("schedule.errorPast"));
    expect(screen.getByRole("button", { name: translate("schedule.submit") })).toBeDisabled();
    expect(api.scheduleMyCampaign).not.toHaveBeenCalled();
  });

  it("drops the plan and the campaign stays a draft", async () => {
    serve({ ...draft, scheduledPublishAtUtc: "2026-11-23T06:00:00Z" });
    render(<I18nProvider><MyCampaignDetails id="campaign-a" /></I18nProvider>);

    fireEvent.click(await screen.findByRole("button", { name: translate("schedule.change") }));
    fireEvent.click(screen.getByRole("button", { name: translate("schedule.cancel") }));

    await waitFor(() => expect(api.cancelMyCampaignSchedule).toHaveBeenCalledWith("campaign-a"));
    expect(await screen.findByRole("button", { name: translate("schedule.action") })).toBeInTheDocument();
    expect(screen.getByTestId("badge")).not.toHaveTextContent(translate("schedule.badge"));
  });

  it("a published campaign has nothing to plan", async () => {
    serve({ ...draft, status: 1 });
    render(<I18nProvider><MyCampaignDetails id="campaign-a" /></I18nProvider>);

    expect(await screen.findByText(draft.title)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: translate("schedule.action") })).not.toBeInTheDocument();
  });
});
