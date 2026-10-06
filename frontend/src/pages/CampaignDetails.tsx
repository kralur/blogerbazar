import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../api/client";
import { applyToCampaign, getBusinessReviews, getCampaign, getCurrentPlatformUser, getMyBloggerProfile, getMyBusinessProfile, getMyCampaignApplicationsPage, getPublicContact, normalizeMarketplaceRole, type BusinessReviews, type CampaignDetails, type ContactDetails } from "../api/marketplace";
import { Avatar, Badge, BottomNav, Button, Card, ErrorState, FixedActionBar, Icon, LoadingState, Modal, Rating, Textarea, Toast } from "../components/ui";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";
import { ContactList, hasContacts } from "../components/ContactList";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { getCachedPublicDetail, setCachedPublicDetail } from "../data/publicDetailCache";
import { getCachedCampaignApplication, setCachedCampaignApplication } from "../data/campaignApplicationCache";
import { campaignApplicationStatusLabelKey, campaignApplicationStatusTone } from "../lib/campaignApplicationStatus";

export function CampaignDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  const [campaign, setCampaign] = useState<CampaignDetails | null>(() => getCachedPublicDetail<CampaignDetails>("campaign", id));
  const [loading, setLoading] = useState(() => !getCachedPublicDetail<CampaignDetails>("campaign", id));
  const [failed, setFailed] = useState(false);
  const [contact, setContact] = useState<ContactDetails | null>(null);
  const [businessReviews, setBusinessReviews] = useState<BusinessReviews | null>(null);
  const [applicationOpen, setApplicationOpen] = useState(false);
  const [applicationMessage, setApplicationMessage] = useState("");
  const [applying, setApplying] = useState(false);
  const [canApply, setCanApply] = useState(false);
  const [application, setApplication] = useState(() => getCachedCampaignApplication(id));
  const [applicationLookupFailed, setApplicationLookupFailed] = useState(false);
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error" | "info">("success");

  const requestIdRef = useRef(0);
  const applicationRequestRef = useRef(0);
  const applicationMutationRef = useRef(0);
  const applyingRef = useRef(false);
  const currentCampaignIdRef = useRef(id);
  currentCampaignIdRef.current = id;
  const loadCampaign = useCallback(() => {
    const cached = getCachedPublicDetail<CampaignDetails>("campaign", id);
    const requestId = ++requestIdRef.current;
    if (cached) setCampaign(cached);
    else setCampaign(null);
    setLoading(!cached);
    setFailed(false);
    getCampaign(id).then((response) => {
      if (requestId !== requestIdRef.current) return;
      setCachedPublicDetail("campaign", id, response);
      setCampaign(response);
    }).catch(() => {
      if (requestId !== requestIdRef.current) return;
      setFailed(true);
      if (!cached) setCampaign(null);
    }).finally(() => {
      if (requestId === requestIdRef.current) setLoading(false);
    });
  }, [id]);

  useEffect(() => {
    loadCampaign();
    return () => { requestIdRef.current += 1; };
  }, [loadCampaign]);

  useEffect(() => {
    const businessId = campaign?.businessId;
    if (!businessId) return;

    getPublicContact("Business", businessId)
      .then(setContact)
      .catch(() => undefined);
  }, [campaign?.businessId, id]);

  useEffect(() => {
    const businessId = campaign?.businessId;
    setBusinessReviews(null);
    if (!businessId) return;

    const controller = new AbortController();
    getBusinessReviews(businessId, controller.signal)
      .then(setBusinessReviews)
      .catch(() => undefined);
    return () => controller.abort();
  }, [campaign?.businessId]);

  useEffect(() => {
    if (!campaign || campaign.id !== id) {
      applicationRequestRef.current += 1;
      applicationMutationRef.current += 1;
      setCanApply(false);
      setApplication(null);
      setApplicationLookupFailed(false);
      return;
    }

    const campaignId = id;
    const requestId = ++applicationRequestRef.current;
    const lookupController = new AbortController();
    let cancelled = false;
    Promise.allSettled([getCurrentPlatformUser(), getMyBloggerProfile(), getMyBusinessProfile()]).then(([userResult, bloggerResult, businessResult]) => {
      if (cancelled || requestId !== applicationRequestRef.current || currentCampaignIdRef.current !== campaignId) return;
      const activeRole = userResult.status === "fulfilled" ? normalizeMarketplaceRole(userResult.value.selectedMarketplaceRole) : undefined;
      const bloggerIsApproved = bloggerResult.status === "fulfilled" && bloggerResult.value.status === 1;
      const isOwnCampaign = businessResult.status === "fulfilled" && businessResult.value.id === campaign.businessId;
      const eligible = activeRole === "Blogger" && bloggerIsApproved && campaign.status === 1 && !isOwnCampaign;
      setCanApply(eligible);
      setApplicationLookupFailed(false);
      if (!eligible) { setApplication(null); return; }
      const cachedApplication = getCachedCampaignApplication(campaignId);
      if (cachedApplication) setApplication(cachedApplication);
      else setApplication(null);
      getMyCampaignApplicationsPage({ campaignId, page: 1, pageSize: 1 }, lookupController.signal).then((page) => {
        if (cancelled || requestId !== applicationRequestRef.current || currentCampaignIdRef.current !== campaignId) return;
        const existing = page.items[0] ? { id: page.items[0].id, status: page.items[0].status } : null;
        if (existing) setCachedCampaignApplication(campaignId, existing);
        setApplication(existing);
      }).catch(() => {
        if (!cancelled && !lookupController.signal.aborted && requestId === applicationRequestRef.current && currentCampaignIdRef.current === campaignId) {
          setApplication(null);
          setApplicationLookupFailed(true);
        }
      });
    });

    return () => {
      cancelled = true;
      lookupController.abort();
      applicationRequestRef.current += 1;
      applicationMutationRef.current += 1;
      applyingRef.current = false;
    };
  }, [campaign, id]);

  const reconcileApplication = useCallback(async (campaignId: string, mutationId: number, signal: AbortSignal) => {
    const page = await getMyCampaignApplicationsPage({ campaignId, page: 1, pageSize: 1 }, signal);
    if (mutationId !== applicationMutationRef.current || currentCampaignIdRef.current !== campaignId) return null;
    const existing = page.items[0] ? { id: page.items[0].id, status: page.items[0].status } : null;
    if (existing) setCachedCampaignApplication(campaignId, existing);
    setApplication(existing);
    return existing;
  }, []);

  const apply = async () => {
    if (applyingRef.current) return;
    applyingRef.current = true;
    const campaignId = id;
    const mutationId = ++applicationMutationRef.current;
    const controller = new AbortController();
    try {
      setApplying(true);
      const result = await applyToCampaign(campaignId, applicationMessage.trim() || t("campaign.defaultApplicationMessage"), controller.signal);
      if (mutationId !== applicationMutationRef.current || currentCampaignIdRef.current !== campaignId) return;
      const existing = { id: result.id, status: result.status };
      setCachedCampaignApplication(campaignId, existing);
      setApplication(existing);
      setApplicationOpen(false);
      setApplicationMessage("");
      setCanApply(false);
      setToastTone("success");
      setToast(t("campaign.applicationSent"));
    } catch (error) {
      if (mutationId !== applicationMutationRef.current || currentCampaignIdRef.current !== campaignId || controller.signal.aborted) return;
      if (error instanceof ApiError && error.status === 409) {
        try {
          const existing = await reconcileApplication(campaignId, mutationId, controller.signal);
          if (mutationId !== applicationMutationRef.current || currentCampaignIdRef.current !== campaignId) return;
          if (existing) {
            setApplicationOpen(false);
            setToastTone("info");
            setToast(t("applications.applyConflict"));
            return;
          }
        } catch {}
      }
      setToastTone("error");
      setToast(t("campaign.applicationFailed"));
    } finally {
      if (mutationId === applicationMutationRef.current && currentCampaignIdRef.current === campaignId) {
        applyingRef.current = false;
        setApplying(false);
      }
    }
  };


  if (loading) return <div className="screen screen--with-nav"><LoadingState title={t("campaign.loading")} /><BottomNav /></div>;
  if (!campaign) return <div className="screen screen--with-nav"><ErrorState onRetry={loadCampaign} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /><BottomNav /></div>;

  const contacts = [
    contact?.phone ? { kind: "phone" as const, value: contact.phone } : null,
    contact?.telegram ? { kind: "telegram" as const, value: contact.telegram } : null,
    contact?.websiteUrl ? { kind: "website" as const, value: contact.websiteUrl } : null,
    contact?.email ? { kind: "email" as const, value: contact.email } : null
  ].filter((item): item is NonNullable<typeof item> => item !== null);
  const budget = campaign.budgetFrom != null && campaign.budgetTo != null
    ? `${formatCurrency(campaign.budgetFrom)}–${formatCurrency(campaign.budgetTo)}`
    : campaign.budgetFrom != null ? formatCurrency(campaign.budgetFrom)
      : campaign.budgetTo != null ? formatCurrency(campaign.budgetTo)
        : null;
  const companyInitials = campaign.company.split(" ").map((part) => part[0]).join("").slice(0, 2).toUpperCase();
  return (
    <div className="screen screen--with-nav">
      <header className="flex items-center justify-between">
        <a className="grid h-11 w-11 place-items-center rounded-2xl bg-white shadow-card" href="#/campaigns"><Icon name="back" /></a>
        <div className="flex items-center gap-2"><Badge tone={campaign.isPromoted ? "gold" : "blue"}>{campaign.isPromoted ? t("campaign.promoted") : t("campaign.open")}</Badge><LanguageSwitcher /></div>
      </header>
      <Card className="mt-5 overflow-hidden p-0">
        <div className="bg-gradient-to-br from-blue-600 via-blue-500 to-cyan-400 p-5 text-white">
          <div className="flex items-center gap-3"><Avatar name={companyInitials} size="sm" variant="neutral" /><div><p className="text-sm text-white/75">{campaign.company}</p><h1 className="mt-1 text-2xl font-extrabold leading-7">{campaign.title}</h1></div></div>
        </div>
        <div className="p-5"><p className="text-sm leading-6 text-brand-muted">{campaign.description}</p>{(budget || campaign.city) && <div className="mt-5 grid grid-cols-2 gap-2">{budget && <div className="rounded-2xl bg-slate-50 p-3"><p className="text-xs text-brand-muted">{t("common.budget")}</p><p className="mt-1 text-sm font-extrabold">{budget}</p></div>}{campaign.city && <div className="rounded-2xl bg-slate-50 p-3"><p className="text-xs text-brand-muted">{t("campaign.location")}</p><p className="mt-1 text-sm font-extrabold">{cityLabel(campaign.city, language)}</p></div>}</div>}</div>
      </Card>
      {failed && <p className="mt-3 text-sm text-brand-muted" role="status">{t("common.connectionRetry")}</p>}
      <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("campaign.suitable")}</h2><div className="flex flex-wrap gap-2">{campaign.categories.map((category) => <Badge key={category} tone="blue">{categoryLabel(category, language)}</Badge>)}</div></section>
      <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("common.requirements")}</h2><Card><ul className="grid gap-3">{campaign.requirements.length ? campaign.requirements.map((item) => <li className="flex gap-2 text-sm text-brand-muted" key={item}><Icon className="h-4 w-4 shrink-0 text-brand-success" name="check" />{item}</li>) : <li className="text-sm text-brand-muted">{t("common.noData")}</li>}</ul></Card></section>
      {businessReviews && <section className="mt-5"><div className="mb-3 flex items-center justify-between gap-3"><h2 className="font-extrabold">{t("campaign.businessReviews")}</h2>{businessReviews.reviewsCount > 0 && <Rating count={businessReviews.reviewsCount} value={businessReviews.rating} />}</div>{businessReviews.items.length ? <div className="grid gap-2">{businessReviews.items.map((review) => <Card className="p-3" key={review.id}><div className="flex items-center justify-between"><Rating value={review.rating} /><span className="text-xs text-brand-muted">{new Intl.DateTimeFormat(language === "uz" ? "uz-UZ" : "ru-RU", { day: "numeric", month: "short", year: "numeric" }).format(new Date(review.createdAtUtc))}</span></div>{review.reviewerName && <p className="mt-2 text-sm font-bold">{review.reviewerName}</p>}{review.comment && <p className="mt-1 text-sm leading-5 text-brand-muted">{review.comment}</p>}</Card>)}</div> : <Card><p className="text-sm text-brand-muted">{t("campaign.noBusinessReviews")}</p></Card>}</section>}
      {hasContacts(contacts) && <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("campaign.businessContact")}</h2><ContactList items={contacts} /></section>}
      {applicationLookupFailed && canApply && <p className="mt-4 text-sm text-brand-muted" role="status">{t("applications.applyLookupFailed")}</p>}
      {application ? <FixedActionBar><a aria-label={t("applications.applyState")} className="ds-button ds-button--secondary w-full" href={`#/my-application/${application.id}`}><Badge tone={campaignApplicationStatusTone(application.status)}>{t(campaignApplicationStatusLabelKey(application.status))}</Badge>{t("applications.applyState")}</a></FixedActionBar> : canApply && !applicationLookupFailed ? <FixedActionBar><Button className="w-full" onClick={() => setApplicationOpen(true)}><Icon name="send" />{t("campaign.apply")}</Button></FixedActionBar> : null}
      <Modal onClose={() => setApplicationOpen(false)} open={applicationOpen} title={t("campaign.applyTitle")}><p className="text-sm leading-6 text-brand-muted">{t("campaign.applyDescription")}</p><Textarea className="mt-4" maxLength={1000} onChange={(event) => setApplicationMessage(event.target.value)} placeholder={t("campaign.applyPlaceholder")} value={applicationMessage} /><Button className="mt-4 w-full" disabled={applying} onClick={apply}>{applying ? t("campaign.sending") : t("campaign.submitApplication")}</Button></Modal>
      <Toast message={toast} tone={toastTone} /><BottomNav />
    </div>
  );
}
