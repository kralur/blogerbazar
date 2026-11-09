import type { MyCampaign } from "../api/marketplace";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatBudgetRange, formatCompactNumber, formatDate, formatNumber } from "../lib/currency";
import { campaignApplicationsLabel, campaignStatusLabel, campaignStatusTone } from "../lib/campaignStatus";
import { CampaignActionsMenu, type CampaignMenuResult } from "./CampaignActionsMenu";
import { Badge, Icon } from "./ui";

export function MyCampaignCard({ campaign, onMenuResult }: { campaign: MyCampaign; onMenuResult?: (result: CampaignMenuResult) => void }) {
  const { language, t } = useI18n();
  // A full range reads as two short figures and the unit (compact millions); one bound keeps the usual wording.
  const figure = (value: number) => value >= 1_000_000 ? formatCompactNumber(value) : formatNumber(value);
  const budget = campaign.minBudget != null && campaign.maxBudget != null && campaign.minBudget !== campaign.maxBudget
    ? `${figure(campaign.minBudget)} – ${figure(campaign.maxBudget)} ${t("currency.uzs")}`
    : formatBudgetRange(campaign.minBudget, campaign.maxBudget);
  const deadline = campaign.deadline ? formatDate(campaign.deadline) : t("myCampaigns.deadlineNotSpecified");

  // The menu button sits beside the card link, never inside it: a button inside a link is not valid markup.
  return <article className="my-campaign-card card-enter">
    {onMenuResult && <div className="my-campaign-card__menu"><CampaignActionsMenu campaign={campaign} onResult={onMenuResult} /></div>}
    <a aria-label={t("myCampaigns.openAria", { title: campaign.title })} className="my-campaign-card__link" href={`#/my-campaign/${campaign.id}`}>
    <div className="my-campaign-card__header">
      <div className="my-campaign-card__budget"><strong>{budget ?? t("myCampaigns.budgetNotSpecified")}</strong></div>
      <Badge tone={campaignStatusTone(campaign.status)}>{campaignStatusLabel(campaign.status, t)}</Badge>
    </div>
    <h2>{campaign.title}</h2>
    <p className="my-campaign-card__meta">{[campaign.city ? cityLabel(campaign.city, language) : null, deadline].filter(Boolean).join(" · ")}</p>
    {(campaign.isPromoted || campaign.categories.length > 0) && <div className="my-campaign-card__categories">{campaign.isPromoted && <span className="catalog-card__promoted">{t("card.promoted")}</span>}{campaign.categories.slice(0, 2).map((category) => <span key={category}>{categoryLabel(category, language)}</span>)}</div>}
    <div className="my-campaign-card__facts">
      <span>{t("myCampaigns.updated", { date: formatDate(campaign.updatedAtUtc) })}</span>
      <span className="my-campaign-card__applications"><Icon aria-hidden="true" name="briefcase" />{campaignApplicationsLabel(campaign.applicationsCount, language, t)}</span>
    </div>
    </a>
  </article>;
}
