import { Button } from "../components/ui";
import { cityLabel, useI18n } from "../i18n";
import { formatCompactNumber, formatNumber } from "../lib/currency";

// The first screen in the "Bazar" direction (D62, stage 3): lapis, the question of the marketplace in the display
// face and three example price tags hung on a cord, like the landing's first screen. The tags are examples, not
// real orders, and the note under them says so.
const exampleTags = [
  { key: "reels", labelKey: "firstRun.tagReels", amount: 900_000, city: "tashkent" },
  { key: "stories", labelKey: "firstRun.tagStories", amount: 300_000, city: "samarkand" },
  { key: "integration", labelKey: "firstRun.tagIntegration", amount: 1_500_000, city: "tashkent" }
] as const;

export function Welcome({ onContinue }: { onContinue: () => void }) {
  const { language, t } = useI18n();
  return <main className="ftue-screen bazar-welcome"><div className="ftue-screen__layout bazar-welcome__layout">
    <section className="bazar-welcome__content" data-content-header>
      <p className="bazar-welcome__brand">{t("common.appName")}</p>
      <h1 className="bazar-welcome__title">{t("firstRun.welcomeTitle")}</h1>
      <p className="bazar-welcome__description">{t("firstRun.welcomeSubtitle")}</p>
      <ul aria-hidden="true" className="bazar-welcome__tags">
        {exampleTags.map((tag, index) => <li className={`bazar-welcome__tag bazar-welcome__tag--${index + 1}`} key={tag.key}>
          <small>{t(tag.labelKey)}</small>
          <strong>{tag.amount >= 1_000_000 ? formatCompactNumber(tag.amount) : formatNumber(tag.amount)}</strong>
          <span>{t("currency.uzs")} · {cityLabel(tag.city, language)}</span>
        </li>)}
      </ul>
      <p className="bazar-welcome__note">{t("firstRun.examplesNote")}</p>
    </section>
    <Button aria-label={t("firstRun.start")} className="ftue-primary-button w-full" onClick={onContinue} type="button" variant="secondary">{t("firstRun.start")}</Button>
  </div></main>;
}
