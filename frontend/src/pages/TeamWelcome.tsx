import { sendWorkOnComputerLink, siteAddress } from "../api/team";
import { AtlasBackground } from "../components/AtlasBackground";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { Button, Icon } from "../components/ui";
import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";

// D64 stage C1: the window right after a company "with a team" is created. Three ways on: the site on a computer
// (opened at once; the bot also puts the address into the chat), inviting the first person, or staying here.
export function TeamWelcome({ onDone }: { onDone: (route: string) => void }) {
  const { t } = useI18n();
  const { haptic, openLink } = useTelegram();
  const address = siteAddress();

  const workOnComputer = () => {
    haptic.selection();
    openLink(address);
    void sendWorkOnComputerLink().catch(() => undefined);
    onDone("/");
  };

  return <main className="ftue-screen ftue-screen--atlas team-welcome">
    <AtlasBackground />
    <div className="ftue-screen__layout">
      <section className="ftue-screen__content" data-content-header>
        <div className="ftue-screen__language"><LanguageSwitcher /></div>
        <span aria-hidden="true" className="ftue-success__indicator"><Icon name="check" /></span>
        <h1 className="ftue-screen__title">{t("team.welcome.title")}</h1>
        <ul className="team-welcome__points">
          <li><Icon name="briefcase" /><span>{t("team.welcome.computer", { address: address.replace(/^https?:\/\//, "") })}</span></li>
          <li><Icon name="users" /><span>{t("team.welcome.invite")}</span></li>
        </ul>
      </section>
      <div className="grid gap-2">
        <Button className="ftue-primary-button w-full" onClick={workOnComputer} type="button">{t("team.computer.button")}</Button>
        <Button className="w-full" onClick={() => { haptic.selection(); onDone("/team"); }} type="button" variant="secondary">{t("team.invite.button")}</Button>
        <Button className="w-full" onClick={() => onDone("/")} type="button" variant="ghost">{t("team.welcome.stay")}</Button>
      </div>
    </div>
  </main>;
}
