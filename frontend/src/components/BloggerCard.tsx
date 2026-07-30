import { formatCurrency, formatNumber } from "../lib/currency";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { Avatar, Badge, Icon, Price, Rating } from "./ui";

export type BloggerCardData = {
  id: string;
  name: string;
  city: string;
  categories: string[];
  totalFollowers: number;
  priceFrom?: number | null;
  priceTo?: number | null;
  priceNote?: string | null;
  rating?: number | null;
  reviewsCount: number;
  completedDealsCount: number;
  avatarUrl?: string | null;
  verified?: boolean;
  averageReach?: number | null;
  engagementRate?: number | null;
  storiesPrice?: number | null;
  reelsPrice?: number | null;
  platform?: string | null;
  isPromoted?: boolean;
};

function Metric({ icon, value, label }: { icon: string; value: string; label: string }) {
  return <div className="rounded-2xl bg-slate-50 px-2 py-2 text-center"><div className="flex items-center justify-center gap-1 text-[13px] font-extrabold"><Icon className="h-3.5 w-3.5 text-brand-muted" name={icon} />{value}</div><div className="mt-0.5 text-[10px] text-brand-muted">{label}</div></div>;
}

export function BloggerCard({ blogger }: { blogger: BloggerCardData }) {
  const { t } = useI18n();
  const primaryPrice = blogger.storiesPrice ?? blogger.priceFrom;
  return <a className="glass-card pressable block overflow-hidden p-4" href={`#/blogger/${blogger.id}`}>
    <div className="flex gap-3">
      <Avatar name={blogger.name} size="sm" src={blogger.avatarUrl} verified={blogger.verified} />
      <div className="min-w-0 flex-1"><div className="flex items-center gap-2"><strong className="truncate text-[16px] tracking-tight">{blogger.name}</strong>{blogger.isPromoted && <Badge tone="gold">{t("card.promoted")}</Badge>}</div><p className="mt-0.5 truncate text-[13px] text-brand-muted">{cityLabel(blogger.city)}{blogger.platform ? ` · ${blogger.platform}` : ""}</p><div className="mt-2 flex flex-wrap gap-1.5">{blogger.categories.slice(0, 2).map((category) => <Badge key={category} tone="blue">{categoryLabel(category)}</Badge>)}</div></div>
      <span aria-label={t("card.bookmark")} className="grid h-9 w-9 shrink-0 place-items-center rounded-2xl bg-slate-50 text-brand-muted"><Icon name="bookmark" /></span>
    </div>
    <div className="mt-4 grid grid-cols-3 gap-2"><Metric icon="users" label={t("common.followers")} value={formatNumber(blogger.totalFollowers)} /><Metric icon="chart" label="ER" value={blogger.engagementRate == null ? "—" : `${blogger.engagementRate}%`} /><Metric icon="search" label={t("common.reach")} value={formatNumber(blogger.averageReach)} /></div>
    <div className="mt-4 flex items-center justify-between gap-3 border-t border-brand-line pt-3"><Rating count={blogger.reviewsCount} value={blogger.rating} /><span className="text-xs font-semibold text-brand-muted">{t("common.deals", { count: blogger.completedDealsCount })}</span></div>
    <div className="mt-3 grid grid-cols-2 gap-2"><div className="rounded-2xl border border-brand-line px-3 py-2"><span className="block text-[11px] text-brand-muted">{t("card.stories")}</span><Price className="mt-0.5 block text-[13px]" value={primaryPrice} /></div><div className="rounded-2xl border border-brand-line px-3 py-2"><span className="block text-[11px] text-brand-muted">{t("card.reels")}</span><span className="mt-0.5 block text-[13px] font-extrabold tracking-tight">{blogger.reelsPrice ? formatCurrency(blogger.reelsPrice) : t("card.onRequest")}</span></div></div>
  </a>;
}
