import { useRef, useState } from "react";
import { createPortal } from "react-dom";
import { getApiErrorMessage } from "../api/client";
import { createComplaint, type ComplaintReason, type ComplaintTarget } from "../api/marketplace";
import { useI18n } from "../i18n";
import { Button, Modal, Textarea, Toast } from "./ui";

// The order shown in the form; the same fixed values the API accepts.
export const complaintReasons: readonly ComplaintReason[] = ["spam", "fraud", "fake", "offensive", "noShow", "other"];

// D52: a red "Report" button in the page header of a profile, campaign, offer or deal. An admin reviews it;
// nothing is blocked automatically. The dialog and toast go to <body> so the header holds only the button.
export function ReportButton({ targetType, targetId }: { targetType: ComplaintTarget; targetId: string }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState<ComplaintReason | null>(null);
  const [comment, setComment] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [toast, setToast] = useState("");
  const [tone, setTone] = useState<"success" | "error">("success");
  const busyRef = useRef(false);

  const start = () => { setReason(null); setComment(""); setError(""); setOpen(true); };

  const send = async () => {
    if (busyRef.current) return;
    if (!reason) { setError(t("report.reasonRequired")); return; }
    if (reason === "other" && comment.trim().length < 5) { setError(t("report.commentRequired")); return; }
    busyRef.current = true;
    setBusy(true);
    try {
      const result = await createComplaint(targetType, targetId, reason, comment);
      setOpen(false);
      setTone("success");
      setToast(t(result.alreadyReported ? "report.already" : "report.sent"));
    } catch (failure) {
      setOpen(false);
      setTone("error");
      setToast(getApiErrorMessage(failure, t("report.failed")));
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  };

  return <>
    <button aria-haspopup="dialog" className="report-button" onClick={start} type="button">
      <svg aria-hidden="true" fill="none" height="16" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" viewBox="0 0 24 24" width="16"><path d="M5 21V4" /><path d="M5 4h11l-2 4 2 4H5" /></svg>
      {t("report.action")}
    </button>
    {typeof document !== "undefined" && createPortal(<>
      <Modal onClose={() => !busy && setOpen(false)} open={open} title={t("report.title")}>
        <p className="text-sm leading-6 text-brand-muted">{t("report.description")}</p>
        <div className="mt-4 grid gap-2" role="radiogroup" aria-label={t("report.reasonLabel")}>
          {complaintReasons.map((value) => <label className="flex min-h-11 items-center gap-3 rounded-xl border border-brand-line px-3 text-sm font-semibold" key={value}>
            <input checked={reason === value} name="complaint-reason" onChange={() => { setReason(value); setError(""); }} type="radio" value={value} />
            {t(`report.reason.${value}`)}
          </label>)}
        </div>
        <Textarea className="mt-3" label={reason === "other" ? t("report.commentLabelRequired") : t("report.commentLabel")} maxLength={500} onChange={(event) => { setComment(event.target.value); setError(""); }} value={comment} />
        {error && <p className="mt-2 text-sm font-semibold text-brand-danger" role="alert">{error}</p>}
        <div className="mt-5 grid grid-cols-2 gap-3"><Button disabled={busy} onClick={() => setOpen(false)} type="button" variant="secondary">{t("common.cancel")}</Button><Button disabled={busy} onClick={() => void send()} type="button">{t("report.send")}</Button></div>
      </Modal>
      <Toast message={toast} tone={tone} />
    </>, document.body)}
  </>;
}
