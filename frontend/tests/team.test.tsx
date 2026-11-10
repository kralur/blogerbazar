import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  getTeam: vi.fn(),
  setTeamMode: vi.fn(),
  createTeamInvitation: vi.fn(),
  revokeTeamInvitation: vi.fn(),
  previewTeamInvitation: vi.fn(),
  requestTeamAccess: vi.fn(),
  acceptTeamRequest: vi.fn(),
  declineTeamRequest: vi.fn(),
  changeTeamRole: vi.fn(),
  removeTeamMember: vi.fn(),
  leaveTeam: vi.fn(),
  sendWorkOnComputerLink: vi.fn()
}));
const marketplace = vi.hoisted(() => ({ getCurrentPlatformUser: vi.fn() }));
const telegram = vi.hoisted(() => ({ openLink: vi.fn(), isTelegram: true }));
vi.mock("../src/api/team", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/team")>()), ...api }));
vi.mock("../src/api/marketplace", () => marketplace);
vi.mock("../src/telegram/TelegramProvider", () => ({
  useTelegram: () => ({ haptic: { selection: vi.fn(), success: vi.fn(), error: vi.fn(), warning: vi.fn() }, isTelegram: telegram.isTelegram, openLink: telegram.openLink, setBackButtonHandler: vi.fn(), registerBackButtonHandler: () => () => undefined, setClosingConfirmation: vi.fn() })
}));
vi.mock("../src/components/AtlasBackground", () => ({ AtlasBackground: () => null }));
vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => null }));
vi.mock("../src/pages/PhoneVerification", () => ({ PhoneVerification: () => <p>phone-verification</p> }));
vi.mock("../src/components/ui", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/components/ui")>()), BottomNav: () => null }));

import { ApiError } from "../src/api/client";
import { mayHandle, rolesYouMayGive, teamJoinCodeFrom, type Team as TeamData } from "../src/api/team";
import { isDeepLinkRoute } from "../src/lib/deepLink";
import { BusinessModeChoice } from "../src/pages/BusinessModeChoice";
import { Team } from "../src/pages/Team";
import { TeamJoin } from "../src/pages/TeamJoin";
import { TeamWelcome } from "../src/pages/TeamWelcome";

const ru = (key: string, values?: Record<string, string | number>) => translate(key, values, "ru");
const code = "AbCdEfGhIjKlMnOpQrStUv";
const team = (overrides: Partial<TeamData> = {}): TeamData => ({
  companyId: "c1", companyName: "Kofe Hona", teamMode: true, myRole: "owner", canManage: true, seats: 3, seatsUsed: 2,
  members: [
    { id: "m1", firstName: "Umid", username: "umid", role: "owner", joinedAtUtc: "2026-11-01T09:00:00Z", isMe: true },
    { id: "m2", firstName: "Dilnoza", username: null, role: "manager", joinedAtUtc: "2026-11-05T09:00:00Z", isMe: false }
  ],
  requests: [{ id: "r1", firstName: "Aziz", username: "aziz", phone: "+998901234567", role: "viewer", requestedAtUtc: "2026-11-10T08:00:00Z" }],
  invitations: [],
  ...overrides
});
const renderIn = (node: React.ReactNode) => render(<I18nProvider>{node}</I18nProvider>);

describe("team rules in the app", () => {
  it("reads only well-formed invitation codes from the launch parameter", () => {
    expect(teamJoinCodeFrom(`join_${code}`)).toBe(code);
    expect(teamJoinCodeFrom(`web_${code}`)).toBeNull();
    expect(teamJoinCodeFrom("join_short")).toBeNull();
    expect(teamJoinCodeFrom("join_<script>alert(1)</script>")).toBeNull();
    expect(teamJoinCodeFrom(undefined)).toBeNull();
  });

  it("lets only the owner give or handle the admin role, and nobody the owner's", () => {
    expect(rolesYouMayGive("owner")).toEqual(["manager", "viewer", "admin"]);
    expect(rolesYouMayGive("admin")).toEqual(["manager", "viewer"]);
    expect(mayHandle("admin", "admin")).toBe(false);
    expect(mayHandle("owner", "admin")).toBe(true);
    expect(mayHandle("owner", "owner")).toBe(false);
    expect(isDeepLinkRoute("/team")).toBe(true);
  });
});

describe("team screen", () => {
  beforeEach(() => { vi.clearAllMocks(); telegram.isTelegram = true; });

  it("shows who asks with their number and lets the owner accept", async () => {
    api.getTeam.mockResolvedValue(team());
    api.acceptTeamRequest.mockResolvedValue(team({ seatsUsed: 3, requests: [], members: [...team().members, { id: "m3", firstName: "Aziz", role: "viewer", joinedAtUtc: "2026-11-10T08:05:00Z", isMe: false }] }));
    renderIn(<Team />);

    expect(await screen.findByText(ru("team.seats", { used: 2, total: 3 }))).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "+998901234567" })).toHaveAttribute("href", "tel:+998901234567");
    fireEvent.click(screen.getByRole("button", { name: ru("team.requests.accept") }));

    await waitFor(() => expect(api.acceptTeamRequest).toHaveBeenCalledWith("r1"));
    expect(await screen.findByText(ru("team.seats", { used: 3, total: 3 }))).toBeInTheDocument();
    expect(screen.getByRole("button", { name: ru("team.invite.button") })).toBeDisabled();
    // Back to working alone needs the team removed first.
    expect(screen.getByRole("button", { name: ru("team.mode.switchToAlone") })).toBeDisabled();
  });

  it("makes a one-time link for a role and offers to send it in Telegram", async () => {
    api.getTeam.mockResolvedValue(team({ requests: [] }));
    api.createTeamInvitation.mockResolvedValue({ id: "i1", role: "viewer", expiresAtUtc: "2026-11-12T08:00:00Z", link: `https://t.me/FindInfluencerBot?startapp=join_${code}` });
    renderIn(<Team />);

    fireEvent.click(await screen.findByRole("button", { name: ru("team.invite.button") }));
    const sheet = await screen.findByRole("dialog");
    expect(within(sheet).getByRole("radio", { name: new RegExp(ru("team.role.admin")) })).toBeInTheDocument();
    fireEvent.click(within(sheet).getByRole("radio", { name: new RegExp(ru("team.role.viewer")) }));
    fireEvent.click(within(sheet).getByRole("button", { name: ru("team.invite.create") }));

    await waitFor(() => expect(api.createTeamInvitation).toHaveBeenCalledWith("viewer"));
    fireEvent.click(await within(sheet).findByRole("button", { name: ru("team.invite.share") }));
    expect(telegram.openLink).toHaveBeenCalledWith(expect.stringContaining(`https://t.me/share/url?url=${encodeURIComponent(`https://t.me/FindInfluencerBot?startapp=join_${code}`)}`));
  });

  it("gives a manager the team list without requests or management", async () => {
    api.getTeam.mockResolvedValue(team({ myRole: "manager", canManage: false, requests: [], members: team().members.map((member) => ({ ...member, isMe: member.id === "m2" })) }));
    renderIn(<Team />);

    expect(await screen.findByText("Dilnoza")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: ru("team.invite.button") })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: ru("team.members.remove") })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: ru("team.leave") })).toBeInTheDocument();
  });

  it("asks before removing a person and explains the access goes at once", async () => {
    api.getTeam.mockResolvedValue(team({ requests: [] }));
    api.removeTeamMember.mockResolvedValue(team({ requests: [], seatsUsed: 1, members: [team().members[0]] }));
    renderIn(<Team />);

    fireEvent.click(await screen.findByRole("button", { name: ru("team.members.remove") }));
    expect(await screen.findByText(ru("team.confirm.removeText"))).toBeInTheDocument();
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: ru("team.members.remove") }));

    await waitFor(() => expect(api.removeTeamMember).toHaveBeenCalledWith("m2"));
    await waitFor(() => expect(screen.queryByText("Dilnoza")).not.toBeInTheDocument());
  });

  it("lets the owner of a company working alone switch to a team", async () => {
    api.getTeam.mockResolvedValue(team({ teamMode: false, seatsUsed: 1, requests: [], members: [team().members[0]] }));
    api.setTeamMode.mockResolvedValue(team({ seatsUsed: 1, requests: [], members: [team().members[0]] }));
    renderIn(<Team />);

    fireEvent.click(await screen.findByRole("button", { name: ru("team.mode.switchToTeam") }));

    await waitFor(() => expect(api.setTeamMode).toHaveBeenCalledWith(true));
    expect(await screen.findByRole("button", { name: ru("team.invite.button") })).toBeEnabled();
  });

  it("opens the site and puts its address into the bot chat for work on a computer", async () => {
    api.getTeam.mockResolvedValue(team({ requests: [] }));
    api.sendWorkOnComputerLink.mockResolvedValue({ url: window.location.origin });
    renderIn(<Team />);

    fireEvent.click(await screen.findByRole("button", { name: ru("team.computer.button") }));

    expect(telegram.openLink).toHaveBeenCalledWith(window.location.origin);
    expect(api.sendWorkOnComputerLink).toHaveBeenCalledTimes(1);
  });
});

describe("joining a team from a link", () => {
  beforeEach(() => { vi.clearAllMocks(); marketplace.getCurrentPlatformUser.mockResolvedValue({ verifiedPhone: "+998901234567" }); });

  it("shows the company and the role, warns about the number, and asks for access", async () => {
    api.previewTeamInvitation.mockResolvedValue({ companyName: "Kofe Hona", role: "manager", state: "open" });
    api.requestTeamAccess.mockResolvedValue({ companyName: "Kofe Hona", role: "manager", state: "requestedByYou" });
    renderIn(<TeamJoin code={code} onClose={vi.fn()} />);

    expect(await screen.findByText("Kofe Hona")).toBeInTheDocument();
    expect(screen.getByText(ru("team.join.privacy"))).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: ru("team.join.ask") }));

    expect(await screen.findByText(ru("team.join.waiting", { company: "Kofe Hona" }))).toBeInTheDocument();
    expect(api.requestTeamAccess).toHaveBeenCalledWith(code);
  });

  it("asks for the phone number from Telegram first", async () => {
    marketplace.getCurrentPlatformUser.mockResolvedValue({ verifiedPhone: null });
    renderIn(<TeamJoin code={code} onClose={vi.fn()} />);

    expect(await screen.findByText("phone-verification")).toBeInTheDocument();
    expect(api.previewTeamInvitation).not.toHaveBeenCalled();
  });

  it("explains a used link and a person already working elsewhere", async () => {
    api.previewTeamInvitation.mockResolvedValue({ companyName: "Kofe Hona", role: "viewer", state: "open" });
    api.requestTeamAccess.mockRejectedValue(new ApiError(409, "business_member_elsewhere"));
    renderIn(<TeamJoin code={code} onClose={vi.fn()} />);

    fireEvent.click(await screen.findByRole("button", { name: ru("team.join.ask") }));

    expect(await screen.findByText(ru("team.error.business_member_elsewhere"))).toBeInTheDocument();
  });

  it("opens the app as the company once the person is in", async () => {
    api.previewTeamInvitation.mockResolvedValue({ companyName: "Kofe Hona", role: "viewer", state: "joined" });
    const onClose = vi.fn();
    renderIn(<TeamJoin code={code} onClose={onClose} />);

    fireEvent.click(await screen.findByRole("button", { name: ru("team.join.open") }));

    expect(onClose).toHaveBeenCalledWith(true);
  });
});

describe("a new company's way of working", () => {
  beforeEach(() => { vi.clearAllMocks(); });

  it("chooses between alone and with a team before the company form", () => {
    const onChosen = vi.fn();
    renderIn(<BusinessModeChoice onBack={vi.fn()} onChosen={onChosen} />);

    expect(screen.getByRole("button", { name: ru("wizard.continue") })).toBeDisabled();
    fireEvent.click(screen.getByRole("radio", { name: new RegExp(ru("team.mode.team")) }));
    fireEvent.click(screen.getByRole("button", { name: ru("wizard.continue") }));

    expect(onChosen).toHaveBeenCalledWith(true);
  });

  it("offers the computer, an invitation or staying here once the company is ready", () => {
    api.sendWorkOnComputerLink.mockResolvedValue({ url: window.location.origin });
    const onDone = vi.fn();
    renderIn(<TeamWelcome onDone={onDone} />);

    expect(screen.getByRole("heading", { name: ru("team.welcome.title") })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: ru("team.invite.button") }));
    expect(onDone).toHaveBeenLastCalledWith("/team");

    fireEvent.click(screen.getByRole("button", { name: ru("team.computer.button") }));
    expect(telegram.openLink).toHaveBeenCalledWith(window.location.origin);
    expect(api.sendWorkOnComputerLink).toHaveBeenCalledTimes(1);
    expect(onDone).toHaveBeenLastCalledWith("/");
  });
});
