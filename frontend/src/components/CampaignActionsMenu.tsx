import { useState } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { closeMyCampaign, deleteMyCampaign, reopenMyCampaign, type MyCampaignStatus } from "../api/marketplace";
import { removeCachedMyCampaign, updateCachedMyCampaign } from "../data/myCampaignCache";
import { removeCachedPublicDetail } from "../data/publicDetailCache";
import { notifyCampaignDataChanged } from "../hooks/useCampaignDataRefresh";
import { useI18n } from "../i18n";
import { isPastDay } from "../lib/currency";
import { navigateWithHistoryOrigin } from "../navigation/hashNavigation";
import { BottomSheet, Button, Icon, Modal } from "./ui";

export type CampaignMenuTarget = { id: string; title: string; status: MyCampaignStatus; applicationsCount: number; deadline?: string | null };
export type CampaignMenuResult = { kind: "status"; status: MyCampaignStatus; message: string } | { kind: "deleted"; message: string } | { kind: "failed"; message: string; reload?: boolean };

type Confirm = "close" | "delete" | null;

// A deleted campaign has no page left to show the toast, so the list picks it up.
export const myCampaignsFeedbackKey = "bloggerbazar.my-campaigns-feedback";

// One place for everything an owner can do with a campaign: edit, see applications, close, publish again, delete.
export function CampaignActionsMenu({ campaign, onResult, showApplications = true }: { campaign: CampaignMenuTarget; onResult: (result: CampaignMenuResult) => void; showApplications?: boolean }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const [confirm, setConfirm] = useState<Confirm>(null);
  const [busy, setBusy] = useState(false);
  const closed = campaign.status === 2;
  const canEdit = !closed;
  const canClose = campaign.status === 0 || campaign.status === 1;
  const canReopen = campaign.status === 0 || closed;
  const canDelete = campaign.applicationsCount === 0;

  const go = (href: string) => {
    setOpen(false);
    navigateWithHistoryOrigin(window.location.hash, href);
  };

  const changeStatus = (status: MyCampaignStatus) => {
    updateCachedMyCampaign(campaign.id, (current) => ({ ...current, status }));
    removeCachedPublicDetail("campaign", campaign.id);
    notifyCampaignDataChanged();
  };

  const run = async (action: "close" | "reopen" | "delete") => {
    if (busy) return;
    setBusy(true);
    try {
      if (action === "close") {
        await closeMyCampaign(campaign.id);
        changeStatus(2);
        onResult({ kind: "status", status: 2, message: t("myCampaignDetails.closed") });
      } else if (action === "reopen") {
        await reopenMyCampaign(campaign.id);
        changeStatus(1);
        onResult({ kind: "status", status: 1, message: t("campaignMenu.reopened") });
      } else {
        await deleteMyCampaign(campaign.id);
        removeCachedMyCampaign(campaign.id);
        removeCachedPublicDetail("campaign", campaign.id);
        notifyCampaignDataChanged();
        onResult({ kind: "deleted", message: t("campaignMenu.deleted") });
      }
    } catch (error) {
      const fallback = t(action === "close" ? "myCampaignDetails.closeFailed" : action === "reopen" ? "campaignMenu.reopenFailed" : "campaignMenu.deleteFailed");
      const known = error instanceof ApiError && (error.code === "campaign_expired" || error.code === "campaign_has_applications");
      onResult({ kind: "failed", message: known ? getApiErrorMessage(error, fallback) : fallback, reload: error instanceof ApiError && (error.status === 404 || error.status === 409) });
    } finally {
      setBusy(false);
      setConfirm(null);
      setOpen(false);
    }
  };

  const reopenBlocked = canReopen && isPastDay(campaign.deadline);
  return <>
    <button aria-expanded={open} aria-haspopup="dialog" aria-label={t("campaignMenu.openAria", { title: campaign.title })} className="campaign-menu__trigger" onClick={(event) => { event.preventDefault(); event.stopPropagation(); setOpen(true); }} type="button"><Icon name="dots" /></button>
    <BottomSheet onClose={() => { if (!busy) setOpen(false); }} open={open && !confirm} title={campaign.title} variant="neutral">
      <div className="campaign-menu">
        {canEdit && <button className="campaign-menu__item" onClick={() => go(`#/my-campaign-edit/${campaign.id}`)} type="button">{t("myCampaignDetails.edit")}</button>}
        {showApplications && campaign.applicationsCount > 0 && <button className="campaign-menu__item" onClick={() => go(`#/my-campaign-applications/${campaign.id}`)} type="button">{t("applications.openInbox")}</button>}
        {canReopen && <button className="campaign-menu__item" disabled={busy || reopenBlocked} onClick={() => void run("reopen")} type="button">{t(campaign.status === 0 ? "campaignMenu.publish" : "campaignMenu.reopen")}{reopenBlocked && <span>{t("campaignMenu.reopenExpired")}</span>}</button>}
        {canClose && <button className="campaign-menu__item" onClick={() => setConfirm("close")} type="button">{t("campaignMenu.close")}</button>}
        {canDelete && <button className="campaign-menu__item campaign-menu__item--danger" onClick={() => setConfirm("delete")} type="button">{t("campaignMenu.delete")}</button>}
        {!canDelete && <p className="campaign-menu__note">{t("campaignMenu.deleteUnavailable")}</p>}
      </div>
    </BottomSheet>
    <Modal onClose={() => { if (!busy) { setConfirm(null); setOpen(false); } }} open={confirm !== null} title={t(confirm === "delete" ? "campaignMenu.deleteTitle" : "myCampaignDetails.closeTitle")} variant="neutral">
      <p className="text-sm leading-6 text-brand-muted">{t(confirm === "delete" ? "campaignMenu.deleteDescription" : "myCampaignDetails.closeDescription")}</p>
      <div className="mt-5 grid grid-cols-2 gap-3">
        <Button disabled={busy} onClick={() => { setConfirm(null); setOpen(false); }} type="button" variant="secondary">{t("common.cancel")}</Button>
        <Button aria-busy={busy} disabled={busy} onClick={() => void run(confirm === "delete" ? "delete" : "close")} type="button" variant="danger">{t(confirm === "delete" ? "campaignMenu.delete" : "myCampaignDetails.close")}</Button>
      </div>
    </Modal>
  </>;
}
