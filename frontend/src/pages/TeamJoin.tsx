import { useCallback, useEffect, useState } from "react";
import { ApiError } from "../api/client";
import { getCurrentPlatformUser } from "../api/marketplace";
import { previewTeamInvitation, requestTeamAccess, type TeamInvitationPreview } from "../api/team";
import { AtlasBackground } from "../components/AtlasBackground";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { Avatar, Button } from "../components/ui";
import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";
import { PhoneVerification } from "./PhoneVerification";
import { teamErrorMessage } from "./Team";

type State =
  | { kind: "loading" }
  | { kind: "phone" }
  | { kind: "ready"; preview: TeamInvitationPreview }
  | { kind: "busy"; preview: TeamInvitationPreview }
  | { kind: "failed"; message: string };

// D64 stage C1: the Mini App opened from a team invitation (startapp=join_<code>). The person sees the company and
// the role, knows the owner will see their name and number, and asks for access; the bot tells them the answer.
// onClose(opened): opened is true when they are already in the team and the app should open as the company.
export function TeamJoin({ code, onClose }: { code: string; onClose: (opened: boolean) => void }) {
  const { t } = useI18n();
  const { haptic } = useTelegram();
  const [state, setState] = useState<State>({ kind: "loading" });

  const load = useCallback(async () => {
    setState({ kind: "loading" });
    try {
      // The account exists and has the number Telegram confirmed (D41) before anyone sees it.
      const user = await getCurrentPlatformUser();
      if (!user.verifiedPhone) { setState({ kind: "phone" }); return; }
      setState({ kind: "ready", preview: await previewTeamInvitation(code) });
    } catch (error) {
      setState({ kind: "failed", message: error instanceof ApiError && error.status === 404 ? t("team.join.gone") : teamErrorMessage(error, t) });
    }
  }, [code, t]);
  useEffect(() => { void load(); }, [load]);

  const ask = async (preview: TeamInvitationPreview) => {
    haptic.selection();
    setState({ kind: "busy", preview });
    try {
      setState({ kind: "ready", preview: await requestTeamAccess(code) });
      haptic.success();
    } catch (error) {
      haptic.error();
      if (error instanceof ApiError && error.code === "phone_not_verified") { setState({ kind: "phone" }); return; }
      setState({ kind: "failed", message: teamErrorMessage(error, t) });
    }
  };

  if (state.kind === "phone") return <PhoneVerification onVerified={() => void load()} />;

  const preview = state.kind === "ready" || state.kind === "busy" ? state.preview : null;
  const busy = state.kind === "busy" || state.kind === "loading";
  const role = preview ? t(`team.role.${preview.role}`) : "";
  const note = !preview ? (state.kind === "failed" ? state.message : null)
    : preview.state === "requestedByYou" ? t("team.join.waiting", { company: preview.companyName })
      : preview.state === "joined" ? t("team.join.joined", { company: preview.companyName })
        : preview.state === "declined" ? t("team.join.declined")
          : preview.state === "expired" ? t("team.join.expired")
            : preview.state === "open" ? null
              : t("team.join.used");

  return <main className="ftue-screen ftue-screen--atlas browser-confirm team-join">
    <AtlasBackground />
    <div className="ftue-screen__layout">
      <section className="ftue-screen__content" data-content-header>
        <div className="ftue-screen__language"><LanguageSwitcher /></div>
        <p className="ftue-screen__kicker">{t("common.appName")}</p>
        <h1 className="ftue-screen__title">{t("team.join.title")}</h1>
        {preview && <div className="browser-confirm__device">
          <Avatar name={preview.companyName} size="md" src={preview.companyLogoUrl} variant="neutral" />
          <p><strong>{preview.companyName}</strong><span>{t("team.join.role", { role })}</span><span>{t(`team.roleHint.${preview.role}`)}</span></p>
        </div>}
        {preview?.state === "open" && <p className="ftue-screen__description ftue-screen__description--left">{t("team.join.privacy")}</p>}
        {note && <p className="phone-verification__note" role="status">{note}</p>}
      </section>
      <div className="grid gap-2">
        {preview?.state === "open" && <Button aria-busy={busy} className="ftue-primary-button w-full" disabled={busy} onClick={() => void ask(preview)} type="button">{t("team.join.ask")}</Button>}
        {preview?.state === "joined" && <Button className="ftue-primary-button w-full" onClick={() => onClose(true)} type="button">{t("team.join.open")}</Button>}
        {state.kind !== "loading" && preview?.state !== "joined" && <Button className="w-full" disabled={state.kind === "busy"} onClick={() => onClose(false)} type="button" variant="secondary">{t("browser.confirm.close")}</Button>}
      </div>
    </div>
  </main>;
}
