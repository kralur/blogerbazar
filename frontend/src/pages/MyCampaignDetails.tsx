import { useBusinessCanOperate } from "../hooks/useBusinessCanOperate";
import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { closeMyCampaign, getMyCampaign, reopenMyCampaign, type MyCampaignDetails as MyCampaignDetailsData } from "../api/marketplace";
import { BottomNav, Badge, Button, Card, ErrorState, Icon, LoadingState, Modal, Toast } from "../components/ui";
import { notifyCampaignDataChanged, useCampaignDataRefresh } from "../hooks/useCampaignDataRefresh";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatDate, isPastDay } from "../lib/currency";
import { campaignApplicationsLabel, campaignStatusLabel, campaignStatusTone } from "../lib/campaignStatus";
import { navigateWithHistoryOrigin } from "../navigation/hashNavigation";
import { getCachedMyCampaign, removeCachedMyCampaign, setCachedMyCampaign, updateCachedMyCampaign } from "../data/myCampaignCache";
import { removeCachedPublicDetail } from "../data/publicDetailCache";
import { PageHeader } from "../components/PageHeader";
import { CampaignActionsMenu, myCampaignsFeedbackKey, type CampaignMenuResult } from "../components/CampaignActionsMenu";
import { ChipList, DetailSection, FactGrid, compactBudgetRange } from "../components/details/DetailBlocks";
import { ListHero } from "../components/ListHero";
import { DetailLayout } from "../components/DetailLayout";
import { useScreenRefresh } from "../hooks/useScreenRefresh";
import { CampaignScheduleModal } from "../components/CampaignSchedule";
import { formatScheduled } from "../lib/campaignSchedule";

type DetailState = "not-found" | "denied" | "failed" | null;

export function MyCampaignDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  // A team viewer (D64) sees the campaign and its applications, without edit, close or the "…" menu.
  const canOperate = useBusinessCanOperate();
  const [campaign, setCampaign] = useState<MyCampaignDetailsData | null>(() => getCachedMyCampaign(id));
  const [loading, setLoading] = useState(() => !getCachedMyCampaign(id));
  const [failure, setFailure] = useState<DetailState>(null);
  const [closeOpen, setCloseOpen] = useState(false);
  const [closing, setClosing] = useState(false);
  const [reopening, setReopening] = useState(false);
  const [scheduleOpen, setScheduleOpen] = useState(false);
  const [toast, setToast] = useState("");
  // A campaign just created from My campaigns opens here with its "published" message.
  useEffect(() => {
    const feedback = sessionStorage.getItem(myCampaignsFeedbackKey);
    if (!feedback) return;
    sessionStorage.removeItem(myCampaignsFeedbackKey);
    setToast(feedback);
  }, [id]);
  const requestVersionRef = useRef(0);
  const activeRequestRef = useRef<AbortController | undefined>(undefined);
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
  useScreenRefresh(load);

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
      // Any status change on the server also ends a planned publication (D69).
      setCampaign((current) => current ? { ...current, status: 2, scheduledPublishAtUtc: null } : current);
      updateCachedMyCampaign(id, (current) => ({ ...current, status: 2, scheduledPublishAtUtc: null }));
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

  const onMenuResult = (result: CampaignMenuResult) => {
    if (result.kind === "deleted") {
      sessionStorage.setItem(myCampaignsFeedbackKey, result.message);
      window.location.hash = "#/my-campaigns";
      return;
    }
    if (result.kind === "status") setCampaign((current) => current ? { ...current, status: result.status, scheduledPublishAtUtc: null } : current);
    if (result.kind === "failed" && result.reload) void load();
    setToast(result.message);
  };

  if (loading && !campaign) return <div className="campaign-management-screen screen screen--with-nav"><LoadingState title={t("myCampaignDetails.loading")} /><BottomNav /></div>;
  if (!campaign) return <div className="campaign-management-screen screen screen--with-nav detail-error-screen"><PageHeader back={{ href: "#/my-campaigns", label: t("myCampaignDetails.backAria") }} /><ErrorState onRetry={failure === "failed" ? load : undefined} subtitle={t(failure === "not-found" ? "myCampaignDetails.notFoundSubtitle" : failure === "denied" ? "myCampaignDetails.deniedSubtitle" : "myCampaignDetails.errorSubtitle")} title={t(failure === "not-found" ? "myCampaignDetails.notFoundTitle" : failure === "denied" ? "myCampaignDetails.deniedTitle" : "myCampaignDetails.errorTitle")} /><BottomNav /></div>;

  // A closed campaign is published again from the side panel too, not only from the "⋯" menu.
  const reopen = async () => {
    setReopening(true);
    try {
      await reopenMyCampaign(campaign.id);
      onMenuResult({ kind: "status", status: 1, message: t("campaignMenu.reopened") });
    } catch {
      onMenuResult({ kind: "failed", message: t("campaignMenu.reopenFailed"), reload: true });
    } finally {
      setReopening(false);
    }
  };
  const reopenBlocked = isPastDay(campaign.deadline);
  // D69: a draft or closed campaign can wait for a planned moment; the job publishes it then.
  const canSchedule = canOperate && (campaign.status === 0 || campaign.status === 2);
  const scheduled = campaign.status === 0 || campaign.status === 2 ? campaign.scheduledPublishAtUtc ?? null : null;
  const scheduleButton = canSchedule && <Button onClick={() => setScheduleOpen(true)} type="button" variant="secondary">{t(scheduled ? "schedule.change" : "schedule.action")}</Button>;
  const onScheduleDone = (result: { scheduledPublishAtUtc: string | null; message: string } | { failed: string; reload?: boolean }) => {
    setScheduleOpen(false);
    if ("failed" in result) {
      setToast(result.failed);
      if (result.reload) void load();
      return;
    }
    const scheduledPublishAtUtc = result.scheduledPublishAtUtc;
    setCampaign((current) => current ? { ...current, scheduledPublishAtUtc } : current);
    updateCachedMyCampaign(id, (current) => ({ ...current, scheduledPublishAtUtc }));
    notifyCampaignDataChanged();
    setToast(scheduledPublishAtUtc ? t("schedule.saved", { date: formatScheduled(scheduledPublishAtUtc, language) }) : result.message);
  };
  const budget = compactBudgetRange(campaign.minBudget, campaign.maxBudget, t("currency.uzs"));
  const canManage = campaign.status !== 2;
  // D63, package G: the lapis head holds the state, the budget large, the title and the facts as tags; the inbox and the text below.
  return <div className="campaign-management-screen my-campaign-details screen screen--with-nav">
    <ListHero className="detail-hero">
      <PageHeader actions={<><Badge tone={campaignStatusTone(campaign.status)}>{scheduled ? t("schedule.badge") : campaignStatusLabel(campaign.status, t)}</Badge>{campaign.isPromoted && <span className="detail-promoted">{t("card.promoted")}</span>}{canOperate && <CampaignActionsMenu campaign={campaign} onResult={onMenuResult} showApplications={false} />}</>} back={{ href: "#/my-campaigns", label: t("myCampaignDetails.backAria") }} />
      <p className="detail-hero__price">{budget ?? t("myCampaigns.budgetNotSpecified")}</p>
      {budget && <p className="detail-hero__price-unit">{t("common.budget")}</p>}
      <h1 className="detail-hero__title">{campaign.title}</h1>
      <p className="detail-hero__meta">{t("myCampaignDetails.updated", { date: formatDate(campaign.updatedAtUtc) })}</p>
      <FactGrid className="hero-tags fact-grid--three" facts={[
        { label: t("common.city"), value: campaign.city ? cityLabel(campaign.city, language) : null },
        { label: t("campaigns.deadline"), value: campaign.deadline ? formatDate(campaign.deadline) : t("myCampaigns.deadlineNotSpecified") },
        { label: t("myCampaignDetails.created"), value: formatDate(campaign.createdAtUtc) },
        { label: t("myCampaigns.applications"), value: canManage ? null : campaignApplicationsLabel(campaign.applicationsCount, language, t) }
      ]} />
    </ListHero>
    <DetailLayout top={<>
    {scheduled && <p className="my-campaign-details__scheduled" role="status">{t("schedule.plannedNote", { date: formatScheduled(scheduled, language) })}</p>}
    {campaign.status === 1 && isPastDay(campaign.deadline) && <p className="campaign-details__expired" role="status">{t("myCampaignDetails.expiredNote")}</p>}
    {canManage && campaign.applicationsCount === 0 && <div className="my-campaign-details__inbox my-campaign-details__inbox--empty" role="status"><span><strong>{t("applications.emptyTitle")}</strong><span>{t("myCampaignDetails.noApplicationsHint")}</span></span></div>}
    {campaign.applicationsCount > 0 && <a aria-label={t("applications.openInboxAria", { title: campaign.title })} className="my-campaign-details__inbox" href={`#/my-campaign-applications/${id}`}><span><strong>{t("applications.openInbox")}</strong><span>{campaignApplicationsLabel(campaign.applicationsCount, language, t)}</span></span><Icon className="my-campaign-details__inbox-chevron" name="back" /></a>}
    </>} side={<>
    {canManage && <section className="my-campaign-details__actions" aria-label={t("myCampaignDetails.actionsAria")}>
      {canOperate && <>
      <a aria-label={t("myCampaignDetails.editAria", { title: campaign.title })} className="my-campaign-details__edit" href={`#/my-campaign-edit/${id}`} onClick={(event) => {
        if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        navigateWithHistoryOrigin(window.location.hash, `#/my-campaign-edit/${id}`);
      }}>{t("myCampaignDetails.edit")}</a>
      {campaign.status === 0 && scheduleButton}
      <Button aria-label={t("myCampaignDetails.closeAria", { title: campaign.title })} onClick={() => setCloseOpen(true)} type="button" variant="danger">{t("myCampaignDetails.close")}</Button>
      </>}
      {/* The owner sees the campaign the way bloggers see it in the catalog. */}
      {campaign.status === 1 && <a className="my-campaign-details__edit my-campaign-details__public" href={`#/campaign/${id}`}>{t("myCampaignDetails.viewPublic")}</a>}
    </section>}
    {campaign.status === 2 && canOperate && <section className="my-campaign-details__actions" aria-label={t("myCampaignDetails.actionsAria")}><Button aria-busy={reopening} disabled={reopening || reopenBlocked} onClick={() => void reopen()} type="button">{t("campaignMenu.reopen")}</Button>{scheduleButton}</section>}
    {campaign.status === 2 && <p className="my-campaign-details__closed-hint">{t(reopenBlocked ? "campaignMenu.reopenExpired" : "myCampaignDetails.closedHintSide")}</p>}
    </>}>
    <DetailSection title={t("myCampaignDetails.description")}><Card><p className="whitespace-pre-line text-sm leading-6 text-brand-muted">{campaign.description}</p></Card></DetailSection>
    <DetailSection title={t("campaign.suitable")}>{campaign.categories.length ? <ChipList items={campaign.categories.map((category) => categoryLabel(category, language))} /> : <p className="text-sm text-brand-muted">{t("common.notSpecified")}</p>}</DetailSection>
    <DetailSection title={t("common.requirements")}><Card>{campaign.requirements.length ? <ul className="grid gap-2">{campaign.requirements.map((requirement) => <li className="flex gap-2 text-sm text-brand-muted" key={requirement}><Icon aria-hidden="true" className="h-4 w-4 shrink-0 text-brand-success" name="check" />{requirement}</li>)}</ul> : <p className="text-sm text-brand-muted">{t("common.notSpecified")}</p>}</Card></DetailSection>
    </DetailLayout>
    <Modal onClose={() => { if (!closing) setCloseOpen(false); }} open={closeOpen} title={t("myCampaignDetails.closeTitle")} variant="neutral">
      <p className="text-sm leading-6 text-brand-muted">{t("myCampaignDetails.closeDescription")}</p>
      <div className="mt-5 modal-actions"><Button disabled={closing} onClick={() => setCloseOpen(false)} type="button" variant="secondary">{t("common.cancel")}</Button><Button aria-busy={closing} disabled={closing} onClick={closeCampaign} type="button" variant="danger">{closing ? t("myCampaignDetails.closing") : t("myCampaignDetails.close")}</Button></div>
    </Modal>
    {canSchedule && <CampaignScheduleModal campaignId={campaign.id} current={scheduled} deadline={campaign.deadline} onClose={() => setScheduleOpen(false)} onDone={onScheduleDone} open={scheduleOpen} />}
    <Toast message={toast} tone="success" />
    <BottomNav />
  </div>;
}
