import { useCallback, useEffect, useState } from "react";
import { getBrandFace, type BrandFaceDetails as BrandFaceDetailsModel } from "../api/marketplace";
import { Avatar, Badge, BottomNav, Card, ErrorState, Icon, LoadingState, StatsCard } from "../components/ui";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";
import { ContactList, hasContacts } from "../components/ContactList";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";

export function BrandFaceDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  const [profile, setProfile] = useState<BrandFaceDetailsModel | null>(null);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const load = useCallback(() => {
    setLoading(true);
    setFailed(false);
    getBrandFace(id).then(setProfile).catch(() => { setProfile(null); setFailed(true); }).finally(() => setLoading(false));
  }, [id]);
  useEffect(() => { load(); }, [load]);
  useProfileDataRefresh(load);

  if (loading) return <div className="screen screen--with-nav"><LoadingState title={t("common.loadingProfile")} /><BottomNav /></div>;
  if (failed || !profile) return <div className="screen screen--with-nav"><ErrorState onRetry={load} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /><BottomNav /></div>;
  const contacts = [
    profile.telegram ? { kind: "telegram" as const, value: profile.telegram } : null,
    profile.instagram ? { kind: "instagram" as const, value: profile.instagram } : null,
    profile.portfolioUrl ? { kind: "website" as const, value: profile.portfolioUrl } : null
  ].filter((item): item is NonNullable<typeof item> => item !== null);
  return <div className="screen screen--with-nav pt-5">
    <div className="flex items-center justify-between gap-3"><a aria-label={t("common.back")} className="grid h-11 w-11 place-items-center rounded-2xl bg-white shadow-card" href="#/"><Icon name="back" /></a><LanguageSwitcher /></div>
    <Card className="mt-5 overflow-hidden bg-gradient-to-br from-blue-50 via-white to-cyan-50 text-center"><div className="mx-auto w-fit"><Avatar name={profile.name} size="xl" src={profile.avatarUrl} /></div><div className="mt-4 flex justify-center gap-2">{profile.isPromoted && <Badge tone="gold">{t("card.promoted")}</Badge>}<Badge tone="blue">{t("onboarding.brandFace")}</Badge></div><h1 className="mt-3 text-2xl font-extrabold">{profile.name}</h1><p className="mt-1 text-sm text-brand-muted">{cityLabel(profile.city, language)}</p></Card>
    <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("common.categories")}</h2><div className="flex flex-wrap gap-2">{profile.categories.map((category) => <Badge key={category} tone="blue">{categoryLabel(category, language)}</Badge>)}</div></section>
    <section className="mt-5 grid grid-cols-2 gap-2"><StatsCard icon="users" label={t("brandFace.languages")} value={profile.languages.join(" · ") || "—"} /><StatsCard icon="star" label={t("common.price")} value={profile.collaborationPrice ? formatCurrency(profile.collaborationPrice) : t("card.onRequest")} /></section>
    {(profile.description || profile.experience) && <Card className="mt-5"><h2 className="font-extrabold">{t("details.about")}</h2>{profile.description && <p className="mt-2 text-sm leading-6 text-brand-muted">{profile.description}</p>}{profile.experience && <><h3 className="mt-4 text-sm font-extrabold">{t("brandFace.experience")}</h3><p className="mt-1 text-sm leading-6 text-brand-muted">{profile.experience}</p></>}</Card>}
    {hasContacts(contacts) && <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("details.contacts")}</h2><ContactList items={contacts} /></section>}
    <BottomNav />
  </div>;
}
