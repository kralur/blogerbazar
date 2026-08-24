import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { getBrandFaceFavorites, getCurrentPlatformUser, getFavorites, normalizeMarketplaceRole, removeBrandFaceFavorite, removeFavorite, saveBrandFaceFavorite, saveFavorite, type FavoriteTarget, type MarketplaceRole } from "../../api/marketplace";

type FavoritesContextValue = {
  isEligible: boolean;
  ready: boolean;
  canManageFavorite: (target: FavoriteTarget) => boolean;
  isFavorite: (target: FavoriteTarget, id: string) => boolean;
  toggleFavorite: (target: FavoriteTarget, id: string) => Promise<boolean>;
  refreshFavorites: () => Promise<void>;
};

const FavoritesContext = createContext<FavoritesContextValue | null>(null);

export function FavoritesProvider({ children, enabled = true }: { children: ReactNode; enabled?: boolean }) {
  const [favoriteKeys, setFavoriteKeys] = useState<Set<string>>(() => new Set());
  const [isEligible, setIsEligible] = useState(false);
  const [marketplaceRole, setMarketplaceRole] = useState<MarketplaceRole>();
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
      setMarketplaceRole(role);
      if (!canManageFavorites) {
        setFavoriteKeys(new Set());
        return;
      }

      const bloggerIds = await loadAllBloggerFavoriteIds();
      const brandFaceIds = role === "Business" ? await loadAllBrandFaceFavoriteIds() : [];
      if (refreshVersion !== stateVersionRef.current) return;
      setFavoriteKeys(new Set([...bloggerIds.map((id) => favoriteKey("blogger", id)), ...brandFaceIds.map((id) => favoriteKey("brandFace", id))]));
    } catch {
      if (refreshVersion !== stateVersionRef.current) return;
      setIsEligible(false);
      setMarketplaceRole(undefined);
      setFavoriteKeys(new Set());
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
    setMarketplaceRole(undefined);
    setFavoriteKeys(new Set());
    setReady(false);
  }, [enabled, refreshFavorites]);

  const canManageFavorite = useCallback((target: FavoriteTarget) =>
    target === "blogger" ? isEligible : marketplaceRole === "Business", [isEligible, marketplaceRole]);

  const toggleFavorite = useCallback(async (target: FavoriteTarget, id: string) => {
    if (!canManageFavorite(target)) throw new Error("Favorite action is unavailable.");
    const key = favoriteKey(target, id);
    const saved = favoriteKeys.has(key);
    const mutationVersion = ++stateVersionRef.current;
    mutationVersionsRef.current.set(key, mutationVersion);
    setFavoriteKeys((current) => {
      const next = new Set(current);
      if (saved) next.delete(key); else next.add(key);
      return next;
    });

    try {
      if (target === "blogger") {
        if (saved) await removeFavorite(id); else await saveFavorite(id);
      } else {
        if (saved) await removeBrandFaceFavorite(id); else await saveBrandFaceFavorite(id);
      }
      return !saved;
    } catch (error) {
      if (mutationVersionsRef.current.get(key) !== mutationVersion) throw error;
      setFavoriteKeys((current) => {
        const next = new Set(current);
        if (saved) next.add(key); else next.delete(key);
        return next;
      });
      throw error;
    }
  }, [canManageFavorite, favoriteKeys]);

  const value = useMemo<FavoritesContextValue>(() => ({
    isEligible,
    ready,
    canManageFavorite,
    isFavorite: (target, id) => favoriteKeys.has(favoriteKey(target, id)),
    toggleFavorite,
    refreshFavorites
  }), [canManageFavorite, favoriteKeys, isEligible, ready, refreshFavorites, toggleFavorite]);

  return <FavoritesContext.Provider value={value}>{children}</FavoritesContext.Provider>;
}

function favoriteKey(target: FavoriteTarget, id: string) {
  return `${target}:${id}`;
}

async function loadAllBloggerFavoriteIds() {
  const ids: string[] = [];
  let page = 1;
  const pageSize = 50;
  while (true) {
    const response = await getFavorites(page, pageSize);
    ids.push(...response.items.map((item) => item.bloggerId));
    if (page * pageSize >= response.total) return ids;
    page += 1;
  }
}

async function loadAllBrandFaceFavoriteIds() {
  const ids: string[] = [];
  let page = 1;
  const pageSize = 50;
  while (true) {
    const response = await getBrandFaceFavorites(page, pageSize);
    ids.push(...response.items.map((item) => item.id));
    if (!response.hasMore) return ids;
    page += 1;
  }
}

export function useFavorites() {
  const context = useContext(FavoritesContext);
  if (!context) throw new Error("useFavorites must be used inside FavoritesProvider.");
  return context;
}
