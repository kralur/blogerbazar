import { formatShortDate, formatBudgetRange } from "../lib/currency";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { Avatar } from "./ui";

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
  const deadline = campaign.deadline ? formatShortDate(campaign.deadline, language) : null;
  const budget = formatBudgetRange(campaign.budgetFrom, campaign.budgetTo);

  // One campaign card for Home rails and the catalog; rails hide requirements to keep a fixed height.
  const requirements = variant === "home" ? [] : campaign.requirements?.filter(Boolean).slice(0, 2) ?? [];
  return <a aria-label={t("campaigns.openCampaignAria", { title: campaign.title })} className={`campaign-catalog-card card-enter${variant === "home" ? " catalog-card--rail" : ""}`} href={`#/campaign/${campaign.id}`}>
    <div className="campaign-catalog-card__identity">
      <Avatar name={businessName} size="sm" src={businessAvatarUrl} variant="neutral" />
      <div className="campaign-catalog-card__heading">
        <div className="campaign-catalog-card__name-row"><strong>{businessName}</strong></div>
        <h2>{campaign.title}</h2>
      </div>
    </div>
    {(campaign.isPromoted || campaign.categories.length > 0) && <div className="campaign-catalog-card__categories">{campaign.isPromoted && <span className="catalog-card__promoted">{t("card.promoted")}</span>}{campaign.categories.slice(0, 2).map((category) => <span key={category}>{categoryLabel(category)}</span>)}</div>}
    {requirements.length > 0 && <p className="campaign-catalog-card__requirements">{requirements.join(" · ")}</p>}
    <dl className="campaign-catalog-card__facts">
      {campaign.city && <div><dt>{t("common.city")}</dt><dd>{cityLabel(campaign.city)}</dd></div>}
      {budget && <div className="campaign-catalog-card__fact--budget"><dt>{t("common.budget")}</dt><dd>{budget}</dd></div>}
      {deadline && <div><dt>{t("campaigns.deadline")}</dt><dd>{deadline}</dd></div>}
    </dl>
  </a>;
}
