import { formatCurrency } from "../lib/currency";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { Avatar, Icon } from "./ui";

export type CampaignCardData = {
  id: string;
  title: string;
  description?: string | null;
  city?: string | null;
  categories: string[];
  budgetFrom?: number | null;
  budgetTo?: number | null;
  isPromoted: boolean;
  status?: number;
  requirements?: string[] | null;
  deadline?: string | null;
  createdAtUtc?: string;
  applicationsCount?: number;
  _count?: { applications: number };
  business?: { name: string; avatarUrl?: string | null };
  businessName?: string;
  businessAvatarUrl?: string | null;
};

export function CampaignCard({ campaign, variant = "default" }: { campaign: CampaignCardData; variant?: "default" | "home" }) {
  const { language, t } = useI18n();
  const businessName = campaign.businessName ?? campaign.business?.name ?? t("common.business");
  const businessAvatarUrl = campaign.businessAvatarUrl ?? campaign.business?.avatarUrl;
  const deadline = campaign.deadline ? new Intl.DateTimeFormat(language === "uz" ? "uz-UZ" : "ru-UZ", { day: "numeric", month: "short" }).format(new Date(campaign.deadline)) : null;
  const budget = formatBudget(campaign.budgetFrom, campaign.budgetTo, language, t);

  if (variant === "home") {
    return <a aria-label={t("home.openCampaign", { title: campaign.title })} className="home-campaign-card card-enter block overflow-hidden" href={`#/campaign/${campaign.id}`}>
      <div className="flex min-w-0 items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">{campaign.isPromoted && <span className="home-card__badge">{t("card.promoted")}</span>}<span className="truncate text-xs font-semibold text-[color:var(--bb-text-secondary)]">{businessName}</span></div>
          <h3 className="mt-2 line-clamp-2 text-[16px] font-extrabold leading-5 tracking-tight">{campaign.title}</h3>
        </div>
        <span aria-hidden="true" className="home-campaign-card__icon"><Icon name="briefcase" /></span>
      </div>
      <div className="mt-3 flex flex-wrap gap-1.5">{campaign.categories.slice(0, 2).map((category) => <span className="home-card__chip" key={category}>{categoryLabel(category)}</span>)}</div>
      <div className="home-campaign-card__metrics mt-4 flex items-center justify-between gap-3">
        <div><span>{t("common.budget")}</span><strong>{campaign.budgetFrom == null ? t("card.openBudget") : formatCurrency(campaign.budgetFrom)}</strong></div>
        <span className="text-right text-xs font-semibold text-[color:var(--bb-text-secondary)]">{deadline ? t("card.deadline", { date: deadline }) : campaign.city ? cityLabel(campaign.city) : t("card.wholeCountry")}</span>
      </div>
    </a>;
  }

  const requirements = campaign.requirements?.filter(Boolean).slice(0, 2) ?? [];
  return <a aria-label={t("campaigns.openCampaignAria", { title: campaign.title })} className="campaign-catalog-card card-enter" href={`#/campaign/${campaign.id}`}>
    <div className="campaign-catalog-card__identity">
      <Avatar name={businessName} size="sm" src={businessAvatarUrl} variant="neutral" />
      <div className="campaign-catalog-card__heading">
        <div className="campaign-catalog-card__name-row"><strong>{businessName}</strong>{campaign.isPromoted && <span className="campaign-catalog-card__promoted">{t("card.promoted")}</span>}</div>
        <h2>{campaign.title}</h2>
      </div>
    </div>
    {campaign.categories.length > 0 && <div className="campaign-catalog-card__categories">{campaign.categories.slice(0, 2).map((category) => <span key={category}>{categoryLabel(category)}</span>)}</div>}
    {requirements.length > 0 && <p className="campaign-catalog-card__requirements">{requirements.join(" · ")}</p>}
    <dl className="campaign-catalog-card__facts">
      {campaign.city && <div><dt>{t("common.city")}</dt><dd>{cityLabel(campaign.city)}</dd></div>}
      {budget && <div className="campaign-catalog-card__fact--budget"><dt>{t("common.budget")}</dt><dd>{budget}</dd></div>}
      {deadline && <div><dt>{t("campaigns.deadline")}</dt><dd>{deadline}</dd></div>}
    </dl>
  </a>;
}

function formatBudget(min: number | null | undefined, max: number | null | undefined, language: "ru" | "uz", t: (key: string, values?: Record<string, string | number>) => string) {
  const format = (value: number) => new Intl.NumberFormat(language === "uz" ? "uz-UZ" : "ru-RU").format(value);
  if (min != null && max != null) return t("campaigns.budgetRange", { min: format(min), max: format(max) });
  if (min != null) return t("campaigns.budgetFromValue", { min: format(min) });
  if (max != null) return t("campaigns.budgetToValue", { max: format(max) });
  return null;
}
