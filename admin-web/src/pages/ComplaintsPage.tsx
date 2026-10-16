import { useRef, useState } from "react";
import { closeComplaint, complaintStatuses, getComplaints, maxComplaintNote, setUserBlocked, type AdminComplaint, type ComplaintStatus } from "../api/admin";
import type { AdminWebSession } from "../api/session";
import { useActions } from "../components/Actions";
import { Link, userPath } from "../components/Link";
import { Report } from "../components/Report";
import { Badge, Button, Choice } from "../components/ui";
import { useI18n } from "../i18n";
import { formatTashkentDateTime } from "../lib/time";
import { useLoad } from "../lib/useLoad";

// The admin's note when closing a case; optional, at most 500 characters like the server allows.
function NoteField({ onChange }: { onChange: (note: string) => void }) {
  const { t } = useI18n();
  const [note, setNote] = useState("");
  return (
    <label className="grid gap-1 text-xs text-brand-muted">
      {t("complaints.note")}
      <textarea
        className="min-h-[96px] rounded-[var(--bb-radius-control)] border border-brand-line bg-brand-surface px-3 py-2 text-sm text-brand-ink"
        maxLength={maxComplaintNote}
        value={note}
        onChange={(event) => { setNote(event.target.value); onChange(event.target.value); }}
      />
      <span>{t("complaints.noteLimit", { count: note.length, max: maxComplaintNote })}</span>
    </label>
  );
}

// Complaints from every role (D52): open cases first; closing one is final and goes to the journal. The owner can
// also block the person who answers for the reported profile or campaign.
export function ComplaintsPage({ session }: { session: AdminWebSession }) {
  const { t } = useI18n();
  const [status, setStatus] = useState<ComplaintStatus>("open");
  const { state, reload, refresh } = useLoad(() => getComplaints(status), status);
  const actions = useActions(refresh);
  const note = useRef("");

  const close = (complaint: AdminComplaint, next: "resolved" | "dismissed") => {
    note.current = "";
    actions.ask({
      title: t(`complaints.${next}.title`),
      text: t(`complaints.${next}.text`, { name: complaint.targetName ?? t("user.deletedAccount") }),
      confirmLabel: t(`complaints.${next}`),
      danger: next === "dismissed",
      body: <NoteField onChange={(value) => { note.current = value; }} />,
      run: () => closeComplaint(complaint.id, next, note.current),
      done: t(`complaints.${next}.done`)
    });
  };

  const toggleBlock = (complaint: AdminComplaint) => {
    if (!complaint.ownerTelegramUserId) return;
    const ownerId = complaint.ownerTelegramUserId;
    const name = complaint.targetName ?? String(ownerId);
    actions.ask(complaint.ownerIsBlocked
      ? { title: t("user.unblockTitle"), text: t("user.unblockText", { name }), confirmLabel: t("user.unblock"), run: () => setUserBlocked(ownerId, false), done: t("user.unblockDone") }
      : { title: t("user.blockTitle"), text: t("user.blockText", { name }), confirmLabel: t("user.block"), danger: true, run: () => setUserBlocked(ownerId, true), done: t("user.blockDone") });
  };

  return (
    <div className="space-y-6">
      <Choice label={t("complaints.filter")} value={status} onChange={setStatus} options={complaintStatuses.map((value) => ({ value, label: t(`admin.complaintStatus.${value}`) }))} />
      {actions.status}
      <Report title={t("complaints.title")} hint={t("complaints.hint")} loaded={state} onRetry={reload}>
        {(complaints) => complaints.length === 0 ? <p className="text-sm text-brand-muted">{t("complaints.empty")}</p> : (
          <ul className="space-y-3">
            {complaints.map((complaint) => (
              <li key={complaint.id} className="space-y-2 rounded-[var(--bb-radius-control)] border border-brand-line p-4">
                <div className="flex flex-wrap items-center gap-2">
                  <p className="min-w-0 break-words font-extrabold">{complaint.targetName ?? t("user.deletedAccount")}</p>
                  <Badge>{t(`admin.complaintTarget.${complaint.targetType}`)}</Badge>
                  {complaint.ownerIsBlocked && <Badge tone="danger">{t("users.blocked")}</Badge>}
                </div>
                <p className="text-sm font-semibold">{t(`report.reason.${complaint.reason}`)}</p>
                {complaint.comment && <p className="whitespace-pre-line break-words text-sm leading-6 text-brand-muted">{complaint.comment}</p>}
                <p className="text-xs text-brand-muted">
                  {t("complaints.reporter")} <Link to={userPath(complaint.reporterTelegramUserId)}>{complaint.reporterName ?? String(complaint.reporterTelegramUserId)}</Link>
                  {" · "}{formatTashkentDateTime(complaint.createdAtUtc)}
                </p>
                {complaint.status !== "open" && (
                  <p className="text-xs text-brand-muted">
                    {t("complaints.closedAt", { date: formatTashkentDateTime(complaint.resolvedAtUtc) })}
                    {complaint.resolutionNote ? ` · ${complaint.resolutionNote}` : ""}
                  </p>
                )}
                <div className="flex flex-wrap gap-2 pt-1">
                  {complaint.status === "open" && <Button variant="primary" onClick={() => close(complaint, "resolved")}>{t("complaints.resolved")}</Button>}
                  {complaint.status === "open" && <Button onClick={() => close(complaint, "dismissed")}>{t("complaints.dismissed")}</Button>}
                  {complaint.ownerTelegramUserId && <Link className="inline-flex min-h-[44px] items-center px-2 text-sm font-semibold text-brand-blue underline-offset-4 hover:underline" to={userPath(complaint.ownerTelegramUserId)}>{t("complaints.ownerCard")}</Link>}
                  {complaint.ownerTelegramUserId && !complaint.ownerIsPlatformOwner && session.role === "owner" && (
                    <Button variant={complaint.ownerIsBlocked ? "secondary" : "danger"} onClick={() => toggleBlock(complaint)}>
                      {complaint.ownerIsBlocked ? t("user.unblock") : t("user.block")}
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </Report>
      {actions.dialog}
    </div>
  );
}
