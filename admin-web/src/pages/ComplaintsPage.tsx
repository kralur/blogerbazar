import { useRef, useState } from "react";
import { closeComplaint, complaintStatuses, getComplaintReview, getComplaints, maxComplaintNote, ownerRole, setUserBlocked, type AdminComplaint, type ComplaintReview, type ComplaintStatus } from "../api/admin";
import type { AdminWebSession } from "../api/session";
import { useActions } from "../components/Actions";
import { Link, userPath } from "../components/Link";
import { Report } from "../components/Report";
import { Badge, Button, Card, Choice, Table, Tile } from "../components/ui";
import { useI18n } from "../i18n";
import { formatNumber, percent } from "../lib/format";
import { formatTashkentDate, formatTashkentDateTime } from "../lib/time";
import { useLoad } from "../lib/useLoad";
import { dealStatusLabel } from "./DealsPage";

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

export const complaintPath = (id: string) => `/complaints/${id}`;
const offerStatuses = ["sent", "viewed", "accepted", "declined", "expired"];

// What was reported and why; shared by the list and the review page.
function ComplaintSummary({ complaint }: { complaint: AdminComplaint }) {
  const { t } = useI18n();
  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center gap-2">
        <p className="min-w-0 break-words font-extrabold">{complaint.targetName ?? t("user.deletedAccount")}</p>
        <Badge>{t(`admin.complaintTarget.${complaint.targetType}`)}</Badge>
        <Badge tone={complaint.status === "open" ? "warning" : "neutral"}>{t(`complaints.state.${complaint.status}`)}</Badge>
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
    </div>
  );
}

// Complaints from every role (D52), open cases first. Each case opens its review page, where the decision is made.
export function ComplaintsPage() {
  const { t } = useI18n();
  const [status, setStatus] = useState<ComplaintStatus>("open");
  const { state, reload } = useLoad(() => getComplaints(status), status);
  return (
    <div className="space-y-6">
      <Choice label={t("complaints.filter")} value={status} onChange={setStatus} options={complaintStatuses.map((value) => ({ value, label: t(`admin.complaintStatus.${value}`) }))} />
      <Report title={t("complaints.title")} hint={t("complaints.hint")} loaded={state} onRetry={reload}>
        {(complaints) => complaints.length === 0 ? <p className="text-sm text-brand-muted">{t("complaints.empty")}</p> : (
          <ul className="space-y-3">
            {complaints.map((complaint) => (
              <li key={complaint.id} className="space-y-3 rounded-[var(--bb-radius-control)] border border-brand-line p-4">
                <ComplaintSummary complaint={complaint} />
                <Link
                  to={complaintPath(complaint.id)}
                  className="inline-flex min-h-[44px] items-center justify-center rounded-[var(--bb-radius-control)] bg-brand-accent px-4 text-sm font-semibold text-[color:var(--bb-accent-text)]"
                >
                  {complaint.status === "open" ? t("review.open") : t("review.view")}
                </Link>
              </li>
            ))}
          </ul>
        )}
      </Report>
    </div>
  );
}

// One case and everything needed to decide it: the accused's record, the reporter's history and what the two did
// together. The decision buttons are here, after the facts.
export function ComplaintReviewPage({ complaintId, session }: { complaintId: string; session: AdminWebSession }) {
  const { t } = useI18n();
  const { state, reload, refresh } = useLoad(() => getComplaintReview(complaintId), complaintId);
  const actions = useActions(refresh);
  const note = useRef("");

  const close = (review: ComplaintReview, next: "resolved" | "dismissed") => {
    note.current = "";
    actions.ask({
      title: t(`complaints.${next}.title`),
      text: t(`complaints.${next}.text`, { name: review.complaint.targetName ?? t("user.deletedAccount") }),
      confirmLabel: t(`complaints.${next}`),
      danger: next === "dismissed",
      body: <NoteField onChange={(value) => { note.current = value; }} />,
      run: () => closeComplaint(review.complaint.id, next, note.current),
      done: t(`complaints.${next}.done`)
    });
  };

  const toggleBlock = (accusedId: number, name: string, blocked: boolean) => actions.ask(blocked
    ? { title: t("user.unblockTitle"), text: t("user.unblockText", { name }), confirmLabel: t("user.unblock"), run: () => setUserBlocked(accusedId, false), done: t("user.unblockDone") }
    : { title: t("user.blockTitle"), text: t("user.blockText", { name }), confirmLabel: t("user.block"), danger: true, run: () => setUserBlocked(accusedId, true), done: t("user.blockDone") });

  return (
    <div className="space-y-6">
      <Link to="/complaints">{t("review.back")}</Link>
      {actions.status}
      <Report title={t("review.case")} loaded={state} onRetry={reload}>
        {(review) => <ComplaintSummary complaint={review.complaint} />}
      </Report>
      {state.status === "ready" && <ReviewFacts review={state.data} />}
      {state.status === "ready" && (
        <Card className="space-y-4">
          <h2 className="text-lg font-extrabold">{t("review.decision")}</h2>
          {state.data.complaint.status === "open"
            ? <p className="text-sm text-brand-muted">{t("review.decisionHint")}</p>
            : <p className="text-sm text-brand-muted">{t("review.closed")}</p>}
          <div className="flex flex-wrap gap-2">
            {state.data.complaint.status === "open" && <Button variant="primary" onClick={() => close(state.data, "resolved")}>{t("complaints.resolved")}</Button>}
            {state.data.complaint.status === "open" && <Button onClick={() => close(state.data, "dismissed")}>{t("complaints.dismissed")}</Button>}
            {state.data.accused && state.data.accused.user.role !== ownerRole && session.role === "owner" && !state.data.accused.user.isDeleted && (
              <Button
                variant={state.data.accused.user.isBlocked ? "secondary" : "danger"}
                onClick={() => toggleBlock(state.data.accused!.user.telegramUserId, state.data.accused!.user.firstName, state.data.accused!.user.isBlocked)}
              >
                {state.data.accused.user.isBlocked ? t("user.unblock") : t("review.blockAccused")}
              </Button>
            )}
          </div>
        </Card>
      )}
      {actions.dialog}
    </div>
  );
}

function ReviewFacts({ review }: { review: ComplaintReview }) {
  const { t } = useI18n();
  const { accused, reporter } = review;
  const dismissedShare = percent(reporter.dismissed, reporter.resolved + reporter.dismissed);
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card className="min-w-0 space-y-4">
        <div>
          <h2 className="text-lg font-extrabold">{t("review.accused")}</h2>
          <p className="mt-1 text-sm text-brand-muted">{t("review.accusedHint")}</p>
        </div>
        {!accused ? (
          review.accusedTelegramUserId
            ? <p className="text-sm text-brand-muted"><Link to={userPath(review.accusedTelegramUserId)}>{String(review.accusedTelegramUserId)}</Link> · {t("review.noAccount")}</p>
            : <p className="text-sm text-brand-muted">{t("review.noAccused")}</p>
        ) : (
          <div className="space-y-4">
            <p className="flex flex-wrap items-center gap-2">
              <Link to={userPath(accused.user.telegramUserId)}>{accused.user.firstName}</Link>
              {accused.user.username && <span className="text-sm text-brand-muted">@{accused.user.username}</span>}
              {accused.user.isBlocked && <Badge tone="danger">{t("users.blocked")}</Badge>}
              {accused.user.isDeleted && <Badge>{t("users.deleted")}</Badge>}
            </p>
            <div className="grid gap-3 sm:grid-cols-2">
              <Tile label={t("review.registered")} value={formatTashkentDate(accused.user.createdAtUtc)} />
              <Tile label={t("review.rating")} value={review.reviews.average === null ? "–" : String(review.reviews.average).replace(".", ",")} note={t("review.reviewsCount", { count: formatNumber(review.reviews.count) })} />
              <Tile label={t("review.dealsDone")} value={formatNumber(accused.completedDeals)} note={t("review.dealsOther", { active: accused.activeDeals, cancelled: accused.cancelledDeals })} />
              <Tile label={t("review.otherComplaints")} value={formatNumber(review.otherComplaints.length)} note={t("review.openOf", { count: review.otherComplaints.filter((item) => item.status === "open").length })} />
            </div>
            <section className="space-y-2">
              <h3 className="text-sm font-bold">{t("review.recentReviews")}</h3>
              {review.reviews.recent.length === 0 ? <p className="text-sm text-brand-muted">{t("review.noReviews")}</p> : (
                <ul className="space-y-2">
                  {review.reviews.recent.map((item, index) => (
                    <li key={index} className="rounded-[var(--bb-radius-control)] border border-brand-line px-3 py-2 text-sm">
                      <p className="font-semibold">{t("review.stars", { rating: item.rating })} · {item.reviewerName ?? t("user.deletedAccount")} · {formatTashkentDate(item.createdAtUtc)}{!item.isPublished && <> <Badge>{t("review.unpublished")}</Badge></>}</p>
                      {item.comment && <p className="mt-1 whitespace-pre-line break-words text-brand-muted">{item.comment}</p>}
                    </li>
                  ))}
                </ul>
              )}
            </section>
            <section className="space-y-2">
              <h3 className="text-sm font-bold">{t("review.otherComplaints")}</h3>
              <Table
                caption={t("review.otherComplaints")}
                empty={t("review.noOtherComplaints")}
                textColumns={[1, 2, 3]}
                head={[t("deals.date"), t("review.reason"), t("review.from"), t("deals.status")]}
                rows={review.otherComplaints.map((item) => [
                  <Link key="date" to={complaintPath(item.id)}>{formatTashkentDate(item.createdAtUtc)}</Link>,
                  t(`report.reason.${item.reason}`),
                  item.reporterName ?? String(item.reporterTelegramUserId),
                  t(`complaints.state.${item.status}`)
                ])}
              />
            </section>
          </div>
        )}
      </Card>

      <div className="min-w-0 space-y-6">
        <Card className="space-y-4">
          <div>
            <h2 className="text-lg font-extrabold">{t("review.reporterTitle")}</h2>
            <p className="mt-1 text-sm text-brand-muted">{t("review.reporterHint")}</p>
          </div>
          <p className="flex flex-wrap items-center gap-2">
            <Link to={userPath(reporter.telegramUserId)}>{reporter.person?.firstName ?? String(reporter.telegramUserId)}</Link>
            {reporter.person?.username && <span className="text-sm text-brand-muted">@{reporter.person.username}</span>}
            {reporter.person?.isBlocked && <Badge tone="danger">{t("users.blocked")}</Badge>}
          </p>
          <div className="grid gap-3 sm:grid-cols-2">
            <Tile label={t("review.filed")} value={formatNumber(reporter.filed)} note={t("review.filedSplit", { open: reporter.open, resolved: reporter.resolved, dismissed: reporter.dismissed })} />
            <Tile label={t("review.dismissedShare")} value={dismissedShare ?? "–"} note={t("review.dismissedHint")} />
            {reporter.person && <Tile label={t("review.registered")} value={formatTashkentDate(reporter.person.createdAtUtc)} />}
          </div>
        </Card>

        <Card className="space-y-4">
          <div>
            <h2 className="text-lg font-extrabold">{t("review.sharedTitle")}</h2>
            <p className="mt-1 text-sm text-brand-muted">{t("review.sharedHint")}</p>
          </div>
          <Table
            caption={t("review.sharedTitle")}
            empty={review.accused ? t("review.noShared") : t("review.noAccused")}
            textColumns={[1, 2]}
            head={[t("deals.date"), t("deals.what"), t("deals.status")]}
            rows={review.sharedHistory.map((item) => [
              formatTashkentDate(item.createdAtUtc),
              item.kind === "deal" ? item.title ?? t("deals.offer") : t("review.offer"),
              item.kind === "deal" ? dealStatusLabel(item.status, t) : t(`review.offerState.${offerStatuses[item.status] ?? "sent"}`)
            ])}
          />
        </Card>
      </div>
    </div>
  );
}
