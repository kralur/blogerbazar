import type { MyCampaign } from "../api/marketplace";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatBudgetRange, formatCompactNumber, formatDate, formatNumber, formatShortDate } from "../lib/currency";
import { campaignApplicationsLabel, campaignStatusLabel, campaignStatusTone } from "../lib/campaignStatus";
import { CampaignActionsMenu, type CampaignMenuResult } from "./CampaignActionsMenu";
import { Badge, Icon } from "./ui";
import { useIsDesktop } from "../lib/desktopLayout";
import { compactBudgetRange } from "./details/DetailBlocks";
import { formatScheduled } from "../lib/campaignSchedule";

export function MyCampaignCard({ campaign, onMenuResult }: { campaign: MyCampaign; onMenuResult?: (result: CampaignMenuResult) => void }) {
  const { language, t } = useI18n();
  // A full range reads as two short figures and the unit (compact millions); one bound keeps the usual wording.
  const figure = (value: number) => value >= 1_000_000 ? formatCompactNumber(value) : formatNumber(value);
  const budget = campaign.minBudget != null && campaign.maxBudget != null && campaign.minBudget !== campaign.maxBudget
    ? `${figure(campaign.minBudget)} – ${figure(campaign.maxBudget)} ${t("currency.uzs")}`
    : formatBudgetRange(campaign.minBudget, campaign.maxBudget);
  const deadline = campaign.deadline ? formatDate(campaign.deadline) : t("myCampaigns.deadlineNotSpecified");
  // D69: a planned draft or closed campaign says when it goes out instead of when it was last edited.
  const scheduled = campaign.status === 0 || campaign.status === 2 ? campaign.scheduledPublishAtUtc ?? null : null;
  const statusLabel = scheduled ? t("schedule.badge") : campaignStatusLabel(campaign.status, t);
  const scheduledLabel = scheduled ? t("schedule.planned", { date: formatScheduled(scheduled, language) }) : null;

  const desktop = useIsDesktop();
  // On a computer (the website) the card is the catalog's campaign tile, so My campaigns and Campaigns look alike:
  // the state and the title on top, the applications in a line, then city, budget and deadline at the bottom.
  if (desktop) {
    const shortBudget = compactBudgetRange(campaign.minBudget, campaign.maxBudget, t("currency.uzs"));
    return <article className="catalog-tile my-campaign-tile card-enter">
      <a aria-label={t("myCampaigns.openAria", { title: campaign.title })} className="campaign-catalog-card" href={`#/my-campaign/${campaign.id}`}>
        <div className="campaign-catalog-card__identity">
          <div className="campaign-catalog-card__heading">
            <div className="campaign-catalog-card__name-row"><Badge tone={campaignStatusTone(campaign.status)}>{statusLabel}</Badge></div>
            <h2>{campaign.title}</h2>
          </div>
        </div>
        {(campaign.isPromoted || campaign.categories.length > 0) && <div className="campaign-catalog-card__categories">{campaign.isPromoted && <span className="catalog-card__promoted">{t("card.promoted")}</span>}{campaign.categories.slice(0, 2).map((category) => <span key={category}>{categoryLabel(category, language)}</span>)}</div>}
        <p className="campaign-catalog-card__requirements">
          <span className="catalog-tile__fact my-campaign-card__applications"><Icon aria-hidden="true" name="briefcase" />{campaignApplicationsLabel(campaign.applicationsCount, language, t)}</span>
          <span className="catalog-tile__fact">{scheduledLabel ?? t("myCampaigns.updated", { date: formatShortDate(campaign.updatedAtUtc, language) })}</span>
        </p>
        <dl className="campaign-catalog-card__facts">
          {campaign.city && <div><dt>{t("common.city")}</dt><dd>{cityLabel(campaign.city, language)}</dd></div>}
          <div className="campaign-catalog-card__fact--budget"><dt>{t("common.budget")}</dt><dd>{shortBudget ?? t("myCampaigns.budgetNotSpecified")}</dd></div>
          {campaign.deadline && <div><dt>{t("campaigns.deadline")}</dt><dd>{formatShortDate(campaign.deadline, language)}</dd></div>}
        </dl>
      </a>
      {onMenuResult && <div className="catalog-tile__save"><CampaignActionsMenu campaign={campaign} onResult={onMenuResult} /></div>}
    </article>;
  }

  // The menu button sits beside the card link, never inside it: a button inside a link is not valid markup.
  return <article className="my-campaign-card card-enter">
    {onMenuResult && <div className="my-campaign-card__menu"><CampaignActionsMenu campaign={campaign} onResult={onMenuResult} /></div>}
    <a aria-label={t("myCampaigns.openAria", { title: campaign.title })} className="my-campaign-card__link" href={`#/my-campaign/${campaign.id}`}>
    <div className="my-campaign-card__header">
      <div className="my-campaign-card__budget"><strong>{budget ?? t("myCampaigns.budgetNotSpecified")}</strong></div>
      <Badge tone={campaignStatusTone(campaign.status)}>{statusLabel}</Badge>
    </div>
    <h2>{campaign.title}</h2>
    <p className="my-campaign-card__meta">{[campaign.city ? cityLabel(campaign.city, language) : null, deadline].filter(Boolean).join(" · ")}</p>
    {(campaign.isPromoted || campaign.categories.length > 0) && <div className="my-campaign-card__categories">{campaign.isPromoted && <span className="catalog-card__promoted">{t("card.promoted")}</span>}{campaign.categories.slice(0, 2).map((category) => <span key={category}>{categoryLabel(category, language)}</span>)}</div>}
    <div className="my-campaign-card__facts">
      <span>{scheduledLabel ?? t("myCampaigns.updated", { date: formatDate(campaign.updatedAtUtc) })}</span>
      <span className="my-campaign-card__applications"><Icon aria-hidden="true" name="briefcase" />{campaignApplicationsLabel(campaign.applicationsCount, language, t)}</span>
    </div>
    </a>
  </article>;
}
