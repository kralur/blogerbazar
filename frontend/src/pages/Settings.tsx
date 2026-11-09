import { useEffect, useState } from "react";
import { getCurrentPlatformUser, peekCurrentPlatformUser, setBotTips } from "../api/marketplace";
import { AccountActions } from "../components/AccountActions";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { AtlasStrip } from "../components/AtlasStrip";
import { PageHeader } from "../components/PageHeader";
import { ListHero } from "../components/ListHero";
import { SeasonArt } from "../components/SeasonArt";
import { BottomNav, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { isAtlasOn, setAtlas } from "../lib/atlasPreference";
import { getSeasonPreference, seasonOf, seasons, setSeasonPreference, type Season, type SeasonPreference } from "../lib/season";
import { getThemePreference, setThemePreference, type ThemePreference } from "../lib/themePreference";
import { useTelegram } from "../telegram/TelegramProvider";

const themeOptions: Array<{ value: ThemePreference; labelKey: string }> = [
  { value: "telegram", labelKey: "settings.themeTelegram" },
  { value: "light", labelKey: "settings.themeLight" },
  { value: "dark", labelKey: "settings.themeDark" }
];

export function Settings({ onSessionReset }: { onSessionReset?: () => void }) {
  const { language, t } = useI18n();
  const { haptic } = useTelegram();
  const [theme, setTheme] = useState<ThemePreference>(getThemePreference);
  // D63: five tiles, the standard design and the four seasons; "auto" follows the calendar and shows as its season.
  const [seasonPreference, setSeasonPreferenceState] = useState<SeasonPreference>(getSeasonPreference);
  const calendarSeason = seasonOf(new Date());
  const shownSeason: Season | "off" = seasonPreference === "auto" ? calendarSeason : seasonPreference;
  const [atlasOn, setAtlasOnState] = useState<boolean>(isAtlasOn);
  const chooseAtlas = (on: boolean) => {
    if (on === atlasOn) return;
    setAtlasOnState(on);
    setAtlas(on);
    saved();
  };
  // Owners and admins (platform roles 3 and 4) get the way into the admin panel; nobody else sees it.
  // The account loaded at app start draws these rows at once (no jump a second later); the request below refreshes them.
  const known = peekCurrentPlatformUser();
  const [isAdmin, setIsAdmin] = useState(() => Boolean(known && known.role >= 3 && !known.isBlocked));
  // D54: bot tips; null until the account is known, so the switch never shows a guess.
  const [tipsEnabled, setTipsEnabled] = useState<boolean | null>(() => known ? known.tipsEnabled ?? true : null);
  const [toast, setToast] = useState<{ id: number; message: string; tone: "success" | "error" } | null>(null);
  const saved = () => { haptic.success(); setToast({ id: Date.now(), message: t("settings.saved"), tone: "success" }); };
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
    try {
      const user = await setBotTips(enabled);
      setTipsEnabled(user.tipsEnabled ?? enabled);
      saved();
    } catch {
      setTipsEnabled(previous);
      setToast({ id: Date.now(), message: t("settings.tipsFailed"), tone: "error" });
    }
  };

  // Picking the calendar's own season keeps following the calendar, so the design still turns with the year.
  const chooseSeason = (tile: Season | "off") => {
    if (tile === shownSeason) return;
    const value: SeasonPreference = tile === calendarSeason ? "auto" : tile;
    setSeasonPreferenceState(value);
    setSeasonPreference(value);
    saved();
  };

  const chooseTheme = (value: ThemePreference) => {
    setTheme(value);
    setThemePreference(value);
    saved();
  };

  // D63, package G: the lapis head with the current state in one line, the rows below.
  const state = [language.toUpperCase(), t(themeOptions.find((option) => option.value === theme)?.labelKey ?? "settings.themeTelegram"), shownSeason === "off" ? t("settings.seasonDefault") : t(`settings.season.${shownSeason}`)].join(" · ");
  return <div className="screen screen--with-nav space-y-5 px-4 pt-5">
    <ListHero className="detail-hero settings-hero">
      <PageHeader back={{ href: "#/profile", label: t("nav.profile") }} eyebrow={t("profile.eyebrow")} title={t("profile.settings")} />
      <p className="settings-hero__state">{state}</p>
    </ListHero>
    <div className="settings-list">
      <div className="settings-row"><span className="settings-row__text"><strong>{t("language.interface")}</strong></span><LanguageSwitcher /></div>
      <div className="settings-row settings-row--stacked">
        <span className="settings-row__text"><strong>{t("settings.theme")}</strong></span>
        <div aria-label={t("settings.theme")} className="language-switcher language-switcher--three" role="group">
          {themeOptions.map((option) => <button aria-pressed={theme === option.value} className="language-switcher__option" key={option.value} onClick={() => chooseTheme(option.value)} type="button">{t(option.labelKey)}</button>)}
        </div>
      </div>
      <div className="settings-row settings-row--stacked">
        <span className="settings-row__text"><strong>{t("settings.season")}</strong><span className="text-sm text-brand-muted">{t("settings.seasonHint")}</span></span>
        <div aria-label={t("settings.season")} className="season-previews" role="group">
          <button aria-pressed={shownSeason === "off"} className="season-preview season-preview--standard" data-season="none" onClick={() => chooseSeason("off")} type="button">
            <span className="season-preview__label">{t("settings.seasonDefault")}</span>
            <AtlasStrip fixed height={18} />
          </button>
          {seasons.map((season) => <button aria-pressed={shownSeason === season} className="season-preview" data-season={season} key={season} onClick={() => chooseSeason(season)} type="button">
            <span className="season-preview__label">{t(`settings.season.${season}`)}</span>
            <span className="season-preview__art"><SeasonArt season={season} variant="static" /></span>
            <AtlasStrip fixed height={18} />
          </button>)}
        </div>
      </div>
      <div className="settings-row settings-row--stacked">
        <span className="settings-row__text"><strong>{t("settings.atlas")}</strong><span className="text-sm text-brand-muted">{t("settings.atlasHint")}</span></span>
        <div aria-label={t("settings.atlas")} className="language-switcher language-switcher--three language-switcher--two" role="group">
          <button aria-pressed={atlasOn} className="language-switcher__option" onClick={() => chooseAtlas(true)} type="button">{t("settings.atlasOn")}</button>
          <button aria-pressed={!atlasOn} className="language-switcher__option" onClick={() => chooseAtlas(false)} type="button">{t("settings.atlasOff")}</button>
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
    <Toast key={toast?.id} message={toast?.message ?? ""} tone={toast?.tone ?? "success"} />
    <BottomNav />
  </div>;
}
