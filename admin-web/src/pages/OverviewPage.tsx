import type { AdminWebSession } from "../api/session";
import { Card } from "../components/ui";
import { useI18n } from "../i18n";

// Phase 1: the place for the main numbers; dashboard tiles and charts arrive with the analytics endpoints.
export function OverviewPage({ session }: { session: AdminWebSession }) {
  const { t } = useI18n();
  return (
    <div className="space-y-4">
      <p className="text-base text-brand-muted">{t("overview.welcome", { name: session.firstName })}</p>
      <Card>
        <h2 className="text-lg font-extrabold">{t("overview.placeholderTitle")}</h2>
        <p className="mt-2 max-w-2xl text-sm leading-6 text-brand-muted">{t("overview.placeholderText")}</p>
      </Card>
    </div>
  );
}

export function NotFoundPage({ onBack }: { onBack: () => void }) {
  const { t } = useI18n();
  return (
    <Card>
      <h2 className="text-lg font-extrabold">{t("notFound.title")}</h2>
      <button type="button" className="mt-3 text-sm font-semibold text-brand-blue underline-offset-4 hover:underline" onClick={onBack}>{t("notFound.back")}</button>
    </Card>
  );
}
