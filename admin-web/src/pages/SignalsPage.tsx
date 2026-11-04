import { Fragment, type ReactNode } from "react";
import { getSignals, type AdminSignals, type SignalPerson } from "../api/admin";
import { UserLink } from "../components/Link";
import { Report } from "../components/Report";
import { Badge, Card, Table } from "../components/ui";
import { useI18n } from "../i18n";
import { formatNumber } from "../lib/format";
import { formatTashkentDateTime } from "../lib/time";
import { useLoad, type Loaded } from "../lib/useLoad";

// D59: patterns fake accounts and rating farming leave in the data. Hints only: nothing is blocked automatically,
// each name opens the person's card, where the owner can look closer and act.
export function SignalsPage() {
  const { t } = useI18n();
  const { state, reload } = useLoad(getSignals, "signals");
  return (
    <div className="space-y-6">
      <Card><p className="text-sm leading-6 text-brand-muted">{t("signals.intro")}</p></Card>
      <Section loaded={state} onRetry={reload} title={t("signals.handles.title")} hint={() => t("signals.handles.hint")}>
        {(data) => (
          <Table caption={t("signals.handles.title")} empty={t("signals.empty")} textColumns={[0, 1]}
            head={[t("signals.handle"), t("signals.people")]}
            rows={data.duplicateHandles.map((row) => [<span key="handle" className="break-all">{row.handle}</span>, <People key="people" people={row.people} />])} />
        )}
      </Section>
      <Section loaded={state} onRetry={reload} title={t("signals.phones.title")} hint={() => t("signals.phones.hint")}>
        {(data) => (
          <Table caption={t("signals.phones.title")} empty={t("signals.empty")} textColumns={[0, 1]}
            head={[t("signals.phone"), t("signals.people")]}
            rows={data.sharedPhones.map((row) => [row.phone, <People key="people" people={row.people} />])} />
        )}
      </Section>
      <Section loaded={state} onRetry={reload} title={t("signals.deals.title")} hint={(data) => t("signals.deals.hint", { minutes: data.rules.quickDealMinutes, days: data.rules.quickDealDays })}>
        {(data) => (
          <Table caption={t("signals.deals.title")} empty={t("signals.empty")} textColumns={[0, 1, 2]}
            head={[t("signals.deal"), t("signals.business"), t("signals.creator"), t("signals.minutes"), t("signals.reviews"), t("signals.completed")]}
            rows={data.quickDeals.map((row) => [
              row.title ?? t("deals.offer"),
              <UserLink key="business" telegramUserId={row.business.telegramUserId} name={row.business.name} />,
              <UserLink key="creator" telegramUserId={row.creator.telegramUserId} name={row.creator.name} />,
              formatNumber(row.minutes),
              formatNumber(row.reviews),
              formatTashkentDateTime(row.completedAtUtc)
            ])} />
        )}
      </Section>
      <Section loaded={state} onRetry={reload} title={t("signals.complaints.title")} hint={(data) => t("signals.complaints.hint", { reporters: data.rules.complaintReporters, days: data.rules.complaintDays })}>
        {(data) => (
          <Table caption={t("signals.complaints.title")} empty={t("signals.empty")} textColumns={[0]}
            head={[t("signals.target"), t("signals.reporters"), t("signals.last")]}
            rows={data.complaintTargets.map((row) => [
              // The name opens the card of the person behind the profile or campaign.
              <span key="target">{t(`admin.complaintTarget.${row.targetType}`)}: <UserLink telegramUserId={row.ownerTelegramUserId} name={row.targetName ?? row.targetId.slice(0, 8)} /></span>,
              formatNumber(row.reporters),
              formatTashkentDateTime(row.lastAtUtc)
            ])} />
        )}
      </Section>
      <Section loaded={state} onRetry={reload} title={t("signals.bursts.title")} hint={(data) => t("signals.bursts.hint", { accounts: data.rules.burstAccounts, days: data.rules.burstDays, percent: data.rules.burstLowProfilePercent })}>
        {(data) => (
          <Table caption={t("signals.bursts.title")} empty={t("signals.empty")} textColumns={[0]}
            head={[t("signals.source"), t("signals.hour"), t("signals.accounts"), t("signals.withProfile")]}
            rows={data.signupBursts.map((row) => [
              row.source,
              formatTashkentDateTime(row.hourUtc),
              formatNumber(row.accounts),
              <span key="profiles">{formatNumber(row.withProfile)}{row.lowProfiles && <> <Badge tone="warning">{t("signals.lowProfiles")}</Badge></>}</span>
            ])} />
        )}
      </Section>
    </div>
  );
}

function Section({ loaded, onRetry, title, hint, children }: { loaded: Loaded<AdminSignals>; onRetry: () => void; title: string; hint: (data: AdminSignals) => string; children: (data: AdminSignals) => ReactNode }) {
  return (
    <Report title={title} hint={loaded.status === "ready" ? hint(loaded.data) : undefined} loaded={loaded} onRetry={onRetry}>
      {children}
    </Report>
  );
}

function People({ people }: { people: SignalPerson[] }) {
  return (
    <span>
      {people.map((person, index) => (
        <Fragment key={person.telegramUserId}>
          {index > 0 && ", "}
          <UserLink telegramUserId={person.telegramUserId} name={person.name || String(person.telegramUserId)} />
        </Fragment>
      ))}
    </span>
  );
}
