import { formatCurrency } from "../lib/currency";
import { Badge, Icon } from "./ui";

export type CampaignCardData = {
  id: string;
  title: string;
  description: string;
  city?: string | null;
  categories: string[];
  budgetFrom?: number | null;
  budgetTo?: number | null;
  isPromoted: boolean;
  _count?: { applications: number };
  business?: { name: string };
};

export function CampaignCard({ campaign }: { campaign: CampaignCardData }) {
  return <a className={`glass-card block p-4 ${campaign.isPromoted ? "border-amber-200 bg-gradient-to-br from-amber-50 via-white to-white" : ""}`} href={`#/campaign/${campaign.id}`}>
    <div className="flex items-start justify-between gap-3"><div className="min-w-0"><div className="flex items-center gap-2">{campaign.isPromoted && <Badge tone="gold">Продвигается</Badge>}<span className="text-xs font-bold text-brand-muted">{campaign.business?.name ?? "Бизнес"}</span></div><h3 className="mt-2 text-[17px] font-extrabold leading-5">{campaign.title}</h3></div><span className="grid h-10 w-10 shrink-0 place-items-center rounded-2xl bg-blue-50 text-brand-blue"><Icon name="briefcase" /></span></div>
    <p className="mt-3 line-clamp-2 text-sm leading-5 text-brand-muted">{campaign.description}</p>
    <div className="mt-3 flex flex-wrap gap-2">{campaign.categories.slice(0, 3).map((category) => <Badge key={category} tone="blue">{category}</Badge>)}</div>
    <div className="mt-4 flex items-center justify-between border-t border-brand-line pt-3 text-[13px]"><span className="text-brand-muted">{campaign.city ?? "Любой город"} · {campaign._count?.applications ?? 0} откликов</span><strong>{campaign.budgetFrom ? `${formatCurrency(campaign.budgetFrom)}+` : "Бюджет открыт"}</strong></div>
  </a>;
}
