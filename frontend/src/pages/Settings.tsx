import { useEffect, useState } from "react";
import { signOutBrowser } from "../api/browserSessions";
import { getCurrentPlatformUser, normalizeMarketplaceRole, peekCurrentPlatformUser, setBotTips } from "../api/marketplace";
import { AccountActions } from "../components/AccountActions";
import { BrowserSessions } from "../components/BrowserSessions";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { AtlasStrip } from "../components/AtlasStrip";
import { PageHeader } from "../components/PageHeader";
import { SeasonArt } from "../components/SeasonArt";
import { BottomNav, Button, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { isAtlasSoundOn, setAtlasSound } from "../lib/atlasSound";
import { isBrowserSession } from "../lib/browserSession";
import { getSeasonPreference, isSeasonDrawn, seasonOf, setSeasonPreference, type SeasonPreference } from "../lib/season";
import { getThemePreference, setThemePreference, type ThemePreference } from "../lib/themePreference";
import { useTelegram } from "../telegram/TelegramProvider";

const themeOptions: Array<{ value: ThemePreference; labelKey: string }> = [
  { value: "telegram", labelKey: "settings.themeTelegram" },
  { value: "light", labelKey: "settings.themeLight" },
  { value: "dark", labelKey: "settings.themeDark" }
];

// onBrowserSignedOut: in a browser session (D64 stage B), this browser has just been signed out.
export function Settings({ onSessionReset, onBrowserSignedOut }: { onSessionReset?: () => void; onBrowserSignedOut?: () => void }) {
  const { t } = useI18n();
  const { haptic } = useTelegram();
  const [theme, setTheme] = useState<ThemePreference>(getThemePreference);
  // D63: the seasonal design row appears only while the calendar season has drawn artefacts.
  const [seasonPreference, setSeasonPreferenceState] = useState<SeasonPreference>(getSeasonPreference);
  const seasonDrawn = isSeasonDrawn();
  const [atlasSound, setAtlasSoundState] = useState<boolean>(isAtlasSoundOn);
  const chooseAtlasSound = (on: boolean) => {
    if (on === atlasSound) return;
    setAtlasSoundState(on);
    setAtlasSound(on);
    saved();
  };
  const calendarSeason = seasonOf(new Date());
  // Owners and admins (platform roles 3 and 4) get the way into the admin panel; nobody else sees it.
  // The account loaded at app start draws these rows at once (no jump a second later); the request below refreshes them.
  const known = peekCurrentPlatformUser();
  const inBrowser = isBrowserSession();
  const [isAdmin, setIsAdmin] = useState(() => Boolean(known && known.role >= 3 && !known.isBlocked));
  // D64 stage B: company members see their browsers; in a browser the list is always there.
  const [isBusiness, setIsBusiness] = useState(() => normalizeMarketplaceRole(known?.selectedMarketplaceRole) === "Business");
  const [signingOut, setSigningOut] = useState(false);
  // D54: bot tips; null until the account is known, so the switch never shows a guess.
  const [tipsEnabled, setTipsEnabled] = useState<boolean | null>(() => known ? known.tipsEnabled ?? true : null);
  const [toast, setToast] = useState<{ id: number; message: string; tone: "success" | "error" } | null>(null);
  const saved = () => { haptic.success(); setToast({ id: Date.now(), message: t("settings.saved"), tone: "success" }); };
  useEffect(() => {
    let active = true;
    getCurrentPlatformUser().then((user) => {
      if (!active) return;
      setIsAdmin(user.role >= 3 && !user.isBlocked);
      setIsBusiness(normalizeMarketplaceRole(user.selectedMarketplaceRole) === "Business");
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

  const signOut = async () => {
    setSigningOut(true);
    try { await signOutBrowser(); } catch { /* the cookie ends with the session anyway */ }
    onBrowserSignedOut?.();
  };

  const chooseSeason = (value: SeasonPreference) => {
    if (value === seasonPreference) return;
    setSeasonPreferenceState(value);
    setSeasonPreference(value);
    saved();
  };

  const chooseTheme = (value: ThemePreference) => {
    setTheme(value);
    setThemePreference(value);
    saved();
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
      {seasonDrawn && <div className="settings-row settings-row--stacked">
        <span className="settings-row__text"><strong>{t("settings.season")}</strong><span className="text-sm text-brand-muted">{t(`settings.seasonHint.${calendarSeason}`)}</span></span>
        <div aria-label={t("settings.season")} className="season-previews" role="group">
          <button aria-pressed={seasonPreference === "auto"} className="season-preview" data-season={calendarSeason} onClick={() => chooseSeason("auto")} type="button">
            <span className="season-preview__label">{t(`settings.season.${calendarSeason}`)}</span>
            <span className="season-preview__art"><SeasonArt force variant="static" /></span>
            <AtlasStrip fixed height={18} />
          </button>
          <button aria-pressed={seasonPreference === "off"} className="season-preview" data-season="none" onClick={() => chooseSeason("off")} type="button">
            <span className="season-preview__label">{t("settings.seasonDefault")}</span>
            <AtlasStrip fixed height={18} />
          </button>
        </div>
      </div>}
      <div className="settings-row settings-row--stacked">
        <span className="settings-row__text"><strong>{t("settings.atlasSound")}</strong><span className="text-sm text-brand-muted">{t("settings.atlasSoundHint")}</span></span>
        <div aria-label={t("settings.atlasSound")} className="language-switcher language-switcher--three language-switcher--two" role="group">
          <button aria-pressed={atlasSound} className="language-switcher__option" onClick={() => chooseAtlasSound(true)} type="button">{t("settings.atlasSoundOn")}</button>
          <button aria-pressed={!atlasSound} className="language-switcher__option" onClick={() => chooseAtlasSound(false)} type="button">{t("settings.atlasSoundOff")}</button>
        </div>
      </div>
      {tipsEnabled !== null && <div className="settings-row settings-row--stacked">
        <span className="settings-row__text"><strong>{t("settings.tips")}</strong><span className="text-sm text-brand-muted">{t("settings.tipsHint")}</span></span>
        <div aria-label={t("settings.tips")} className="language-switcher language-switcher--three language-switcher--two" role="group">
          <button aria-pressed={tipsEnabled} className="language-switcher__option" onClick={() => void chooseTips(true)} type="button">{t("settings.tipsOn")}</button>
          <button aria-pressed={!tipsEnabled} className="language-switcher__option" onClick={() => void chooseTips(false)} type="button">{t("settings.tipsOff")}</button>
        </div>
      </div>}
      {isAdmin && !inBrowser && <a className="settings-row" href="#/admin"><span className="settings-row__text"><strong>{t("settings.adminPanel")}</strong><span className="text-sm text-brand-muted">{t("settings.adminPanelHint")}</span></span><span aria-hidden="true">›</span></a>}
    </div>
    {(isBusiness || inBrowser) && <BrowserSessions onSignedOut={onBrowserSignedOut} />}
    {inBrowser
      ? <div className="grid gap-2"><Button aria-busy={signingOut} className="w-full" disabled={signingOut} onClick={() => void signOut()} type="button" variant="secondary">{t("browser.signOut")}</Button><p className="text-center text-xs text-brand-muted">{t("browser.telegramOnly")}</p></div>
      : <AccountActions onSessionReset={onSessionReset} />}
    <p className="text-center text-xs text-brand-muted">BloggerBazar · {t("common.version", { version: "1.0.0" })}</p>
    <Toast key={toast?.id} message={toast?.message ?? ""} tone={toast?.tone ?? "success"} />
    <BottomNav />
  </div>;
}
