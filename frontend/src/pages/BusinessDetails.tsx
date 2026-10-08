import { useCallback, useEffect, useRef, useState } from "react";
import { getPublicBusiness, type PublicBusinessProfile } from "../api/marketplace";
import { Avatar, BottomNav, Card, ErrorState, Icon, LoadingState, Rating } from "../components/ui";
import { DetailSection, FactGrid, ReviewList } from "../components/details/DetailBlocks";
import { cityLabel, useI18n } from "../i18n";
import { formatBudgetRange, formatShortDate } from "../lib/currency";
import { ContactList } from "../components/ContactList";
import { getCachedPublicDetail, setCachedPublicDetail } from "../data/publicDetailCache";
import { PageHeader } from "../components/PageHeader";
import { useScreenRefresh } from "../hooks/useScreenRefresh";

// Public business profile: who placed a campaign or sent an offer, its rating and open campaigns.
export function BusinessDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  const [profile, setProfile] = useState<PublicBusinessProfile | null>(() => getCachedPublicDetail<PublicBusinessProfile>("business", id));
  const [loading, setLoading] = useState(() => !getCachedPublicDetail<PublicBusinessProfile>("business", id));
  const [failed, setFailed] = useState(false);
  const requestIdRef = useRef(0);
  const load = useCallback(() => {
    const cached = getCachedPublicDetail<PublicBusinessProfile>("business", id);
    const requestId = ++requestIdRef.current;
    setProfile(cached);
    setLoading(!cached);
    setFailed(false);
    return getPublicBusiness(id).then((response) => {
      if (requestId !== requestIdRef.current) return;
      setCachedPublicDetail("business", id, response);
      setProfile(response);
    }).catch(() => {
      if (requestId !== requestIdRef.current) return;
      setFailed(true);
    }).finally(() => {
      if (requestId === requestIdRef.current) setLoading(false);
    });
  }, [id]);
  useEffect(() => {
    void load();
    return () => { requestIdRef.current += 1; };
  }, [load]);
  useScreenRefresh(load);

  if (loading) return <div className="screen screen--with-nav"><LoadingState title={t("common.loadingProfile")} /><BottomNav /></div>;
  if (!profile) return <div className="screen screen--with-nav"><ErrorState onRetry={() => void load()} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /><BottomNav /></div>;
  const website = profile.websiteUrl ? [{ kind: "website" as const, value: profile.websiteUrl }] : [];
  return <div className="screen screen--with-nav">
    <PageHeader back={{ href: "#/campaigns", label: t("common.back") }} />
    <div className="mt-4 text-center">
      <div className="mx-auto w-fit"><Avatar name={profile.name} size="xl" src={profile.logoUrl} /></div>
      <h1 className="mt-3 text-2xl font-extrabold tracking-tight">{profile.name}</h1>
      <p className="mt-1 text-sm text-brand-muted">{[t("common.business"), profile.city ? cityLabel(profile.city, language) : null].filter(Boolean).join(" · ")}</p>
      {profile.reviewsCount > 0 && <div className="mt-2 flex justify-center"><Rating count={profile.reviewsCount} value={profile.rating} /></div>}
    </div>
    {failed && <p className="mt-3 text-sm text-brand-muted" role="status">{t("common.connectionRetry")}</p>}
    <FactGrid className="mt-5" facts={[
      { label: t("company.completedDeals"), value: String(profile.completedDealsCount) },
      { label: t("company.onPlatformSince"), value: formatShortDate(profile.createdAtUtc, language, { year: true }) }
    ]} />
    {profile.description && <DetailSection title={t("company.about")}><Card><p className="whitespace-pre-line text-sm leading-6 text-brand-muted">{profile.description}</p></Card></DetailSection>}
    <DetailSection title={t("company.openCampaigns")}>
      {profile.openCampaigns.length === 0
        ? <Card><p className="text-sm text-brand-muted">{t("company.noOpenCampaigns")}</p></Card>
        : <div className="grid gap-2">{profile.openCampaigns.map((campaign) => <a className="company-campaign" href={`#/campaign/${campaign.id}`} key={campaign.id}>
          <span className="min-w-0"><strong>{campaign.title}</strong><span>{[campaign.city ? cityLabel(campaign.city, language) : null, formatBudgetRange(campaign.budgetFrom, campaign.budgetTo), campaign.deadline ? formatShortDate(campaign.deadline, language) : null].filter(Boolean).join(" · ")}</span></span>
          <Icon className="company-campaign__chevron" name="back" />
        </a>)}</div>}
    </DetailSection>
    <DetailSection title={t("campaign.businessReviews")}><ReviewList emptyText={t("campaign.noBusinessReviews")} reviewerRoute={(profileId) => `#/blogger/${profileId}`} reviews={profile.reviews ?? []} /></DetailSection>
    {website.length > 0 && <DetailSection title={t("company.website")}><ContactList items={website} /></DetailSection>}
    <BottomNav />
  </div>;
}
