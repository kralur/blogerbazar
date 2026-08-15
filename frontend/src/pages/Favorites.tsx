import { useCallback, useEffect, useState } from "react";
import { getFavorites, type FavoriteBlogger } from "../api/marketplace";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatNumber } from "../lib/currency";
import { Avatar, BottomNav, Card, EmptyState, ErrorState, Icon, LoadingState } from "../components/ui";
import { FavoriteButton } from "../components/FavoriteButton";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";

export function Favorites() {
  const { language, t } = useI18n();
  useScrollRestoration("favorites");
  const [items, setItems] = useState<FavoriteBlogger[]>([]);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);

  const load = useCallback(() => {
    setLoading(true);
    setFailed(false);
    getFavorites().then((response) => setItems(response.items)).catch(() => setFailed(true)).finally(() => setLoading(false));
  }, []);

  useEffect(load, []);
  useProfileDataRefresh(load);

  return <div className="screen screen--with-nav">
    <header className="flex items-center gap-3"><a aria-label={t("favorites.backAria")} className="grid h-11 w-11 place-items-center rounded-2xl bg-white shadow-card" href="#/profile"><Icon name="back" /></a><div className="min-w-0 flex-1"><p className="text-sm font-semibold text-brand-muted">{t("profile.eyebrow")}</p><h1 className="mt-1 text-3xl font-extrabold tracking-tight">{t("favorites.title")}</h1></div><LanguageSwitcher /></header>
    <div className="mt-5">
      {loading ? <LoadingState title={t("favorites.loading")} /> : failed ? <ErrorState onRetry={load} subtitle={t("favorites.loadFailedSubtitle")} title={t("favorites.loadFailedTitle")} /> : items.length === 0 ? <EmptyState icon="bookmark" subtitle={t("favorites.emptySubtitle")} title={t("favorites.emptyTitle")} /> : <div className="grid gap-3">{items.map((blogger) => <Card className="relative p-3" key={blogger.bloggerId}><a className="flex min-w-0 items-center gap-3 pr-10" href={`#/blogger/${blogger.bloggerId}`}><Avatar name={blogger.name} size="sm" src={blogger.avatarUrl} /><span className="min-w-0 flex-1"><strong className="block truncate">{blogger.name}</strong><span className="mt-1 block truncate text-sm text-brand-muted">{cityLabel(blogger.city, language)} · {blogger.categories.map((category) => categoryLabel(category, language)).join(", ")}</span><span className="mt-1 block text-xs font-semibold text-brand-muted">{formatNumber(blogger.totalFollowers)} {t("common.followers").toLowerCase()}</span></span></a><FavoriteButton bloggerId={blogger.bloggerId} className="absolute right-3 top-1/2 -translate-y-1/2" onChanged={(saved) => { if (!saved) setItems((current) => current.filter((item) => item.bloggerId !== blogger.bloggerId)); }} /></Card>)}</div>}
    </div>
    <BottomNav />
  </div>;
}
