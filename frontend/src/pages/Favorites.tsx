import { useCallback, useEffect, useRef, useState } from "react";
import { getBrandFaceFavorites, getFavorites, type FavoriteBlogger, type FavoriteBrandFace } from "../api/marketplace";
import { BrandFaceCard } from "../components/BrandFaceCard";
import { FavoriteButton } from "../components/FavoriteButton";
import { CatalogState, SearchSkeleton } from "../components/catalog/CatalogShared";
import { usePaginatedCatalog } from "../components/catalog/usePaginatedCatalog";
import { Avatar, BottomNav, Card, Icon } from "../components/ui";
import { useFavorites } from "../features/favorites/FavoritesProvider";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatNumber } from "../lib/currency";
import { LanguageSwitcher } from "../components/LanguageSwitcher";

type FavoriteTab = "blogger" | "brandFace";
type MutableRef<T> = { current: T };
const pageSize = 20;

export function Favorites() {
  const { t } = useI18n();
  const { canManageFavorite, ready } = useFavorites();
  const canManageBrandFaceFavorites = canManageFavorite("brandFace");
  const [activeTab, setActiveTab] = useState<FavoriteTab>("blogger");

  useEffect(() => {
    if (ready && !canManageBrandFaceFavorites) setActiveTab("blogger");
  }, [canManageBrandFaceFavorites, ready]);

  return <div className="screen screen--with-nav">
    <header className="flex items-center gap-3"><a aria-label={t("favorites.backAria")} className="grid h-11 w-11 place-items-center rounded-2xl bg-white shadow-card" href="#/profile"><Icon name="back" /></a><div className="min-w-0 flex-1"><p className="text-sm font-semibold text-brand-muted">{t("profile.eyebrow")}</p><h1 className="mt-1 text-3xl font-extrabold tracking-tight">{activeTab === "brandFace" ? t("favorites.brandFacesTitle") : t("favorites.title")}</h1></div><LanguageSwitcher /></header>
    {canManageBrandFaceFavorites && <div aria-label={t("favorites.creatorType")} className="catalog-search__segments mt-5" role="group">
      <FavoriteTabButton label={t("favorites.typeBloggers")} onClick={() => setActiveTab("blogger")} selected={activeTab === "blogger"} />
      <FavoriteTabButton label={t("favorites.typeBrandFaces")} onClick={() => setActiveTab("brandFace")} selected={activeTab === "brandFace"} />
    </div>}
    <div className="mt-5" hidden={activeTab !== "blogger"}><BloggerFavorites active={activeTab === "blogger"} /></div>
    {canManageBrandFaceFavorites && <div className="mt-5" hidden={activeTab !== "brandFace"}><BrandFaceFavorites active={activeTab === "brandFace"} /></div>}
    <BottomNav />
  </div>;
}

function FavoriteTabButton({ label, onClick, selected }: { label: string; onClick: () => void; selected: boolean }) {
  return <button aria-pressed={selected} className={`catalog-search__segment${selected ? " catalog-search__segment--selected" : ""}`} onClick={onClick} type="button"><span>{label}</span>{selected && <span aria-hidden="true" className="catalog-search__segment-indicator">✓</span>}</button>;
}

function BloggerFavorites({ active }: { active: boolean }) {
  const { language, t } = useI18n();
  const startedRef = useRef(false);
  const sentinelRef = useRef<HTMLDivElement | null>(null);
  useScrollRestoration("favorites:blogger", active);
  const fetchPage = useCallback(async (page: number) => {
    const response = await getFavorites(page, pageSize);
    return { items: response.items, total: response.total, page: response.page, hasMore: response.page * response.pageSize < response.total };
  }, []);
  const catalog = usePaginatedCatalog<FavoriteBlogger>({ active, fetchPage, getItemId: (item) => item.bloggerId });
  const refresh = useCallback(() => { void catalog.load(1, false); }, [catalog.load]);

  useInitialFavoriteLoad(active, startedRef, catalog.load, catalog.cancel);
  useProfileDataRefresh(useCallback(() => { if (active) refresh(); }, [active, refresh]));
  useInfiniteFavoritesScroll({ active, failure: catalog.failure, hasMore: catalog.hasMore, load: catalog.load, loadMoreFailed: catalog.loadMoreFailed, loading: catalog.loading, loadingMore: catalog.loadingMore, page: catalog.page, sentinelRef });

  return <section aria-busy={catalog.loading || catalog.loadingMore} aria-live="polite" className="catalog-search__results">
    {catalog.loading ? <SearchSkeleton count={3} /> : catalog.failure ? <CatalogState icon="refresh" onRetry={refresh} subtitle={t("favorites.loadFailedSubtitle")} title={t("favorites.loadFailedTitle")} /> : catalog.items.length === 0 ? <CatalogState icon="bookmark" subtitle={t("favorites.emptySubtitle")} title={t("favorites.emptyTitle")} /> : catalog.items.map((blogger) => <Card className="relative p-3" key={blogger.bloggerId}><a className="flex min-w-0 items-center gap-3 pr-10" href={`#/blogger/${blogger.bloggerId}`}><Avatar name={blogger.name} size="sm" src={blogger.avatarUrl} /><span className="min-w-0 flex-1"><strong className="block truncate">{blogger.name}</strong><span className="mt-1 block truncate text-sm text-brand-muted">{cityLabel(blogger.city, language)} · {blogger.categories.map((category) => categoryLabel(category, language)).join(", ")}</span><span className="mt-1 block text-xs font-semibold text-brand-muted">{formatNumber(blogger.totalFollowers)} {t("common.followers").toLowerCase()}</span></span></a><FavoriteButton bloggerId={blogger.bloggerId} className="absolute right-3 top-1/2 -translate-y-1/2" /></Card>)}
    <FavoriteListFooter catalog={catalog} onRetry={() => void catalog.load(catalog.page + 1, true)} sentinelRef={sentinelRef} />
  </section>;
}

function BrandFaceFavorites({ active }: { active: boolean }) {
  const { t } = useI18n();
  const startedRef = useRef(false);
  const sentinelRef = useRef<HTMLDivElement | null>(null);
  useScrollRestoration("favorites:brand-face", active);
  const fetchPage = useCallback((page: number) => getBrandFaceFavorites(page, pageSize), []);
  const catalog = usePaginatedCatalog<FavoriteBrandFace>({ active, fetchPage, getItemId: (item) => item.id });
  const refresh = useCallback(() => { void catalog.load(1, false); }, [catalog.load]);

  useInitialFavoriteLoad(active, startedRef, catalog.load, catalog.cancel);
  useProfileDataRefresh(useCallback(() => { if (active) refresh(); }, [active, refresh]));
  useInfiniteFavoritesScroll({ active, failure: catalog.failure, hasMore: catalog.hasMore, load: catalog.load, loadMoreFailed: catalog.loadMoreFailed, loading: catalog.loading, loadingMore: catalog.loadingMore, page: catalog.page, sentinelRef });

  return <section aria-busy={catalog.loading || catalog.loadingMore} aria-live="polite" className="catalog-search__results">
    {catalog.loading ? <SearchSkeleton count={3} /> : catalog.failure ? <CatalogState icon="refresh" onRetry={refresh} subtitle={t("favorites.brandFacesLoadFailedSubtitle")} title={t("favorites.brandFacesLoadFailedTitle")} /> : catalog.items.length === 0 ? <CatalogState icon="bookmark" subtitle={t("favorites.brandFacesEmptySubtitle")} title={t("favorites.brandFacesEmptyTitle")} /> : catalog.items.map((brandFace) => <BrandFaceCard key={brandFace.id} onFavoriteChanged={(isFavorite) => { if (!isFavorite) refresh(); }} profile={brandFace} />)}
    <FavoriteListFooter catalog={catalog} onRetry={() => void catalog.load(catalog.page + 1, true)} sentinelRef={sentinelRef} />
  </section>;
}

function useInitialFavoriteLoad(active: boolean, startedRef: MutableRef<boolean>, load: (page: number, append: boolean) => Promise<void>, cancel: () => void) {
  useEffect(() => {
    if (!active) { cancel(); return; }
    if (!startedRef.current) { startedRef.current = true; void load(1, false); }
    return () => cancel();
  }, [active, cancel, load, startedRef]);
}

function FavoriteListFooter({ catalog, onRetry, sentinelRef }: { catalog: ReturnType<typeof usePaginatedCatalog>; onRetry: () => void; sentinelRef: MutableRef<HTMLDivElement | null> }) {
  const { t } = useI18n();
  if (catalog.loading || catalog.failure || catalog.items.length === 0) return null;
  return <>{catalog.hasMore && <div aria-hidden="true" ref={sentinelRef} />}{catalog.loadingMore && <SearchSkeleton compact count={2} />}{catalog.loadMoreFailed && <CatalogState compact icon="refresh" onRetry={onRetry} subtitle={t("favorites.loadFailedSubtitle")} title={t("favorites.loadFailedTitle")} />}{catalog.loadedInitialResult && !catalog.hasMore && !catalog.loadingMore && !catalog.loadMoreFailed && <p className="catalog-search__end">{t("search.endOfList")}</p>}</>;
}

function useInfiniteFavoritesScroll({ active, failure, hasMore, load, loadMoreFailed, loading, loadingMore, page, sentinelRef }: { active: boolean; failure: string | null; hasMore: boolean; load: (page: number, append: boolean) => Promise<void>; loadMoreFailed: boolean; loading: boolean; loadingMore: boolean; page: number; sentinelRef: MutableRef<HTMLDivElement | null> }) {
  useEffect(() => {
    if (!active || !sentinelRef.current || loading || loadingMore || loadMoreFailed || !hasMore || failure) return;
    const observer = new IntersectionObserver(([entry]) => { if (entry.isIntersecting) void load(page + 1, true); });
    observer.observe(sentinelRef.current);
    return () => observer.disconnect();
  }, [active, failure, hasMore, load, loadMoreFailed, loading, loadingMore, page, sentinelRef]);
}
