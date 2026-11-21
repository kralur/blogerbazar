import { useEffect, useState } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { cancelMyCampaignSchedule, scheduleMyCampaign, type CampaignMutationResponse } from "../api/marketplace";
import { useI18n } from "../i18n";
import { defaultSchedule, scheduleDayRange, scheduleProblem, scheduleProblemKey, tashkentParts, tashkentToUtc } from "../lib/campaignSchedule";
import { Button, Input, Modal } from "./ui";

export type ScheduleValue = { day: string; time: string };

// D69: the Tashkent day and time of a planned publication, with the reason it cannot be used yet.
export function CampaignScheduleFields({ value, onChange, deadline }: { value: ScheduleValue; onChange: (value: ScheduleValue) => void; deadline?: string | null }) {
  const { t } = useI18n();
  const range = scheduleDayRange();
  const problem = value.day && value.time ? scheduleProblem(tashkentToUtc(value.day, value.time), deadline) : null;
  return <div className="campaign-schedule-fields grid gap-3">
    <div className="campaign-schedule-fields__row">
      <Input className="campaign-create-form__input" label={t("schedule.day")} max={range.max} min={range.min} onChange={(event) => onChange({ ...value, day: event.target.value })} required type="date" value={value.day} />
      <Input className="campaign-create-form__input" label={t("schedule.time")} onChange={(event) => onChange({ ...value, time: event.target.value })} required step={60} type="time" value={value.time} />
    </div>
    {problem ? <p className="campaign-schedule-fields__error" role="alert">{t(scheduleProblemKey[problem])}</p> : <p className="campaign-schedule-fields__hint">{t("schedule.hint")}</p>}
  </div>;
}

export function scheduleReady(value: ScheduleValue, deadline?: string | null) {
  return scheduleProblem(tashkentToUtc(value.day, value.time), deadline) === null;
}

// Plans, moves or drops the publication of a draft or closed campaign from its page.
export function CampaignScheduleModal({ campaignId, deadline, current, open, onClose, onDone }: {
  campaignId: string;
  deadline?: string | null;
  current?: string | null;
  open: boolean;
  onClose: () => void;
  onDone: (result: { scheduledPublishAtUtc: string | null; message: string } | { failed: string; reload?: boolean }) => void;
}) {
  const { t } = useI18n();
  const [value, setValue] = useState<ScheduleValue>(() => current ? tashkentParts(current) : defaultSchedule());
  const [busy, setBusy] = useState(false);
  useEffect(() => { if (open) setValue(current ? tashkentParts(current) : defaultSchedule()); }, [current, open]);

  const failure = (error: unknown, fallback: string) => {
    if (error instanceof ApiError && error.code === "campaign_expired") return t("schedule.errorExpired");
    if (error instanceof ApiError && error.code === "validation_failed") return t("schedule.errorPast");
    return getApiErrorMessage(error, fallback);
  };

  const save = async () => {
    const at = tashkentToUtc(value.day, value.time);
    if (busy || !at || !scheduleReady(value, deadline)) return;
    setBusy(true);
    try {
      const saved: CampaignMutationResponse = await scheduleMyCampaign(campaignId, at.toISOString());
      const planned = saved?.scheduledPublishAtUtc ?? at.toISOString();
      onDone({ scheduledPublishAtUtc: planned, message: "saved" });
    } catch (error) {
      onDone({ failed: failure(error, t("schedule.failed")), reload: error instanceof ApiError && (error.status === 404 || error.status === 409) });
    } finally {
      setBusy(false);
    }
  };

  const cancel = async () => {
    if (busy) return;
    setBusy(true);
    try {
      await cancelMyCampaignSchedule(campaignId);
      onDone({ scheduledPublishAtUtc: null, message: t("schedule.cancelled") });
    } catch (error) {
      onDone({ failed: getApiErrorMessage(error, t("schedule.cancelFailed")), reload: error instanceof ApiError && error.status === 404 });
    } finally {
      setBusy(false);
    }
  };

  return <Modal onClose={() => { if (!busy) onClose(); }} open={open} title={t("schedule.title")} variant="neutral">
    <CampaignScheduleFields deadline={deadline} onChange={setValue} value={value} />
    <div className="mt-5 modal-actions">
      {current ? <Button disabled={busy} onClick={() => void cancel()} type="button" variant="secondary">{t("schedule.cancel")}</Button> : <Button disabled={busy} onClick={onClose} type="button" variant="secondary">{t("common.cancel")}</Button>}
      <Button aria-busy={busy} disabled={busy || !scheduleReady(value, deadline)} onClick={() => void save()} type="button">{busy ? t("schedule.saving") : t("schedule.submit")}</Button>
    </div>
  </Modal>;
}
