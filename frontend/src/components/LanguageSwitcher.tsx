import { useI18n, type Language } from "../i18n";

export function LanguageSwitcher({ className = "" }: { className?: string }) {
  const { language, setLanguage, t } = useI18n();
  const options: Array<{ value: Language; label: string; accessibleLabel: string }> = [
    { value: "ru", label: "RU", accessibleLabel: t("language.russian") },
    { value: "uz", label: "UZ", accessibleLabel: t("language.uzbek") }
  ];

  return <div aria-label={t("language.interface")} className={`language-switcher ${className}`.trim()} role="group">
    {options.map((option) => <button
      aria-label={option.accessibleLabel}
      aria-pressed={language === option.value}
      className="language-switcher__option"
      key={option.value}
      onClick={() => setLanguage(option.value)}
      type="button"
    >{option.label}</button>)}
  </div>;
}
