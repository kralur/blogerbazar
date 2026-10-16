import type { ReactNode } from "react";
import { useI18n } from "../i18n";
import type { Loaded } from "../lib/useLoad";
import { Card, LoadFailed, Spinner } from "./ui";

// A card for one report: its title and hint, then the numbers once they arrive, or a retry when they did not.
export function Report<T>({ title, hint, loaded, onRetry, actions, children }: {
  title: string;
  hint?: string;
  loaded: Loaded<T>;
  onRetry: () => void;
  actions?: ReactNode;
  children: (data: T) => ReactNode;
}) {
  const { t } = useI18n();
  return (
    <Card className="min-w-0 space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 max-w-3xl">
          <h2 className="text-lg font-extrabold">{title}</h2>
          {hint && <p className="mt-1 text-sm leading-6 text-brand-muted">{hint}</p>}
        </div>
        {actions}
      </div>
      {loaded.status === "loading" && <Spinner label={t("app.loading")} />}
      {loaded.status === "error" && <LoadFailed message={t("report.loadError")} retry={t("common.retry")} onRetry={onRetry} />}
      {loaded.status === "ready" && children(loaded.data)}
    </Card>
  );
}
