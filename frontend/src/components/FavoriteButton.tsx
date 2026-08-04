import { useState, type MouseEvent } from "react";
import { getApiErrorMessage } from "../api/client";
import { useFavorites } from "../features/favorites/FavoritesProvider";
import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";
import { Icon } from "./ui";

export function FavoriteButton({ bloggerId, className, onChanged }: { bloggerId: string; className?: string; onChanged?: (isFavorite: boolean) => void }) {
  const { isEligible, isFavorite, ready, toggleFavorite } = useFavorites();
  const { t } = useI18n();
  const { haptic } = useTelegram();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const saved = isFavorite(bloggerId);

  if (!ready || !isEligible) return null;

  const handleClick = async (event: MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();
    event.stopPropagation();
    if (pending) return;
    setPending(true);
    setError("");
    try {
      const next = await toggleFavorite(bloggerId);
      haptic.success();
      onChanged?.(next);
    } catch (reason) {
      haptic.error();
      setError(getApiErrorMessage(reason, t("favorites.actionFailed")));
    } finally {
      setPending(false);
    }
  };

  return <span className={className}><button aria-label={saved ? t("favorites.removeAria") : t("favorites.saveAria")} aria-pressed={saved} className={`grid h-10 w-10 place-items-center rounded-full border transition active:scale-95 ${saved ? "border-blue-200 bg-blue-50 text-brand-blue" : "border-brand-line bg-white/90 text-brand-muted"}`} disabled={pending} onClick={(event) => void handleClick(event)} title={error || undefined} type="button"><Icon filled={saved} name="bookmark" /></button>{error && <span className="sr-only" role="alert">{error}</span>}</span>;
}
