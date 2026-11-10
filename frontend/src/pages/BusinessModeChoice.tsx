import { useState } from "react";
import { AtlasBackground } from "../components/AtlasBackground";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { Button, Icon } from "../components/ui";
import { useTelegram } from "../telegram/TelegramProvider";
import { useI18n } from "../i18n";
import { useTelegramBackHandler } from "../hooks/useTelegramBackHandler";

// D64 stage C1: right after the Business role, how the company will work. The company form is the same either way;
// "with a team" opens the team screen, invitations and work on a computer. It can be changed later on the Team screen.
export function BusinessModeChoice({ onChosen, onBack }: { onChosen: (teamMode: boolean) => void; onBack: () => void }) {
  const { t } = useI18n();
  const { haptic, isTelegram } = useTelegram();
  const [teamMode, setTeamMode] = useState<boolean | null>(null);
  useTelegramBackHandler(onBack, true);
  const options = [
    { value: false, title: t("team.mode.alone"), description: t("team.mode.aloneDescription"), icon: "user" },
    { value: true, title: t("team.mode.team"), description: t("team.mode.teamDescription"), icon: "users" }
  ];

  return <main className="ftue-screen ftue-screen--atlas ftue-role-selection business-mode">
    <AtlasBackground />
    <div className="ftue-screen__layout">
      <div className="ftue-role-selection__header" data-content-header>
        <div className="ftue-role-selection__language"><LanguageSwitcher /></div>
        <h1 className="ftue-role-selection__title">{t("team.mode.title")}</h1>
        <p className="ftue-role-selection__subtitle">{t("team.mode.subtitle")}</p>
      </div>

      <div aria-label={t("team.mode.title")} className="ftue-role-selection__list" role="radiogroup">
        {options.map((item) => {
          const selected = teamMode === item.value;
          return <button aria-checked={selected} aria-label={`${item.title}. ${item.description}`} className={`ftue-role-card${selected ? " ftue-role-card--selected" : ""}`} key={item.icon} onClick={() => { haptic.selection(); setTeamMode(item.value); }} role="radio" type="button">
            <span aria-hidden="true" className="ftue-role-card__icon"><Icon name={item.icon} /></span>
            <span className="ftue-role-card__copy"><span className="ftue-role-card__title">{item.title}</span><span className="ftue-role-card__description">{item.description}</span></span>
            <span aria-hidden="true" className="ftue-role-card__check"><Icon name="check" /></span>
          </button>;
        })}
      </div>

      <div className="grid gap-2">
        <Button className="ftue-primary-button ftue-role-selection__cta w-full" disabled={teamMode === null} onClick={() => teamMode !== null && onChosen(teamMode)} type="button" variant="secondary">{t("wizard.continue")}</Button>
        {!isTelegram && <Button className="w-full" onClick={onBack} type="button" variant="ghost">{t("common.back")}</Button>}
      </div>
    </div>
  </main>;
}
