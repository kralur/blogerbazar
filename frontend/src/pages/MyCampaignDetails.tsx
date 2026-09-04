import { useCallback, useEffect, useState } from "react";
import { ApiError } from "../api/client";
import { getMyCampaign, type MyCampaignDetails as MyCampaignDetailsData } from "../api/marketplace";
import { BottomNav, Badge, Card, ErrorState, Icon, LoadingState } from "../components/ui";
import { ManagementBackLink } from "../components/ManagementBackLink";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatDate, formatNumber } from "../lib/currency";
import { campaignApplicationsLabel, campaignStatusLabel, campaignStatusTone } from "../lib/campaignStatus";

type DetailState = "not-found" | "denied" | "failed" | null;

export function MyCampaignDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  const [campaign, setCampaign] = useState<MyCampaignDetailsData | null>(null);
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<DetailState>(null);
  const load = useCallback(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    getMyCampaign(id, controller.signal).then(setCampaign).catch((error: unknown) => {
      if (controller.signal.aborted) return;
      setCampaign(null);
      setFailure(error instanceof ApiError && error.status === 404 ? "not-found" : error instanceof ApiError && (error.status === 401 || error.status === 403) ? "denied" : "failed");
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [id]);
  useEffect(() => load(), [load]);

  if (loading) return <div className="campaign-management-screen screen screen--with-nav"><LoadingState title={t("myCampaignDetails.loading")} /><BottomNav /></div>;
  if (failure || !campaign) return <div className="campaign-management-screen screen screen--with-nav"><ErrorState onRetry={failure === "failed" ? load : undefined} subtitle={t(failure === "not-found" ? "myCampaignDetails.notFoundSubtitle" : failure === "denied" ? "myCampaignDetails.deniedSubtitle" : "myCampaignDetails.errorSubtitle")} title={t(failure === "not-found" ? "myCampaignDetails.notFoundTitle" : failure === "denied" ? "myCampaignDetails.deniedTitle" : "myCampaignDetails.errorTitle")} /><BottomNav /></div>;

  const budget = formatBudget(campaign.minBudget, campaign.maxBudget, t);
  return <div className="campaign-management-screen my-campaign-details screen screen--with-nav">
    <header className="my-campaign-details__header"><ManagementBackLink ariaLabel={t("myCampaignDetails.backAria")} href="#/my-campaigns" /><LanguageSwitcher /></header>
    <section className="my-campaign-details__hero"><div className="min-w-0"><Badge tone={campaignStatusTone(campaign.status)}>{campaignStatusLabel(campaign.status, t)}</Badge><h1>{campaign.title}</h1><p>{t("myCampaignDetails.updated", { date: formatDate(campaign.updatedAtUtc) })}</p></div>{campaign.isPromoted && <span className="my-campaign-card__promoted">{t("card.promoted")}</span>}</section>
    <Card className="my-campaign-details__section"><h2>{t("myCampaignDetails.description")}</h2><p>{campaign.description}</p></Card>
    <section className="my-campaign-details__facts">
      <DetailFact label={t("common.city")} value={campaign.city ? cityLabel(campaign.city, language) : t("common.notSpecified")} />
      <DetailFact label={t("common.budget")} value={budget ?? t("myCampaigns.budgetNotSpecified")} />
      <DetailFact label={t("campaigns.deadline")} value={campaign.deadline ? formatDate(campaign.deadline) : t("myCampaigns.deadlineNotSpecified")} />
      <DetailFact label={t("myCampaigns.applications")} value={campaignApplicationsLabel(campaign.applicationsCount, language, t)} />
      <DetailFact label={t("myCampaignDetails.created")} value={formatDate(campaign.createdAtUtc)} />
    </section>
    <Card className="my-campaign-details__section"><h2>{t("common.categories")}</h2>{campaign.categories.length ? <div className="my-campaign-card__categories">{campaign.categories.map((category) => <span key={category}>{categoryLabel(category, language)}</span>)}</div> : <p>{t("common.notSpecified")}</p>}</Card>
    <Card className="my-campaign-details__section"><h2>{t("common.requirements")}</h2>{campaign.requirements.length ? <ul>{campaign.requirements.map((requirement) => <li key={requirement}><Icon aria-hidden="true" name="check" />{requirement}</li>)}</ul> : <p>{t("common.notSpecified")}</p>}</Card>
    <BottomNav />
  </div>;
}

function DetailFact({ label, value }: { label: string; value: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>;
}

function formatBudget(min: number | null | undefined, max: number | null | undefined, t: (key: string, values?: Record<string, string | number>) => string) {
  if (min != null && max != null) return t("campaigns.budgetRange", { min: formatNumber(min), max: formatNumber(max) });
  if (min != null) return t("campaigns.budgetFromValue", { min: formatNumber(min) });
  if (max != null) return t("campaigns.budgetToValue", { max: formatNumber(max) });
  return null;
}
