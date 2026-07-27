import { useEffect, useState } from "react";
import { getBloggers } from "../api/marketplace";
import { BloggerCard, type BloggerCardData } from "../components/BloggerCard";
import { BottomNav, Chip, Icon, PaywallCard, SearchBar, Skeleton } from "../components/ui";
import { useI18n } from "../i18n";

export function BloggerSearch() {
  const { t } = useI18n();
  const [bloggers, setBloggers] = useState<BloggerCardData[]>([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => { getBloggers().then(setBloggers).catch(() => undefined).finally(() => setLoading(false)); }, []);
  return <div className="screen pb-28"><header className="flex items-center justify-between pt-2"><h1 className="text-2xl font-extrabold tracking-tight">{t("search.title")}</h1><button aria-label={t("search.title")} className="grid h-11 w-11 place-items-center rounded-2xl bg-white shadow-card" type="button"><Icon name="filter" /></button></header><div className="mt-4"><SearchBar placeholder={t("search.placeholder")} /></div><div className="no-scrollbar -mx-5 mt-3 flex gap-2 overflow-x-auto px-5 pb-1"><Chip active>{t("search.all")}</Chip><Chip>{t("search.category")}⌄</Chip><Chip>{t("search.city")}⌄</Chip><Chip>{t("search.price")}⌄</Chip><Chip>{t("search.followers")}⌄</Chip></div><div className="mt-4 grid gap-3">{loading ? [1, 2, 3].map((item) => <Skeleton className="h-44" key={item} />) : bloggers.map((blogger) => <BloggerCard blogger={blogger} key={blogger.id} />)}</div><div className="mt-5"><PaywallCard cta={t("search.proCta")} subtitle={t("search.proSubtitle")} title={t("search.proTitle")} /></div><BottomNav /></div>;
}
