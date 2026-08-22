import { useState, type MouseEvent } from "react";
import { getApiErrorMessage } from "../api/client";
import { useFavorites } from "../features/favorites/FavoritesProvider";
import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";
import { Icon, Toast } from "./ui";

export function FavoriteButton({ bloggerId, className, onChanged }: { bloggerId: string; className?: string; onChanged?: (isFavorite: boolean) => void }) {
  const { isEligible, isFavorite, ready, toggleFavorite } = useFavorites();
  const { t } = useI18n();
  const { haptic } = useTelegram();
  const [pending, setPending] = useState(false);
  const [toast, setToast] = useState("");
  const saved = isFavorite(bloggerId);

  if (!ready || !isEligible) return null;

  const handleClick = async (event: MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();
    event.stopPropagation();
    if (pending) return;
    setPending(true);
    setToast("");
    try {
      const next = await toggleFavorite(bloggerId);
      haptic.success();
      onChanged?.(next);
    } catch (reason) {
      setToast(getApiErrorMessage(reason, t("favorites.actionFailed")));
    } finally {
      setPending(false);
    }
  };

  return <span className={className}><button aria-label={saved ? t("favorites.removeAria") : t("favorites.saveAria")} aria-pressed={saved} className={`catalog-favorite-button ${saved ? "catalog-favorite-button--saved" : ""}`} disabled={pending} onClick={(event) => void handleClick(event)} type="button"><Icon filled={saved} name="bookmark" /></button><Toast message={toast} tone="error" /></span>;
}
