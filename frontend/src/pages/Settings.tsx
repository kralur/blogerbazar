import { useEffect, useState } from "react";
import { getCurrentPlatformUser, peekCurrentPlatformUser, setBotTips } from "../api/marketplace";
import { AccountActions } from "../components/AccountActions";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { PageHeader } from "../components/PageHeader";
import { BottomNav, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { getThemePreference, setThemePreference, type ThemePreference } from "../lib/themePreference";
import { useTelegram } from "../telegram/TelegramProvider";

const themeOptions: Array<{ value: ThemePreference; labelKey: string }> = [
  { value: "telegram", labelKey: "settings.themeTelegram" },
  { value: "light", labelKey: "settings.themeLight" },
  { value: "dark", labelKey: "settings.themeDark" }
];

export function Settings({ onSessionReset }: { onSessionReset?: () => void }) {
  const { t } = useI18n();
  const { haptic } = useTelegram();
  const [theme, setTheme] = useState<ThemePreference>(getThemePreference);
  // Owners and admins (platform roles 3 and 4) get the way into the admin panel; nobody else sees it.
  // The account loaded at app start draws these rows at once (no jump a second later); the request below refreshes them.
  const known = peekCurrentPlatformUser();
  const [isAdmin, setIsAdmin] = useState(() => Boolean(known && known.role >= 3 && !known.isBlocked));
  // D54: bot tips; null until the account is known, so the switch never shows a guess.
  const [tipsEnabled, setTipsEnabled] = useState<boolean | null>(() => known ? known.tipsEnabled ?? true : null);
  const [toast, setToast] = useState("");
  useEffect(() => {
    let active = true;
    getCurrentPlatformUser().then((user) => {
      if (!active) return;
      setIsAdmin(user.role >= 3 && !user.isBlocked);
      setTipsEnabled(user.tipsEnabled ?? true);
    }).catch(() => undefined);
    return () => { active = false; };
  }, []);

  const chooseTips = async (enabled: boolean) => {
    if (tipsEnabled === enabled) return;
    haptic.selection();
    const previous = tipsEnabled;
    setTipsEnabled(enabled);
    setToast("");
    try {
      const user = await setBotTips(enabled);
      setTipsEnabled(user.tipsEnabled ?? enabled);
    } catch {
      setTipsEnabled(previous);
      setToast(t("settings.tipsFailed"));
    }
  };

  const chooseTheme = (value: ThemePreference) => {
    haptic.selection();
    setTheme(value);
    setThemePreference(value);
  };

  return <div className="screen screen--with-nav space-y-5 px-4 pt-5">
    <PageHeader back={{ href: "#/profile", label: t("nav.profile") }} eyebrow={t("profile.eyebrow")} title={t("profile.settings")} />
    <div className="settings-list">
      <div className="settings-row"><span className="settings-row__text"><strong>{t("language.interface")}</strong></span><LanguageSwitcher /></div>
      <div className="settings-row settings-row--stacked">
        <span className="settings-row__text"><strong>{t("settings.theme")}</strong></span>
        <div aria-label={t("settings.theme")} className="language-switcher language-switcher--three" role="group">
          {themeOptions.map((option) => <button aria-pressed={theme === option.value} className="language-switcher__option" key={option.value} onClick={() => chooseTheme(option.value)} type="button">{t(option.labelKey)}</button>)}
        </div>
      </div>
      {tipsEnabled !== null && <div className="settings-row settings-row--stacked">
        <span className="settings-row__text"><strong>{t("settings.tips")}</strong><span className="text-sm text-brand-muted">{t("settings.tipsHint")}</span></span>
        <div aria-label={t("settings.tips")} className="language-switcher language-switcher--three language-switcher--two" role="group">
          <button aria-pressed={tipsEnabled} className="language-switcher__option" onClick={() => void chooseTips(true)} type="button">{t("settings.tipsOn")}</button>
          <button aria-pressed={!tipsEnabled} className="language-switcher__option" onClick={() => void chooseTips(false)} type="button">{t("settings.tipsOff")}</button>
        </div>
      </div>}
      {isAdmin && <a className="settings-row" href="#/admin"><span className="settings-row__text"><strong>{t("settings.adminPanel")}</strong><span className="text-sm text-brand-muted">{t("settings.adminPanelHint")}</span></span><span aria-hidden="true">›</span></a>}
    </div>
    <AccountActions onSessionReset={onSessionReset} />
    <p className="text-center text-xs text-brand-muted">BloggerBazar · {t("common.version", { version: "1.0.0" })}</p>
    <Toast message={toast} tone="error" />
    <BottomNav />
  </div>;
}
