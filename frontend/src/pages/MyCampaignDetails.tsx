import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { closeMyCampaign, getMyCampaign, type MyCampaignDetails as MyCampaignDetailsData } from "../api/marketplace";
import { BottomNav, Badge, Button, Card, ErrorState, Icon, LoadingState, Modal, Toast } from "../components/ui";
import { notifyCampaignDataChanged, useCampaignDataRefresh } from "../hooks/useCampaignDataRefresh";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatBudgetRange, formatDate, isPastDay } from "../lib/currency";
import { campaignApplicationsLabel, campaignStatusLabel, campaignStatusTone } from "../lib/campaignStatus";
import { navigateWithHistoryOrigin } from "../navigation/hashNavigation";
import { getCachedMyCampaign, removeCachedMyCampaign, setCachedMyCampaign, updateCachedMyCampaign } from "../data/myCampaignCache";
import { removeCachedPublicDetail } from "../data/publicDetailCache";
import { PageHeader } from "../components/PageHeader";
import { ChipList, DetailSection, FactGrid } from "../components/details/DetailBlocks";

type DetailState = "not-found" | "denied" | "failed" | null;

export function MyCampaignDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  const [campaign, setCampaign] = useState<MyCampaignDetailsData | null>(() => getCachedMyCampaign(id));
  const [loading, setLoading] = useState(() => !getCachedMyCampaign(id));
  const [failure, setFailure] = useState<DetailState>(null);
  const [closeOpen, setCloseOpen] = useState(false);
  const [closing, setClosing] = useState(false);
  const [toast, setToast] = useState("");
  const requestVersionRef = useRef(0);
  const activeRequestRef = useRef<AbortController>();
  const load = useCallback(() => {
    activeRequestRef.current?.abort();
    const controller = new AbortController();
    activeRequestRef.current = controller;
    const requestVersion = ++requestVersionRef.current;
    const cached = getCachedMyCampaign(id);
    setCampaign(cached);
    setLoading(!cached);
    setFailure(null);
    getMyCampaign(id, controller.signal).then((response) => {
      if (controller.signal.aborted || requestVersion !== requestVersionRef.current) return;
      setCachedMyCampaign(response);
      setCampaign(response);
    }).catch((error: unknown) => {
      if (controller.signal.aborted || requestVersion !== requestVersionRef.current) return;
      const nextFailure = error instanceof ApiError && error.status === 404 ? "not-found" : error instanceof ApiError && (error.status === 401 || error.status === 403) ? "denied" : "failed";
      if (!cached || nextFailure !== "failed") {
        if (nextFailure !== "failed") removeCachedMyCampaign(id);
        setCampaign(null);
        setFailure(nextFailure);
      }
    }).finally(() => { if (!controller.signal.aborted && requestVersion === requestVersionRef.current) setLoading(false); });
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
      updateCachedMyCampaign(id, (current) => ({ ...current, status: 2 }));
      removeCachedPublicDetail("campaign", id);
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

  if (loading && !campaign) return <div className="campaign-management-screen screen screen--with-nav"><LoadingState title={t("myCampaignDetails.loading")} /><BottomNav /></div>;
  if (!campaign) return <div className="campaign-management-screen screen screen--with-nav"><ErrorState onRetry={failure === "failed" ? load : undefined} subtitle={t(failure === "not-found" ? "myCampaignDetails.notFoundSubtitle" : failure === "denied" ? "myCampaignDetails.deniedSubtitle" : "myCampaignDetails.errorSubtitle")} title={t(failure === "not-found" ? "myCampaignDetails.notFoundTitle" : failure === "denied" ? "myCampaignDetails.deniedTitle" : "myCampaignDetails.errorTitle")} /><BottomNav /></div>;

  const budget = formatBudgetRange(campaign.minBudget, campaign.maxBudget);
  const canManage = campaign.status !== 2;
  return <div className="campaign-management-screen my-campaign-details screen screen--with-nav">
    <PageHeader back={{ href: "#/my-campaigns", label: t("myCampaignDetails.backAria") }} />
    <section className="my-campaign-details__hero"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><Badge tone={campaignStatusTone(campaign.status)}>{campaignStatusLabel(campaign.status, t)}</Badge>{campaign.isPromoted && <span className="detail-promoted">{t("card.promoted")}</span>}</div><h1>{campaign.title}</h1><p>{t("myCampaignDetails.updated", { date: formatDate(campaign.updatedAtUtc) })}</p></div></section>
    {campaign.status === 1 && isPastDay(campaign.deadline) && <p className="campaign-details__expired" role="status">{t("myCampaignDetails.expiredNote")}</p>}
    {canManage && <a aria-label={t("applications.openInboxAria", { title: campaign.title })} className="my-campaign-details__inbox" href={`#/my-campaign-applications/${id}`}><span><strong>{t("applications.openInbox")}</strong><span>{campaignApplicationsLabel(campaign.applicationsCount, language, t)}</span></span><Icon className="my-campaign-details__inbox-chevron" name="back" /></a>}
    <FactGrid className="mt-4" facts={[
      { label: t("common.city"), value: campaign.city ? cityLabel(campaign.city, language) : t("common.notSpecified") },
      { label: t("campaigns.deadline"), value: campaign.deadline ? formatDate(campaign.deadline) : t("myCampaigns.deadlineNotSpecified") },
      { label: t("myCampaignDetails.created"), value: formatDate(campaign.createdAtUtc) },
      { label: t("myCampaigns.applications"), value: canManage ? null : campaignApplicationsLabel(campaign.applicationsCount, language, t) },
      { label: t("common.budget"), value: budget ?? t("myCampaigns.budgetNotSpecified"), wide: true }
    ]} />
    <DetailSection title={t("myCampaignDetails.description")}><Card><p className="whitespace-pre-line text-sm leading-6 text-brand-muted">{campaign.description}</p></Card></DetailSection>
    <DetailSection title={t("common.categories")}>{campaign.categories.length ? <ChipList items={campaign.categories.map((category) => categoryLabel(category, language))} /> : <p className="text-sm text-brand-muted">{t("common.notSpecified")}</p>}</DetailSection>
    <DetailSection title={t("common.requirements")}><Card>{campaign.requirements.length ? <ul className="grid gap-2">{campaign.requirements.map((requirement) => <li className="flex gap-2 text-sm text-brand-muted" key={requirement}><Icon aria-hidden="true" className="h-4 w-4 shrink-0 text-brand-success" name="check" />{requirement}</li>)}</ul> : <p className="text-sm text-brand-muted">{t("common.notSpecified")}</p>}</Card></DetailSection>
    {canManage && <section className="my-campaign-details__actions" aria-label={t("myCampaignDetails.actionsAria")}>
      <a aria-label={t("myCampaignDetails.editAria", { title: campaign.title })} className="my-campaign-details__edit" href={`#/my-campaign-edit/${id}`} onClick={(event) => {
        if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        navigateWithHistoryOrigin(window.location.hash, `#/my-campaign-edit/${id}`);
      }}>{t("myCampaignDetails.edit")}</a>
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
