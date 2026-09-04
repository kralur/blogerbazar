import { useCallback, useEffect, useState } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { closeMyCampaign, getMyCampaign, type MyCampaignDetails as MyCampaignDetailsData } from "../api/marketplace";
import { BottomNav, Badge, Button, Card, ErrorState, Icon, LoadingState, Modal, Toast } from "../components/ui";
import { notifyCampaignDataChanged, useCampaignDataRefresh } from "../hooks/useCampaignDataRefresh";
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
  const [closeOpen, setCloseOpen] = useState(false);
  const [closing, setClosing] = useState(false);
  const [toast, setToast] = useState("");
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
  useCampaignDataRefresh(() => { void load(); });

  useEffect(() => {
    const feedback = sessionStorage.getItem(`bloggerbazar.my-campaign-feedback:${id}`);
    if (!feedback || !campaign) return;
    sessionStorage.removeItem(`bloggerbazar.my-campaign-feedback:${id}`);
    setToast(t(feedback === "saved" ? "myCampaignDetails.saved" : "myCampaignDetails.conflict"));
  }, [campaign, id, t]);

  const closeCampaign = async () => {
    if (closing) return;
    setClosing(true);
    try {
      await closeMyCampaign(id);
      setCampaign((current) => current ? { ...current, status: 2 } : current);
      setCloseOpen(false);
      setToast(t("myCampaignDetails.closed"));
      notifyCampaignDataChanged();
    } catch (error) {
      if (error instanceof ApiError && (error.status === 401 || error.status === 403 || error.status === 404)) {
        setCloseOpen(false);
        setFailure(error.status === 404 ? "not-found" : "denied");
        setCampaign(null);
      } else if (error instanceof ApiError && error.status === 409) {
        setCloseOpen(false);
        setToast(t("myCampaignDetails.conflict"));
        void load();
      } else {
        setToast(getApiErrorMessage(error, t("myCampaignDetails.closeFailed")));
      }
    } finally {
      setClosing(false);
    }
  };

  if (loading) return <div className="campaign-management-screen screen screen--with-nav"><LoadingState title={t("myCampaignDetails.loading")} /><BottomNav /></div>;
  if (failure || !campaign) return <div className="campaign-management-screen screen screen--with-nav"><ErrorState onRetry={failure === "failed" ? load : undefined} subtitle={t(failure === "not-found" ? "myCampaignDetails.notFoundSubtitle" : failure === "denied" ? "myCampaignDetails.deniedSubtitle" : "myCampaignDetails.errorSubtitle")} title={t(failure === "not-found" ? "myCampaignDetails.notFoundTitle" : failure === "denied" ? "myCampaignDetails.deniedTitle" : "myCampaignDetails.errorTitle")} /><BottomNav /></div>;

  const budget = formatBudget(campaign.minBudget, campaign.maxBudget, t);
  const canManage = campaign.status !== 2;
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
    {canManage && <section className="my-campaign-details__actions" aria-label={t("myCampaignDetails.actionsAria")}>
      <a aria-label={t("myCampaignDetails.editAria", { title: campaign.title })} className="my-campaign-details__edit" href={`#/my-campaign-edit/${id}`}>{t("myCampaignDetails.edit")}</a>
      <Button aria-label={t("myCampaignDetails.closeAria", { title: campaign.title })} onClick={() => setCloseOpen(true)} type="button" variant="danger">{t("myCampaignDetails.close")}</Button>
    </section>}
    {campaign.status === 2 && <p className="my-campaign-details__closed-hint">{t("myCampaignDetails.closedHint")}</p>}
    <Modal onClose={() => { if (!closing) setCloseOpen(false); }} open={closeOpen} title={t("myCampaignDetails.closeTitle")} variant="neutral">
      <p className="text-sm leading-6 text-brand-muted">{t("myCampaignDetails.closeDescription")}</p>
      <div className="mt-5 grid grid-cols-2 gap-3"><Button disabled={closing} onClick={() => setCloseOpen(false)} type="button" variant="secondary">{t("common.cancel")}</Button><Button aria-busy={closing} disabled={closing} onClick={closeCampaign} type="button" variant="danger">{closing ? t("myCampaignDetails.closing") : t("myCampaignDetails.close")}</Button></div>
    </Modal>
    <Toast message={toast} tone="success" />
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
