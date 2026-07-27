import { formatCurrency } from "../lib/currency";
import { Avatar, Badge, Icon, Rating } from "./ui";

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
  contactUnlocked: boolean;
  avatarUrl?: string | null;
  verified?: boolean;
  averageReach?: number | null;
  engagementRate?: number | null;
  isPromoted?: boolean;
};

function price(blogger: BloggerCardData) {
  if (blogger.priceNote) return blogger.priceNote;
  if (blogger.priceFrom) return formatCurrency(blogger.priceFrom);
  return "По запросу";
}

function Metric({ icon, value, label }: { icon: string; value: string; label: string }) {
  return <div><div className="flex items-center justify-center gap-1 text-[13px] font-extrabold"><Icon className="h-3.5 w-3.5 text-brand-muted" name={icon} />{value}</div><div className="mt-0.5 text-[10px] text-brand-muted">{label}</div></div>;
}

export function BloggerCard({ blogger }: { blogger: BloggerCardData }) {
  return (
    <a className="glass-card block p-4 transition active:scale-[0.99]" href={`#/blogger/${blogger.id}`}>
      <div className="flex gap-3">
        <Avatar name={blogger.name} size="sm" src={blogger.avatarUrl} verified={blogger.verified} />
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2"><strong className="truncate text-[15px]">{blogger.name}</strong>{blogger.isPromoted && <Badge tone="gold">PRO</Badge>}</div>
          <p className="mt-0.5 truncate text-[13px] text-brand-muted">{blogger.categories.slice(0, 2).join(" · ")} · {blogger.city}</p>
          <div className="mt-3 grid grid-cols-3 gap-2 text-center">
            <Metric icon="users" value={`${Math.round(blogger.totalFollowers / 1000)}K`} label="аудитория" />
            <Metric icon="chart" value={`${blogger.engagementRate ?? "—"}%`} label="ER" />
            <Metric icon="search" value={blogger.averageReach ? `${Math.round(blogger.averageReach / 1000)}K` : "—"} label="охват" />
          </div>
        </div>
        <Icon className="h-5 w-5 text-brand-muted" name="bookmark" />
      </div>
      <div className="mt-4 flex items-center justify-between gap-3 border-t border-brand-line pt-3">
        <Rating count={blogger.reviewsCount} value={blogger.rating} />
        <strong className="text-[14px] text-brand-ink">{price(blogger)}</strong>
      </div>
    </a>
  );
}
