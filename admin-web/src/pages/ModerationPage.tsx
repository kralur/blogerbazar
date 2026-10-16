import { useRef, useState } from "react";
import {
  getBloggers, getBusinesses, getCampaigns, getPendingBloggers, getPendingBusinesses, moderateBlogger, moderateBusiness, moderateCampaign,
  type AdminCampaign, type ModerationDecision
} from "../api/admin";
import { useActions, type AdminAction } from "../components/Actions";
import { UserLink } from "../components/Link";
import { Report } from "../components/Report";
import { Badge, Button, Table } from "../components/ui";
import { useI18n } from "../i18n";
import { formatNumber, taxonomyLabel } from "../lib/format";
import { formatTashkentDate } from "../lib/time";
import { useLoad } from "../lib/useLoad";

type Translate = (key: string, values?: Record<string, string | number>) => string;
const decisions: ModerationDecision[] = ["approve", "needs-changes", "reject"];
const profileStatus = (status: number, t: Translate) => t(`admin.moderation.${status}`);

// The decision dialog: every decision sends the person a bot message and is written to the journal.
function decisionAction(kind: "blogger" | "business", name: string, decision: ModerationDecision, run: () => Promise<unknown>, t: Translate): AdminAction {
  return {
    title: t(`moderation.${decision}.title`, { name }),
    text: t(`moderation.${decision}.text.${kind}`),
    confirmLabel: t(`moderation.${decision}`),
    danger: decision === "reject",
    run,
    done: t(`moderation.${decision}.done`, { name })
  };
}

function PendingItem({ name, details, onDecide }: { name: string; details: string; onDecide: (decision: ModerationDecision) => void }) {
  const { t } = useI18n();
  return (
    <li className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--bb-radius-control)] border border-brand-line p-4">
      <div className="min-w-0">
        <p className="break-words font-semibold">{name}</p>
        <p className="text-xs text-brand-muted">{details}</p>
      </div>
      <div className="flex flex-wrap gap-2">
        {decisions.map((decision) => (
          <Button key={decision} variant={decision === "approve" ? "primary" : decision === "reject" ? "danger" : "secondary"} onClick={() => onDecide(decision)}>
            {t(`moderation.${decision}`)}
          </Button>
        ))}
      </div>
    </li>
  );
}

// Bloggers: the queue waiting for a decision (profiles go live on save, so it is usually empty) and every profile.
export function BloggerModerationPage() {
  const { t } = useI18n();
  const pending = useLoad(getPendingBloggers, "pending-bloggers");
  const all = useLoad(getBloggers, "bloggers");
  const actions = useActions(() => { pending.refresh(); all.refresh(); });
  return (
    <div className="space-y-6">
      {actions.status}
      <Report title={t("moderation.pendingTitle")} hint={t("moderation.pendingHint")} loaded={pending.state} onRetry={pending.reload}>
        {(profiles) => profiles.length === 0 ? <p className="text-sm text-brand-muted">{t("moderation.pendingEmpty")}</p> : (
          <ul className="space-y-3">
            {profiles.map((profile) => (
              <PendingItem
                key={profile.id}
                name={profile.name}
                details={`${taxonomyLabel("city", profile.city, t)} · ${formatTashkentDate(profile.createdAtUtc)}`}
                onDecide={(decision) => actions.ask(decisionAction("blogger", profile.name, decision, () => moderateBlogger(profile.id, decision), t))}
              />
            ))}
          </ul>
        )}
      </Report>
      <Report title={t("moderation.bloggersTitle")} hint={t("moderation.listHint")} loaded={all.state} onRetry={all.reload}>
        {(profiles) => (
          <Table
            caption={t("moderation.bloggersTitle")}
            empty={t("report.empty")}
            textColumns={[1, 2, 4]}
            head={[t("moderation.name"), t("geography.city"), t("geography.categories"), t("moderation.followers"), t("moderation.status"), t("users.registered")]}
            rows={profiles.map((profile) => [
              <UserLink key="name" telegramUserId={profile.telegramUserId} name={profile.name} />,
              taxonomyLabel("city", profile.city, t),
              profile.categories.map((category) => taxonomyLabel("category", category, t)).join(", "),
              formatNumber(profile.totalFollowers),
              profileStatus(profile.status, t),
              formatTashkentDate(profile.createdAtUtc)
            ])}
          />
        )}
      </Report>
      {actions.dialog}
    </div>
  );
}

export function BusinessModerationPage() {
  const { t } = useI18n();
  const pending = useLoad(getPendingBusinesses, "pending-businesses");
  const all = useLoad(getBusinesses, "businesses");
  const actions = useActions(() => { pending.refresh(); all.refresh(); });
  return (
    <div className="space-y-6">
      {actions.status}
      <Report title={t("moderation.pendingTitle")} hint={t("moderation.pendingHint")} loaded={pending.state} onRetry={pending.reload}>
        {(profiles) => profiles.length === 0 ? <p className="text-sm text-brand-muted">{t("moderation.pendingEmpty")}</p> : (
          <ul className="space-y-3">
            {profiles.map((profile) => (
              <PendingItem
                key={profile.id}
                name={profile.name}
                details={[profile.city && taxonomyLabel("city", profile.city, t), formatTashkentDate(profile.createdAtUtc)].filter(Boolean).join(" · ")}
                onDecide={(decision) => actions.ask(decisionAction("business", profile.name, decision, () => moderateBusiness(profile.id, decision), t))}
              />
            ))}
          </ul>
        )}
      </Report>
      <Report title={t("moderation.businessesTitle")} hint={t("moderation.listHint")} loaded={all.state} onRetry={all.reload}>
        {(profiles) => (
          <Table
            caption={t("moderation.businessesTitle")}
            empty={t("report.empty")}
            textColumns={[1, 4]}
            head={[t("moderation.name"), t("geography.city"), t("geography.campaigns"), t("overview.completedDeals"), t("moderation.status"), t("users.registered")]}
            rows={profiles.map((profile) => [
              <UserLink key="name" telegramUserId={profile.telegramUserId} name={profile.name} />,
              profile.city ? taxonomyLabel("city", profile.city, t) : "–",
              formatNumber(profile.campaignsCount),
              formatNumber(profile.completedDealsCount),
              <span key="status">{profileStatus(profile.moderationStatus, t)}{profile.isHidden && <> <Badge>{t("user.hidden")}</Badge></>}{profile.isDeleted && <> <Badge>{t("users.deleted")}</Badge></>}</span>,
              formatTashkentDate(profile.createdAtUtc)
            ])}
          />
        )}
      </Report>
      {actions.dialog}
    </div>
  );
}

const campaignStatuses = [0, 1, 2, 3];
type CampaignChange = { status: number; isPromoted: boolean };

// Status and promotion inside the dialog; the page reads the choice when the admin confirms.
function CampaignForm({ campaign, onChange }: { campaign: AdminCampaign; onChange: (change: CampaignChange) => void }) {
  const { t } = useI18n();
  const [change, setChange] = useState<CampaignChange>({ status: campaign.status, isPromoted: campaign.isPromoted });
  const update = (next: CampaignChange) => {
    setChange(next);
    onChange(next);
  };
  return (
    <div className="space-y-3">
      <label className="grid gap-1 text-xs text-brand-muted">
        {t("moderation.status")}
        <select className="h-11 rounded-[var(--bb-radius-control)] border border-brand-line bg-brand-surface px-3 text-sm text-brand-ink" value={change.status} onChange={(event) => update({ ...change, status: Number(event.target.value) })}>
          {campaignStatuses.map((status) => <option key={status} value={status}>{t(`admin.campaignStatus.${status}`)}</option>)}
        </select>
      </label>
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" className="h-5 w-5" checked={change.isPromoted} onChange={(event) => update({ ...change, isPromoted: event.target.checked })} />
        {t("campaigns.promoted")}
      </label>
    </div>
  );
}

// Campaigns: status (draft, published, archived, rejected) and promotion. Only what changed is sent.
export function CampaignModerationPage() {
  const { t } = useI18n();
  const { state, reload, refresh } = useLoad(getCampaigns, "campaigns");
  const actions = useActions(refresh);
  const choice = useRef<CampaignChange | null>(null);

  const edit = (campaign: AdminCampaign) => {
    choice.current = { status: campaign.status, isPromoted: campaign.isPromoted };
    actions.ask({
      title: t("campaigns.editTitle", { title: campaign.title }),
      text: t("campaigns.editText"),
      confirmLabel: t("campaigns.save"),
      body: <CampaignForm campaign={campaign} onChange={(change) => { choice.current = change; }} />,
      run: async () => {
        const change = choice.current ?? { status: campaign.status, isPromoted: campaign.isPromoted };
        const request = {
          ...(change.status !== campaign.status ? { status: change.status } : {}),
          ...(change.isPromoted !== campaign.isPromoted ? { isPromoted: change.isPromoted } : {})
        };
        if (Object.keys(request).length > 0) await moderateCampaign(campaign.id, request);
      },
      done: t("campaigns.saved")
    });
  };

  return (
    <div className="space-y-6">
      {actions.status}
      <Report title={t("campaigns.title")} hint={t("campaigns.hint")} loaded={state} onRetry={reload}>
        {(campaigns) => (
          <Table
            caption={t("campaigns.title")}
            empty={t("report.empty")}
            textColumns={[1, 2, 6]}
            head={[t("campaigns.name"), t("deals.business"), t("moderation.status"), t("campaigns.applications"), t("users.registered"), t("campaigns.promoted"), t("campaigns.actions")]}
            rows={campaigns.map((campaign) => [
              campaign.title,
              <UserLink key="business" telegramUserId={campaign.businessTelegramUserId} name={campaign.businessName} />,
              t(`admin.campaignStatus.${campaign.status}`),
              formatNumber(campaign.applicationsCount),
              formatTashkentDate(campaign.createdAtUtc),
              campaign.isPromoted ? t("campaigns.yes") : "–",
              <Button key="edit" onClick={() => edit(campaign)}>{t("campaigns.edit")}</Button>
            ])}
          />
        )}
      </Report>
      {actions.dialog}
    </div>
  );
}
