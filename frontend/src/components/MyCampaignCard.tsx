import type { MyCampaign } from "../api/marketplace";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatDate, formatNumber } from "../lib/currency";
import { campaignApplicationsLabel, campaignStatusLabel, campaignStatusTone } from "../lib/campaignStatus";
import { Badge, Icon } from "./ui";

export function MyCampaignCard({ campaign }: { campaign: MyCampaign }) {
  const { language, t } = useI18n();
  const budget = formatBudget(campaign.minBudget, campaign.maxBudget, t);
  const deadline = campaign.deadline ? formatDate(campaign.deadline) : t("myCampaigns.deadlineNotSpecified");

  return <a aria-label={t("myCampaigns.openAria", { title: campaign.title })} className="my-campaign-card card-enter" href={`#/my-campaign/${campaign.id}`}>
    <div className="my-campaign-card__header">
      <div className="min-w-0"><h2>{campaign.title}</h2><p>{t("myCampaigns.updated", { date: formatDate(campaign.updatedAtUtc) })}</p></div>
      <Badge tone={campaignStatusTone(campaign.status)}>{campaignStatusLabel(campaign.status, t)}</Badge>
    </div>
    {campaign.categories.length > 0 && <div className="my-campaign-card__categories">{campaign.categories.slice(0, 2).map((category) => <span key={category}>{categoryLabel(category, language)}</span>)}</div>}
    <dl className="my-campaign-card__facts">
      <div><dt>{t("common.city")}</dt><dd>{campaign.city ? cityLabel(campaign.city, language) : t("common.notSpecified")}</dd></div>
      <div><dt>{t("campaigns.deadline")}</dt><dd>{deadline}</dd></div>
      <div className="my-campaign-card__budget"><dt>{t("common.budget")}</dt><dd>{budget ?? t("myCampaigns.budgetNotSpecified")}</dd></div>
      <div><dt>{t("myCampaigns.applications")}</dt><dd><Icon aria-hidden="true" name="briefcase" />{campaignApplicationsLabel(campaign.applicationsCount, language, t)}</dd></div>
    </dl>
    {campaign.isPromoted && <span className="my-campaign-card__promoted">{t("card.promoted")}</span>}
  </a>;
}

function formatBudget(min: number | null | undefined, max: number | null | undefined, t: (key: string, values?: Record<string, string | number>) => string) {
  if (min != null && max != null) return t("campaigns.budgetRange", { min: formatNumber(min), max: formatNumber(max) });
  if (min != null) return t("campaigns.budgetFromValue", { min: formatNumber(min) });
  if (max != null) return t("campaigns.budgetToValue", { max: formatNumber(max) });
  return null;
}
