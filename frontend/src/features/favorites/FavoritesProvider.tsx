import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { getCurrentPlatformUser, getFavorites, normalizeMarketplaceRole, removeFavorite, saveFavorite } from "../../api/marketplace";

type FavoritesContextValue = {
  isEligible: boolean;
  ready: boolean;
  isFavorite: (bloggerId: string) => boolean;
  toggleFavorite: (bloggerId: string) => Promise<boolean>;
  refreshFavorites: () => Promise<void>;
};

const FavoritesContext = createContext<FavoritesContextValue | null>(null);

export function FavoritesProvider({ children, enabled = true }: { children: ReactNode; enabled?: boolean }) {
  const [favoriteIds, setFavoriteIds] = useState<Set<string>>(() => new Set());
  const [isEligible, setIsEligible] = useState(false);
  const [ready, setReady] = useState(false);
  const stateVersionRef = useRef(0);
  const mutationVersionsRef = useRef(new Map<string, number>());

  const refreshFavorites = useCallback(async () => {
    const refreshVersion = stateVersionRef.current;
    if (refreshVersion === 0) setReady(false);
    try {
      const user = await getCurrentPlatformUser();
      const role = normalizeMarketplaceRole(user.selectedMarketplaceRole);
      const canManageFavorites = role === "Business" || role === "BrandFace";
      if (refreshVersion !== stateVersionRef.current) return;
      setIsEligible(canManageFavorites);
      if (!canManageFavorites) {
        setFavoriteIds(new Set());
        return;
      }

      const response = await getFavorites();
      if (refreshVersion !== stateVersionRef.current) return;
      setFavoriteIds(new Set(response.items.map((item) => item.bloggerId)));
    } catch {
      if (refreshVersion !== stateVersionRef.current) return;
      setIsEligible(false);
      setFavoriteIds(new Set());
    } finally {
      if (refreshVersion === stateVersionRef.current) setReady(true);
    }
  }, []);

  useEffect(() => {
    if (enabled) {
      void refreshFavorites();
      return;
    }

    stateVersionRef.current += 1;
    setIsEligible(false);
    setFavoriteIds(new Set());
    setReady(false);
  }, [enabled, refreshFavorites]);

  const toggleFavorite = useCallback(async (bloggerId: string) => {
    const saved = favoriteIds.has(bloggerId);
    const mutationVersion = ++stateVersionRef.current;
    mutationVersionsRef.current.set(bloggerId, mutationVersion);
    setFavoriteIds((current) => {
      const next = new Set(current);
      if (saved) next.delete(bloggerId); else next.add(bloggerId);
      return next;
    });

    try {
      if (saved) await removeFavorite(bloggerId); else await saveFavorite(bloggerId);
      return !saved;
    } catch (error) {
      if (mutationVersionsRef.current.get(bloggerId) !== mutationVersion) throw error;
      setFavoriteIds((current) => {
        const next = new Set(current);
        if (saved) next.add(bloggerId); else next.delete(bloggerId);
        return next;
      });
      throw error;
    }
  }, [favoriteIds]);

  const value = useMemo<FavoritesContextValue>(() => ({
    isEligible,
    ready,
    isFavorite: (bloggerId) => favoriteIds.has(bloggerId),
    toggleFavorite,
    refreshFavorites
  }), [favoriteIds, isEligible, ready, refreshFavorites, toggleFavorite]);

  return <FavoritesContext.Provider value={value}>{children}</FavoritesContext.Provider>;
}

export function useFavorites() {
  const context = useContext(FavoritesContext);
  if (!context) throw new Error("useFavorites must be used inside FavoritesProvider.");
  return context;
}
