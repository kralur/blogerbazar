import { useI18n, type Language } from "../i18n";
import { useTheme, type ThemeChoice } from "../theme/theme";

const selectClass = "h-10 min-w-0 rounded-[var(--bb-radius-control)] border border-brand-line bg-brand-surface px-3 text-sm text-brand-ink";

// Language and theme, remembered on this device only (localStorage holds nothing but these two choices).
export function Preferences() {
  const { language, setLanguage, t } = useI18n();
  const { choice, setChoice } = useTheme();
  return (
    <div className="flex flex-wrap items-center gap-2">
      <select aria-label={t("settings.language")} className={selectClass} value={language} onChange={(event) => setLanguage(event.target.value as Language)}>
        <option value="ru">RU</option>
        <option value="uz">UZ</option>
      </select>
      <select aria-label={t("settings.theme")} className={selectClass} value={choice} onChange={(event) => setChoice(event.target.value as ThemeChoice)}>
        <option value="system">{t("settings.themeSystem")}</option>
        <option value="light">{t("settings.themeLight")}</option>
        <option value="dark">{t("settings.themeDark")}</option>
      </select>
    </div>
  );
}
