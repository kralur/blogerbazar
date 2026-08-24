import { useState, type MouseEvent } from "react";
import { getApiErrorMessage } from "../api/client";
import { useFavorites } from "../features/favorites/FavoritesProvider";
import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";
import { Icon, Toast } from "./ui";

export function FavoriteButton({ bloggerId, brandFaceId, className, onChanged }: { bloggerId?: string; brandFaceId?: string; className?: string; onChanged?: (isFavorite: boolean) => void }) {
  const { canManageFavorite, isFavorite, ready, toggleFavorite } = useFavorites();
  const { t } = useI18n();
  const { haptic } = useTelegram();
  const [pending, setPending] = useState(false);
  const [toast, setToast] = useState("");
  const target = brandFaceId ? "brandFace" : "blogger";
  const id = brandFaceId ?? bloggerId;
  const saved = id ? isFavorite(target, id) : false;

  if (!id || !ready || !canManageFavorite(target)) return null;

  const handleClick = async (event: MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();
    event.stopPropagation();
    if (pending) return;
    setPending(true);
    setToast("");
    try {
      const next = await toggleFavorite(target, id);
      haptic.success();
      onChanged?.(next);
    } catch (reason) {
      setToast(getApiErrorMessage(reason, t("favorites.actionFailed")));
    } finally {
      setPending(false);
    }
  };

  const label = target === "brandFace"
    ? saved ? t("favorites.removeBrandFaceAria") : t("favorites.saveBrandFaceAria")
    : saved ? t("favorites.removeAria") : t("favorites.saveAria");
  return <span className={className}><button aria-label={label} aria-pressed={saved} className={`catalog-favorite-button ${saved ? "catalog-favorite-button--saved" : ""}`} disabled={pending} onClick={(event) => void handleClick(event)} type="button"><Icon filled={saved} name="bookmark" /></button><Toast message={toast} tone="error" /></span>;
}
