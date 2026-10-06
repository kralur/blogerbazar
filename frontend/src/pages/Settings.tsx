import { useState } from "react";
import { AccountActions } from "../components/AccountActions";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { PageHeader } from "../components/PageHeader";
import { BottomNav } from "../components/ui";
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
    </div>
    <AccountActions onSessionReset={onSessionReset} />
    <p className="text-center text-xs text-brand-muted">BloggerBazar · {t("common.version", { version: "1.0.0" })}</p>
    <BottomNav />
  </div>;
}
