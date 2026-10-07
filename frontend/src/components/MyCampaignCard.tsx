import type { MyCampaign } from "../api/marketplace";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatBudgetRange, formatDate } from "../lib/currency";
import { campaignApplicationsLabel, campaignStatusLabel, campaignStatusTone } from "../lib/campaignStatus";
import { CampaignActionsMenu, type CampaignMenuResult } from "./CampaignActionsMenu";
import { Badge, Icon } from "./ui";

export function MyCampaignCard({ campaign, onMenuResult }: { campaign: MyCampaign; onMenuResult?: (result: CampaignMenuResult) => void }) {
  const { language, t } = useI18n();
  const budget = formatBudgetRange(campaign.minBudget, campaign.maxBudget);
  const deadline = campaign.deadline ? formatDate(campaign.deadline) : t("myCampaigns.deadlineNotSpecified");

  // The menu button sits beside the card link, never inside it: a button inside a link is not valid markup.
  return <article className="my-campaign-card card-enter">
    {onMenuResult && <div className="my-campaign-card__menu"><CampaignActionsMenu campaign={campaign} onResult={onMenuResult} /></div>}
    <a aria-label={t("myCampaigns.openAria", { title: campaign.title })} className="my-campaign-card__link" href={`#/my-campaign/${campaign.id}`}>
    <div className="my-campaign-card__header">
      <div className="min-w-0"><h2>{campaign.title}</h2><p>{t("myCampaigns.updated", { date: formatDate(campaign.updatedAtUtc) })}</p></div>
      <Badge tone={campaignStatusTone(campaign.status)}>{campaignStatusLabel(campaign.status, t)}</Badge>
    </div>
    {(campaign.isPromoted || campaign.categories.length > 0) && <div className="my-campaign-card__categories">{campaign.isPromoted && <span className="catalog-card__promoted">{t("card.promoted")}</span>}{campaign.categories.slice(0, 2).map((category) => <span key={category}>{categoryLabel(category, language)}</span>)}</div>}
    <dl className="my-campaign-card__facts">
      <div><dt>{t("common.city")}</dt><dd>{campaign.city ? cityLabel(campaign.city, language) : t("common.notSpecified")}</dd></div>
      <div><dt>{t("campaigns.deadline")}</dt><dd>{deadline}</dd></div>
      <div className="my-campaign-card__budget"><dt>{t("common.budget")}</dt><dd>{budget ?? t("myCampaigns.budgetNotSpecified")}</dd></div>
      <div><dt>{t("myCampaigns.applications")}</dt><dd><Icon aria-hidden="true" name="briefcase" />{campaignApplicationsLabel(campaign.applicationsCount, language, t)}</dd></div>
    </dl>
    </a>
  </article>;
}
