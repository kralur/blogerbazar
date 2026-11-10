import { useCallback, useEffect, useState } from "react";
import { ApiError } from "../api/client";
import {
  acceptTeamRequest,
  changeTeamRole,
  createTeamInvitation,
  declineTeamRequest,
  getTeam,
  leaveTeam,
  mayHandle,
  removeTeamMember,
  revokeTeamInvitation,
  rolesYouMayGive,
  sendWorkOnComputerLink,
  setTeamMode,
  siteAddress,
  type InvitableRole,
  type Team as TeamData,
  type TeamInvitationLink,
  type TeamMember
} from "../api/team";
import { ListHero } from "../components/ListHero";
import { DetailLayout } from "../components/DetailLayout";
import { PageHeader } from "../components/PageHeader";
import { QrCode } from "../components/QrCode";
import { Badge, BottomNav, BottomSheet, Button, EmptyState, ErrorState, Modal, Skeleton, Toast } from "../components/ui";
import { notifyProfileDataChanged } from "../hooks/useProfileDataRefresh";
import { useScreenRefresh } from "../hooks/useScreenRefresh";
import { useI18n } from "../i18n";
import { isBrowserSession } from "../lib/browserSession";
import { formatShortDate } from "../lib/currency";
import { useTelegram } from "../telegram/TelegramProvider";

type Confirm = { kind: "remove"; member: TeamMember } | { kind: "leave" } | { kind: "alone" };

// The D26 error codes of the team, in the person's language.
export function teamErrorMessage(error: unknown, t: (key: string) => string) {
  const code = error instanceof ApiError ? error.code : undefined;
  const known = ["team_mode_off", "team_full", "team_not_empty", "team_invitation_used", "team_already_member", "team_too_many_invitations", "business_member_elsewhere", "phone_not_verified"];
  if (code && known.includes(code)) return t(`team.error.${code}`);
  if (error instanceof ApiError && error.status === 403) return t("team.error.forbidden");
  if (error instanceof ApiError && error.status === 404) return t("team.error.gone");
  return t("team.error.failed");
}

// D64 stage C1: the company's team. Everybody in it sees who works there; the owner and admins invite, let people
// in, change roles and remove; only the owner handles admins and switches between "alone" and "with a team".
export function Team() {
  const { t, language } = useI18n();
  const { haptic, isTelegram, openLink } = useTelegram();
  const inBrowser = isBrowserSession();
  const [team, setTeam] = useState<TeamData | null>(null);
  const [state, setState] = useState<"loading" | "ready" | "missing" | "failed">("loading");
  const [busy, setBusy] = useState(false);
  const [toast, setToast] = useState<{ id: number; message: string; tone: "success" | "error" }>();
  const [inviteOpen, setInviteOpen] = useState(false);
  const [inviteRole, setInviteRole] = useState<InvitableRole>("manager");
  const [link, setLink] = useState<TeamInvitationLink | null>(null);
  const [roleFor, setRoleFor] = useState<TeamMember | null>(null);
  const [confirm, setConfirm] = useState<Confirm | null>(null);

  const say = (message: string, tone: "success" | "error" = "success") => setToast({ id: Date.now(), message, tone });

  const load = useCallback(async () => {
    try {
      setTeam(await getTeam());
      setState("ready");
    } catch (error) {
      setState(error instanceof ApiError && error.status === 404 ? "missing" : "failed");
    }
  }, []);
  useEffect(() => { void load(); }, [load]);
  useScreenRefresh(load);

  const act = async (action: () => Promise<TeamData | void>, done?: string) => {
    if (busy) return;
    setBusy(true);
    try {
      const next = await action();
      if (next) setTeam(next); else await load();
      haptic.success();
      if (done) say(done);
      return true;
    } catch (error) {
      haptic.error();
      say(teamErrorMessage(error, t), "error");
      return false;
    } finally {
      setBusy(false);
    }
  };

  const makeLink = async () => {
    if (busy) return;
    setBusy(true);
    try {
      setLink(await createTeamInvitation(inviteRole));
      haptic.success();
      await load();
    } catch (error) {
      haptic.error();
      say(teamErrorMessage(error, t), "error");
    } finally {
      setBusy(false);
    }
  };

  const shareLink = (url: string) => {
    const text = t("team.invite.shareText", { company: team?.companyName ?? "" });
    openLink(`https://t.me/share/url?url=${encodeURIComponent(url)}&text=${encodeURIComponent(text)}`);
  };

  const copyLink = async (url: string) => {
    try {
      await navigator.clipboard.writeText(url);
      say(t("team.invite.copied"));
    } catch {
      say(t("team.invite.copyFailed"), "error");
    }
  };

  const workOnComputer = () => {
    haptic.selection();
    openLink(siteAddress());
    void sendWorkOnComputerLink().catch(() => undefined);
    say(t("team.computer.sent"));
  };

  const closeInvite = () => {
    if (busy) return;
    setInviteOpen(false);
    setLink(null);
  };

  const runConfirm = async () => {
    if (!confirm) return;
    const ok = confirm.kind === "remove"
      ? await act(() => removeTeamMember(confirm.member.id), t("team.removed"))
      : confirm.kind === "alone"
        ? await act(() => setTeamMode(false), t("team.mode.aloneDone"))
        : await act(async () => { await leaveTeam(); }, t("team.left"));
    if (!ok) return;
    setConfirm(null);
    notifyProfileDataChanged();
    if (confirm.kind === "leave") window.location.hash = "/profile";
  };

  const header = <ListHero className="detail-hero">
    <PageHeader back={{ href: "#/profile", label: t("nav.profile") }} eyebrow={team?.companyName ?? t("profile.businessProfile")} title={t("team.title")} />
  </ListHero>;

  if (state === "loading") return <div className="screen screen--with-nav space-y-5 px-4 pt-5">{header}<Skeleton className="h-28" /><Skeleton className="h-20" /><BottomNav /></div>;
  if (state === "failed") return <div className="screen screen--with-nav space-y-5 px-4 pt-5">{header}<ErrorState onRetry={() => void load()} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /><BottomNav /></div>;
  if (state === "missing" || !team) return <div className="screen screen--with-nav space-y-5 px-4 pt-5">{header}<EmptyState icon="users" subtitle={t("team.missingSubtitle")} title={t("team.missingTitle")} /><BottomNav /></div>;

  const isOwner = team.myRole === "owner";
  const full = team.seatsUsed >= team.seats;
  const roleName = (role: string) => t(`team.role.${role}`);

  return <div className="screen screen--with-nav space-y-5 px-4 pt-5 team-screen">
    {header}

    <DetailLayout top={<>
    {!team.teamMode && <section className="settings-list">
      <div className="settings-row settings-row--stacked">
        <span className="settings-row__text"><strong>{t("team.mode.aloneTitle")}</strong><span>{t("team.mode.aloneHint")}</span></span>
        {isOwner && <Button aria-busy={busy} className="w-full" disabled={busy} onClick={() => void act(() => setTeamMode(true), t("team.mode.teamDone")).then((ok) => ok && notifyProfileDataChanged())} type="button">{t("team.mode.switchToTeam")}</Button>}
      </div>
    </section>}

    {team.teamMode && <section className="settings-list">
        <div className="settings-row settings-row--stacked">
          <span className="settings-row__text">
            <strong>{t("team.seats", { used: team.seatsUsed, total: team.seats })}</strong>
            <span>{t("team.seatsHint")}</span>
          </span>
          {team.canManage && <Button className="w-full" disabled={busy || full} onClick={() => { haptic.selection(); setInviteRole(rolesYouMayGive(team.myRole)[0]); setLink(null); setInviteOpen(true); }} type="button">{t("team.invite.button")}</Button>}
          {team.canManage && full && <p className="text-sm text-brand-muted">{t("team.fullHint")}</p>}
        </div>
      </section>}
    </>} side={<>
    {team.teamMode && team.canManage && team.invitations.length > 0 && <section aria-labelledby="team-links" className="settings-list">
      <div className="settings-row settings-row--stacked">
        <strong id="team-links">{t("team.links.title")}</strong>
        <ul className="team-list">
          {team.invitations.map((invitation) => <li className="team-list__item" key={invitation.id}>
            <span className="settings-row__text">
              <strong>{roleName(invitation.role)}</strong>
              <span>{t("team.links.until", { time: formatShortDate(invitation.expiresAtUtc, language, { time: true }) })}</span>
            </span>
            {mayHandle(team.myRole, invitation.role) && <Button disabled={busy} onClick={() => void act(async () => { await revokeTeamInvitation(invitation.id); }, t("team.links.revoked"))} type="button" variant="secondary">{t("team.links.revoke")}</Button>}
          </li>)}
        </ul>
      </div>
    </section>}
    {isTelegram && !inBrowser && <section className="settings-list">
      <div className="settings-row settings-row--stacked">
        <span className="settings-row__text"><strong>{t("team.computer.title")}</strong><span>{t("team.computer.hint", { address: siteAddress().replace(/^https?:\/\//, "") })}</span></span>
        <Button className="w-full" onClick={workOnComputer} type="button" variant="secondary">{t("team.computer.button")}</Button>
      </div>
    </section>}

    {isOwner && team.teamMode && <div className="grid gap-2">
      <Button className="w-full" disabled={busy || team.seatsUsed > 1} onClick={() => { haptic.warning(); setConfirm({ kind: "alone" }); }} type="button" variant="secondary">{t("team.mode.switchToAlone")}</Button>
      {team.seatsUsed > 1 && <p className="text-center text-xs text-brand-muted">{t("team.mode.aloneBlocked")}</p>}
    </div>}
    {!isOwner && <Button className="w-full" disabled={busy} onClick={() => { haptic.warning(); setConfirm({ kind: "leave" }); }} type="button" variant="danger">{t("team.leave")}</Button>}
    </>}>
      {team.teamMode && team.canManage && team.requests.length > 0 && <section aria-labelledby="team-requests" className="settings-list">
        <div className="settings-row settings-row--stacked">
          <strong id="team-requests">{t("team.requests.title")}</strong>
          <ul className="team-list">
            {team.requests.map((request) => <li className="team-list__item team-list__item--request" key={request.id}>
              <span className="settings-row__text">
                <strong>{request.firstName}{request.username && <span className="team-list__handle"> @{request.username}</span>}</strong>
                {request.phone && <a className="team-list__phone" href={`tel:${request.phone}`}>{request.phone}</a>}
                <span>{t("team.requests.asks", { role: roleName(request.role), time: formatShortDate(request.requestedAtUtc, language, { time: true }) })}</span>
              </span>
              <div className="team-list__actions">
                <Button disabled={busy || full} onClick={() => void act(() => acceptTeamRequest(request.id), t("team.requests.accepted", { name: request.firstName }))} type="button">{t("team.requests.accept")}</Button>
                <Button disabled={busy} onClick={() => void act(() => declineTeamRequest(request.id), t("team.requests.declined"))} type="button" variant="secondary">{t("team.requests.decline")}</Button>
              </div>
            </li>)}
          </ul>
          {full && <p className="text-sm text-brand-muted">{t("team.fullHint")}</p>}
        </div>
      </section>}

    <section aria-labelledby="team-members" className="settings-list">
      <div className="settings-row settings-row--stacked">
        <strong id="team-members">{t("team.members.title")}</strong>
        <ul className="team-list">
          {team.members.map((member) => {
            const manageable = team.canManage && !member.isMe && mayHandle(team.myRole, member.role);
            return <li className="team-list__item" key={member.id}>
              <span className="settings-row__text">
                <strong>{member.firstName || t("profile.telegramUser")}{member.isMe && <span className="browser-sessions__current">{t("team.members.you")}</span>}</strong>
                {member.username && <span>@{member.username}</span>}
                <span>{t("team.members.since", { date: formatShortDate(member.joinedAtUtc, language) })}</span>
              </span>
              <div className="team-list__actions">
                <Badge tone={member.role === "owner" ? "gold" : member.role === "admin" ? "purple" : member.role === "manager" ? "blue" : "gray"}>{roleName(member.role)}</Badge>
                {manageable && <Button disabled={busy} onClick={() => { haptic.selection(); setRoleFor(member); }} type="button" variant="secondary">{t("team.members.changeRole")}</Button>}
                {manageable && <Button disabled={busy} onClick={() => { haptic.warning(); setConfirm({ kind: "remove", member }); }} type="button" variant="danger">{t("team.members.remove")}</Button>}
              </div>
            </li>;
          })}
        </ul>
      </div>
    </section>


    </DetailLayout>

    <BottomSheet onClose={closeInvite} open={inviteOpen} title={t("team.invite.title")}>
      {!link ? <div className="grid gap-4">
        <div aria-label={t("team.invite.role")} className="team-roles" role="radiogroup">
          {rolesYouMayGive(team.myRole).map((role) => <button aria-checked={inviteRole === role} className={`ftue-role-card${inviteRole === role ? " ftue-role-card--selected" : ""}`} key={role} onClick={() => { haptic.selection(); setInviteRole(role); }} role="radio" type="button">
            <span className="ftue-role-card__copy"><span className="ftue-role-card__title">{roleName(role)}</span><span className="ftue-role-card__description">{t(`team.roleHint.${role}`)}</span></span>
          </button>)}
        </div>
        <p className="text-sm leading-6 text-brand-muted">{t("team.invite.howItWorks")}</p>
        <Button aria-busy={busy} className="w-full" disabled={busy} onClick={() => void makeLink()} type="button">{t("team.invite.create")}</Button>
      </div> : <div className="grid gap-4">
        <p className="text-sm leading-6 text-brand-muted">{t("team.invite.ready", { role: roleName(link.role), time: formatShortDate(link.expiresAtUtc, language, { time: true }) })}</p>
        {link.link ? <>
          {(inBrowser || !isTelegram) && <QrCode className="team-invite__qr" label={t("team.invite.qr")} value={link.link} />}
          <code className="team-invite__link">{link.link}</code>
          {isTelegram && <Button className="w-full" onClick={() => shareLink(link.link!)} type="button">{t("team.invite.share")}</Button>}
          <Button className="w-full" onClick={() => void copyLink(link.link!)} type="button" variant={isTelegram ? "secondary" : "primary"}>{t("team.invite.copy")}</Button>
        </> : <p className="text-sm text-brand-muted">{t("team.error.failed")}</p>}
        <Button className="w-full" onClick={closeInvite} type="button" variant="secondary">{t("team.invite.done")}</Button>
      </div>}
    </BottomSheet>

    <BottomSheet onClose={() => !busy && setRoleFor(null)} open={Boolean(roleFor)} title={t("team.members.changeRoleTitle", { name: roleFor?.firstName ?? "" })}>
      {roleFor && <div className="team-roles" role="radiogroup" aria-label={t("team.invite.role")}>
        {rolesYouMayGive(team.myRole).map((role) => <button aria-checked={roleFor.role === role} className={`ftue-role-card${roleFor.role === role ? " ftue-role-card--selected" : ""}`} disabled={busy} key={role} onClick={() => { if (role === roleFor.role) { setRoleFor(null); return; } void act(() => changeTeamRole(roleFor.id, role), t("team.members.roleChanged")).then((ok) => ok && setRoleFor(null)); }} role="radio" type="button">
          <span className="ftue-role-card__copy"><span className="ftue-role-card__title">{roleName(role)}</span><span className="ftue-role-card__description">{t(`team.roleHint.${role}`)}</span></span>
        </button>)}
      </div>}
    </BottomSheet>

    <Modal onClose={() => !busy && setConfirm(null)} open={Boolean(confirm)} title={confirm?.kind === "remove" ? t("team.confirm.removeTitle", { name: confirm.member.firstName }) : confirm?.kind === "alone" ? t("team.confirm.aloneTitle") : t("team.confirm.leaveTitle")}>
      <p className="text-sm leading-6 text-brand-muted">{confirm?.kind === "remove" ? t("team.confirm.removeText") : confirm?.kind === "alone" ? t("team.confirm.aloneText") : t("team.confirm.leaveText", { company: team.companyName })}</p>
      <div className="mt-5 modal-actions">
        <Button disabled={busy} onClick={() => setConfirm(null)} type="button" variant="secondary">{t("common.cancel")}</Button>
        <Button aria-busy={busy} disabled={busy} onClick={() => void runConfirm()} type="button" variant={confirm?.kind === "alone" ? "primary" : "danger"}>{confirm?.kind === "remove" ? t("team.members.remove") : confirm?.kind === "alone" ? t("team.mode.switchToAlone") : t("team.leave")}</Button>
      </div>
    </Modal>
    <Toast key={toast?.id} message={toast?.message ?? ""} tone={toast?.tone ?? "success"} />
    <BottomNav />
  </div>;
}
