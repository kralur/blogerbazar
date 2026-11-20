import { useI18n } from "../../i18n";
import { Icon } from "../ui";

// Lists open page by page (owner, 2026-11-20): about ten cards, then the next page, instead of an endless feed.
// A computer shows 12 (four rows of three), a phone 10.
export function listPageSize() {
  if (typeof window === "undefined" || typeof window.matchMedia !== "function") return 10;
  const insideTelegram = Boolean(window.Telegram?.WebApp?.initData);
  return !insideTelegram && window.matchMedia("(min-width: 1024px)").matches ? 12 : 10;
}

// 1 … 4 5 6 … 20: the first, the last and the neighbours of the current page.
function visiblePages(page: number, pages: number) {
  const shown = new Set([1, pages, page - 1, page, page + 1].filter((value) => value >= 1 && value <= pages));
  const sorted = [...shown].sort((a, b) => a - b);
  return sorted.flatMap((value, index) => index > 0 && value - sorted[index - 1] > 1 ? ["gap", value] as const : [value] as const);
}

export function Pager({ page, total, pageSize, onChange, disabled = false }: { page: number; total: number; pageSize: number; onChange: (page: number) => void; disabled?: boolean }) {
  const { t } = useI18n();
  const pages = Math.ceil(total / pageSize);
  if (pages <= 1) return null;
  const go = (next: number) => {
    if (disabled || next === page || next < 1 || next > pages) return;
    onChange(next);
    window.scrollTo({ top: 0, behavior: "smooth" });
  };
  return <nav aria-label={t("pager.aria")} className="pager">
    <button aria-label={t("pager.previous")} className="pager__step" disabled={disabled || page <= 1} onClick={() => go(page - 1)} type="button"><Icon name="back" /></button>
    {visiblePages(page, pages).map((value, index) => value === "gap"
      ? <span aria-hidden="true" className="pager__gap" key={`gap-${index}`}>…</span>
      : <button aria-current={value === page ? "page" : undefined} aria-label={t("pager.page", { page: value })} className="pager__page" disabled={disabled} key={value} onClick={() => go(value)} type="button">{value}</button>)}
    <button aria-label={t("pager.next")} className="pager__step pager__step--next" disabled={disabled || page >= pages} onClick={() => go(page + 1)} type="button"><Icon name="back" /></button>
  </nav>;
}
