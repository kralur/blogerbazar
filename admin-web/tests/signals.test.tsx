import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { App } from "../src/App";
import { dictionaries, I18nProvider } from "../src/i18n";
import { fakeApi, session, watchNavigation } from "./helpers";

const ru = dictionaries.ru;
const rules = { quickDealMinutes: 30, quickDealDays: 90, complaintReporters: 3, complaintDays: 30, burstAccounts: 20, burstLowProfilePercent: 10, burstDays: 7 };
const signals = {
  duplicateHandles: [{ handle: "instagram.com/madina", people: [{ telegramUserId: 11, name: "Madina" }, { telegramUserId: 12, name: "Madina Fake" }] }],
  sharedPhones: [{ phone: "+998901112233", people: [{ telegramUserId: 21, name: "Ali" }, { telegramUserId: 22, name: "Vali" }] }],
  quickDeals: [{ dealId: "d1", title: "Launch", business: { telegramUserId: 31, name: "Cafe" }, creator: { telegramUserId: 32, name: "Sevara" }, minutes: 4, reviews: 2, completedAtUtc: "2026-11-01T10:00:00Z" }],
  complaintTargets: [{ targetType: "blogger", targetId: "0026d75b-e3f9-44e1-a6c8-fbd43c7e8172", targetName: "Zarina", ownerTelegramUserId: 41, reporters: 3, lastAtUtc: "2026-11-02T10:00:00Z" }],
  signupBursts: [
    { source: "ig_nov", hourUtc: "2026-11-03T10:00:00Z", accounts: 40, withProfile: 1, lowProfiles: true },
    { source: "tg_nov", hourUtc: "2026-11-03T11:00:00Z", accounts: 25, withProfile: 12, lowProfiles: false }
  ],
  rules
};

function open(body: unknown) {
  window.history.replaceState(null, "", "/signals");
  fakeApi({ "GET /api/admin-web/session": { status: 200, body: session }, "GET /api/admin/signals": { status: 200, body } });
  watchNavigation();
  render(<I18nProvider><App /></I18nProvider>);
}

const section = (titleKey: string) => screen.getByRole("heading", { level: 2, name: ru[titleKey] }).closest("section, div.space-y-4") as HTMLElement;

describe("Suspicious page", () => {
  it("lists every signal with links to the people behind it", async () => {
    open(signals);

    expect(await screen.findByRole("link", { name: "Madina Fake" })).toHaveAttribute("href", "/users/12");
    expect(screen.getByRole("link", { name: "Подозрительное" })).toHaveAttribute("aria-current", "page");
    expect(within(section("signals.handles.title")).getByText("instagram.com/madina")).toBeInTheDocument();
    expect(within(section("signals.phones.title")).getByRole("link", { name: "Vali" })).toHaveAttribute("href", "/users/22");
    expect(within(section("signals.deals.title")).getByRole("link", { name: "Sevara" })).toHaveAttribute("href", "/users/32");
    expect(screen.getByText(/меньше чем через 30 мин/)).toBeInTheDocument();
    expect(within(section("signals.complaints.title")).getByRole("link", { name: "Zarina" })).toHaveAttribute("href", "/users/41");
    const bursts = section("signals.bursts.title");
    // Only the hour where almost nobody made a profile is marked.
    expect(within(bursts).getAllByText(ru["signals.lowProfiles"])).toHaveLength(1);
    expect(within(bursts).getByText("ig_nov").closest("tr")).toHaveTextContent(ru["signals.lowProfiles"]);
  });

  it("says so when nothing looks suspicious", async () => {
    open({ duplicateHandles: [], sharedPhones: [], quickDeals: [], complaintTargets: [], signupBursts: [], rules });

    expect(await screen.findAllByText(ru["signals.empty"])).toHaveLength(5);
  });
});
