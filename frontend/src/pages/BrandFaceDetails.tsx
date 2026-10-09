import { useCallback, useEffect, useRef, useState } from "react";
import { getBrandFace, getBrandFaceReviews, getCurrentPlatformUser, normalizeMarketplaceRole, type MarketplaceRole, type BrandFaceDetails as BrandFaceDetailsModel, type BusinessReviews } from "../api/marketplace";
import { Avatar, BottomNav, Button, Card, ErrorState, FixedActionBar, Icon, LoadingState, Rating, Toast } from "../components/ui";
import { OfferForm } from "../components/OfferForm";
import { ChipList, DetailSection, FactGrid, ReviewsSection, reviewCarouselLimit } from "../components/details/DetailBlocks";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";
import { ContactList, hasContacts } from "../components/ContactList";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { FavoriteButton } from "../components/FavoriteButton";
import { getCachedPublicDetail, setCachedPublicDetail } from "../data/publicDetailCache";
import { PageHeader } from "../components/PageHeader";
import { useScreenRefresh } from "../hooks/useScreenRefresh";
import { safeExternalUrl, socialUrl } from "../lib/contacts";
import { useTelegram } from "../telegram/TelegramProvider";
import { spokenLanguageLabelKey } from "../lib/languages";

export function BrandFaceDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  const { openLink } = useTelegram();
  const [profile, setProfile] = useState<BrandFaceDetailsModel | null>(() => getCachedPublicDetail<BrandFaceDetailsModel>("brand-face", id));
  const [loading, setLoading] = useState(() => !getCachedPublicDetail<BrandFaceDetailsModel>("brand-face", id));
  const [failed, setFailed] = useState(false);
  const [reviews, setReviews] = useState<BusinessReviews | null>(null);
  const [role, setRole] = useState<MarketplaceRole>();
  const [offerOpen, setOfferOpen] = useState(false);
  const [toast, setToast] = useState("");
  // A business offers a brand face cooperation the same way it offers a blogger (D48).
  useEffect(() => {
    let active = true;
    getCurrentPlatformUser().then((user) => { if (active) setRole(normalizeMarketplaceRole(user.selectedMarketplaceRole)); }).catch(() => undefined);
    return () => { active = false; };
  }, []);
  const requestIdRef = useRef(0);
  const load = useCallback(() => {
    const cached = getCachedPublicDetail<BrandFaceDetailsModel>("brand-face", id);
    const requestId = ++requestIdRef.current;
    if (cached) setProfile(cached);
    else setProfile(null);
    setLoading(!cached);
    setFailed(false);
    // D46: a brand face is reviewed after a completed deal, like a blogger.
    getBrandFaceReviews(id, undefined, { take: reviewCarouselLimit }).then((response) => {
      if (requestId === requestIdRef.current) setReviews(response);
    }).catch(() => undefined);
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
  // What a business looks at first (QA Q20): photos, Instagram, gender and age, formats, a showreel.
  const instagramHref = profile.instagram ? safeExternalUrl(socialUrl("instagram", profile.instagram)) : null;
  const showreelHref = profile.showreelUrl ? safeExternalUrl(profile.showreelUrl) : null;
  const photos = profile.photoUrls ?? [];
  const identity = [
    profile.gender === "female" || profile.gender === "male" ? t(`brandFace.person.${profile.gender}`) : null,
    profile.age ? t("brandFace.ageYears", { count: profile.age }) : null,
    cityLabel(profile.city, language)
  ].filter(Boolean).join(" · ");
  const open = (href: string) => { if (openLink) openLink(href); else window.open(href, "_blank", "noopener,noreferrer"); };
  return <div className="screen screen--with-nav">
    <PageHeader actions={<FavoriteButton brandFaceId={profile.id} />} back={{ href: "#/", label: t("common.back") }} />
    <div className="mt-4 text-center"><div className="mx-auto w-fit"><Avatar name={profile.name} size="xl" src={profile.avatarUrl} /></div><h1 className="mt-3 text-2xl font-extrabold tracking-tight">{profile.name}</h1><p className="mt-1 text-sm text-brand-muted">{t("onboarding.brandFace")} · {identity}</p>{reviews && reviews.reviewsCount > 0 && <div className="mt-2"><Rating count={reviews.reviewsCount} value={reviews.rating ?? undefined} /></div>}{profile.isPromoted && <div className="mt-3 flex justify-center"><span className="catalog-card__promoted detail-promoted">{t("card.promoted")}</span></div>}</div>
    {failed && <p className="mt-3 text-sm text-brand-muted" role="status">{t("common.connectionRetry")}</p>}
    {photos.length > 0 && <ul aria-label={t("brandFace.galleryTitle")} className="brand-face-photos no-scrollbar">{photos.map((url) => <li className="brand-face-photos__item" key={url}><img alt={profile.name} loading="lazy" src={url} /></li>)}</ul>}
    {instagramHref && <Button className="brand-face-instagram" onClick={() => open(instagramHref)} type="button"><Icon name="instagram" />{t("brandFace.openInstagram")}</Button>}
    {showreelHref && <Button className="mt-2 w-full" onClick={() => open(showreelHref)} type="button" variant="secondary"><Icon name="link" />{t("brandFace.watchShowreel")}</Button>}
    <FactGrid className="mt-5" facts={[
      { label: t("common.price"), value: profile.collaborationPrice ? formatCurrency(profile.collaborationPrice) : t("card.onRequest") },
      { label: t("brandFace.languages"), value: profile.languages.map((code) => spokenLanguageLabelKey(code) ? t(spokenLanguageLabelKey(code)!) : code).join(" · ") || null }
    ]} />
    {(profile.formats?.length ?? 0) > 0 && <DetailSection title={t("brandFace.formats")}><ChipList items={(profile.formats ?? []).map((format) => t(`brandFace.format.${format}`))} /></DetailSection>}
    {profile.categories.length > 0 && <DetailSection title={t("common.categories")}><ChipList items={profile.categories.map((category) => categoryLabel(category, language))} /></DetailSection>}
    {(profile.description || profile.experience) && <DetailSection title={t("brandFace.aboutTitle")}><Card>{profile.description && <p className="text-sm leading-6 text-brand-muted">{profile.description}</p>}{profile.experience && <><h3 className={`${profile.description ? "mt-4 " : ""}text-sm font-extrabold`}>{t("brandFace.experienceTitle")}</h3><p className="mt-1 text-sm leading-6 text-brand-muted">{profile.experience}</p></>}</Card></DetailSection>}
    {reviews && <ReviewsSection allHref={`#/brand-face-reviews/${profile.id}`} count={reviews.reviewsCount} emptyText={t("details.noReviews")} rating={reviews.reviewsCount > 0 ? reviews.rating : null} reviewerRoute={(profileId) => `#/company/${profileId}`} reviews={reviews.items} title={t("details.reviews")} />}
    {hasContacts(contacts) && <DetailSection title={t("details.contacts")}><ContactList items={contacts} /></DetailSection>}
    {role === "Business" && <FixedActionBar><Button className="w-full" onClick={() => setOfferOpen(true)} type="button"><Icon name="send" />{t("offers.propose")}</Button></FixedActionBar>}
    <OfferForm brandFaceId={profile.id} onClose={() => setOfferOpen(false)} onSent={() => { setOfferOpen(false); setToast(t("offers.sent")); }} open={offerOpen} />
    <Toast message={toast} />
    <BottomNav />
  </div>;
}
