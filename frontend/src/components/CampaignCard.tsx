import { formatCurrency } from "../lib/currency";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { Badge, Icon, Price, StatusBadge } from "./ui";

export type CampaignCardData = {
  id: string;
  title: string;
  description: string;
  city?: string | null;
  categories: string[];
  budgetFrom?: number | null;
  budgetTo?: number | null;
  isPromoted: boolean;
  status?: number;
  requirements?: string[];
  deadline?: string | null;
  applicationsCount?: number;
  _count?: { applications: number };
  business?: { name: string };
};

export function CampaignCard({ campaign, variant = "default" }: { campaign: CampaignCardData; variant?: "default" | "home" }) {
  const { language, t } = useI18n();
  const applications = campaign.applicationsCount ?? campaign._count?.applications ?? 0;
  const deadline = campaign.deadline ? new Intl.DateTimeFormat(language === "uz" ? "uz-UZ" : "ru-UZ", { day: "numeric", month: "short" }).format(new Date(campaign.deadline)) : null;
  if (variant === "home") {
    return <a aria-label={t("home.openCampaign", { title: campaign.title })} className="home-campaign-card card-enter block overflow-hidden" href={`#/campaign/${campaign.id}`}>
      <div className="flex min-w-0 items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">{campaign.isPromoted && <span className="home-card__badge">{t("card.promoted")}</span>}<span className="truncate text-xs font-semibold text-[color:var(--bb-text-secondary)]">{campaign.business?.name ?? t("common.business")}</span></div>
          <h3 className="mt-2 line-clamp-2 text-[16px] font-extrabold leading-5 tracking-tight">{campaign.title}</h3>
        </div>
        <span aria-hidden="true" className="home-campaign-card__icon"><Icon name="briefcase" /></span>
      </div>
      <div className="mt-3 flex flex-wrap gap-1.5">{campaign.categories.slice(0, 2).map((category) => <span className="home-card__chip" key={category}>{categoryLabel(category)}</span>)}</div>
      <div className="home-campaign-card__metrics mt-4 flex items-center justify-between gap-3">
        <div><span>{t("common.budget")}</span><strong>{campaign.budgetFrom == null ? t("card.openBudget") : formatCurrency(campaign.budgetFrom)}</strong></div>
        <span className="text-right text-xs font-semibold text-[color:var(--bb-text-secondary)]">{deadline ? t("card.deadline", { date: deadline }) : campaign.city ? cityLabel(campaign.city) : t("card.wholeCountry")}</span>
      </div>
      {applications > 0 && <span className="sr-only">{t("common.applications")}: {applications}</span>}
    </a>;
  }
  return <a className={`card-enter glass-card pressable block overflow-hidden p-4 ${campaign.isPromoted ? "border-amber-200 bg-gradient-to-br from-amber-50 via-white to-white" : ""}`} href={`#/campaign/${campaign.id}`}>
    <div className="flex items-start justify-between gap-3"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2">{campaign.isPromoted && <Badge tone="gold">{t("card.promoted")}</Badge>}<span className="text-xs font-bold text-brand-muted">{campaign.business?.name ?? t("common.business")}</span></div><h3 className="mt-2 text-[17px] font-extrabold leading-5 tracking-tight">{campaign.title}</h3></div><span className="grid h-11 w-11 shrink-0 place-items-center rounded-2xl bg-blue-50 text-brand-blue"><Icon name="briefcase" /></span></div>
    <p className="mt-3 line-clamp-2 text-sm leading-5 text-brand-muted">{campaign.description}</p>
    <div className="mt-3 flex flex-wrap gap-1.5">{campaign.categories.slice(0, 3).map((category) => <Badge key={category} tone="blue">{categoryLabel(category)}</Badge>)}</div>
    <div className="mt-4 grid grid-cols-2 gap-2"><div className="rounded-2xl bg-slate-50 p-3"><span className="block text-[11px] text-brand-muted">{t("common.budget")}</span><Price className="mt-1 block text-[14px]" value={campaign.budgetFrom} /></div><div className="rounded-2xl bg-slate-50 p-3"><span className="block text-[11px] text-brand-muted">{t("common.applications")}</span><span className="mt-1 block text-[14px] font-extrabold">{applications}</span></div></div>
    <div className="mt-3 flex items-center justify-between border-t border-brand-line pt-3 text-xs"><span className="text-brand-muted">{campaign.city ? cityLabel(campaign.city) : t("card.wholeCountry")}</span>{deadline ? <StatusBadge status="warning"><Icon className="h-3.5 w-3.5" name="calendar" />{t("card.deadline", { date: deadline })}</StatusBadge> : <span className="font-semibold text-brand-muted">{campaign.budgetTo ? `${t("common.to")} ${formatCurrency(campaign.budgetTo)}` : t("card.openBudget")}</span>}</div>
  </a>;
}
