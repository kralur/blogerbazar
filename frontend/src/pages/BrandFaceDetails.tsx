import { useCallback, useEffect, useRef, useState } from "react";
import { getBrandFace, type BrandFaceDetails as BrandFaceDetailsModel } from "../api/marketplace";
import { Avatar, BottomNav, Card, ErrorState, LoadingState } from "../components/ui";
import { ChipList, DetailSection, FactGrid } from "../components/details/DetailBlocks";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";
import { ContactList, hasContacts } from "../components/ContactList";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { FavoriteButton } from "../components/FavoriteButton";
import { getCachedPublicDetail, setCachedPublicDetail } from "../data/publicDetailCache";
import { PageHeader } from "../components/PageHeader";
import { useScreenRefresh } from "../hooks/useScreenRefresh";

export function BrandFaceDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  const [profile, setProfile] = useState<BrandFaceDetailsModel | null>(() => getCachedPublicDetail<BrandFaceDetailsModel>("brand-face", id));
  const [loading, setLoading] = useState(() => !getCachedPublicDetail<BrandFaceDetailsModel>("brand-face", id));
  const [failed, setFailed] = useState(false);
  const requestIdRef = useRef(0);
  const load = useCallback(() => {
    const cached = getCachedPublicDetail<BrandFaceDetailsModel>("brand-face", id);
    const requestId = ++requestIdRef.current;
    if (cached) setProfile(cached);
    else setProfile(null);
    setLoading(!cached);
    setFailed(false);
    getBrandFace(id).then((response) => {
      if (requestId !== requestIdRef.current) return;
      setCachedPublicDetail("brand-face", id, response);
      setProfile(response);
    }).catch(() => {
      if (requestId !== requestIdRef.current) return;
      setFailed(true);
      if (!cached) setProfile(null);
    }).finally(() => {
      if (requestId === requestIdRef.current) setLoading(false);
    });
  }, [id]);
  useEffect(() => {
    load();
    return () => { requestIdRef.current += 1; };
  }, [load]);
  useProfileDataRefresh(load);
  useScreenRefresh(load);

  if (loading) return <div className="screen screen--with-nav"><LoadingState title={t("common.loadingProfile")} /><BottomNav /></div>;
  if (!profile) return <div className="screen screen--with-nav"><ErrorState onRetry={load} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /><BottomNav /></div>;
  const contacts = [
    profile.telegram ? { kind: "telegram" as const, value: profile.telegram } : null,
    profile.instagram ? { kind: "instagram" as const, value: profile.instagram } : null,
    profile.portfolioUrl ? { kind: "website" as const, value: profile.portfolioUrl } : null
  ].filter((item): item is NonNullable<typeof item> => item !== null);
  return <div className="screen screen--with-nav">
    <PageHeader actions={<FavoriteButton brandFaceId={profile.id} />} back={{ href: "#/", label: t("common.back") }} />
    <div className="mt-4 text-center"><div className="mx-auto w-fit"><Avatar name={profile.name} size="xl" src={profile.avatarUrl} /></div><h1 className="mt-3 text-2xl font-extrabold tracking-tight">{profile.name}</h1><p className="mt-1 text-sm text-brand-muted">{t("onboarding.brandFace")} · {cityLabel(profile.city, language)}</p>{profile.isPromoted && <div className="mt-3 flex justify-center"><span className="catalog-card__promoted detail-promoted">{t("card.promoted")}</span></div>}</div>
    {failed && <p className="mt-3 text-sm text-brand-muted" role="status">{t("common.connectionRetry")}</p>}
    <FactGrid className="mt-5" facts={[
      { label: t("common.price"), value: profile.collaborationPrice ? formatCurrency(profile.collaborationPrice) : t("card.onRequest") },
      { label: t("brandFace.languages"), value: profile.languages.map((code) => code.toUpperCase()).join(" · ") || null }
    ]} />
    {profile.categories.length > 0 && <DetailSection title={t("common.categories")}><ChipList items={profile.categories.map((category) => categoryLabel(category, language))} /></DetailSection>}
    {(profile.description || profile.experience) && <DetailSection title={t("brandFace.aboutTitle")}><Card>{profile.description && <p className="text-sm leading-6 text-brand-muted">{profile.description}</p>}{profile.experience && <><h3 className={`${profile.description ? "mt-4 " : ""}text-sm font-extrabold`}>{t("brandFace.experienceTitle")}</h3><p className="mt-1 text-sm leading-6 text-brand-muted">{profile.experience}</p></>}</Card></DetailSection>}
    {hasContacts(contacts) && <DetailSection title={t("details.contacts")}><ContactList items={contacts} /></DetailSection>}
    <BottomNav />
  </div>;
}
