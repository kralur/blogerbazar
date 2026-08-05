import { useEffect, useState } from "react";
import { applyToCampaign, getCampaign, getPublicContact, type CampaignDetails, type ContactDetails } from "../api/marketplace";
import { Badge, BottomNav, Button, Card, ErrorState, Icon, LoadingState, Modal, Textarea, Toast } from "../components/ui";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";
import { ContactList, hasContacts } from "../components/ContactList";

export function CampaignDetails({ id }: { id: string }) {
  const { language, t } = useI18n();
  const [campaign, setCampaign] = useState<CampaignDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [contact, setContact] = useState<ContactDetails | null>(null);
  const [applicationOpen, setApplicationOpen] = useState(false);
  const [applicationMessage, setApplicationMessage] = useState("");
  const [applying, setApplying] = useState(false);
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error">("success");

  const loadCampaign = () => {
    setLoading(true);
    setFailed(false);
    getCampaign(id).then(setCampaign).catch(() => { setCampaign(null); setFailed(true); }).finally(() => setLoading(false));
  };

  useEffect(loadCampaign, [id]);

  useEffect(() => {
    const businessId = campaign?.businessId;
    if (!businessId) return;

    getPublicContact("Business", businessId)
      .then(setContact)
      .catch(() => undefined);
  }, [campaign?.businessId, id]);

  const apply = async () => {
    try {
      setApplying(true);
      await applyToCampaign(id, applicationMessage.trim() || t("campaign.defaultApplicationMessage"));
      setApplicationOpen(false);
      setApplicationMessage("");
      setToastTone("success");
      setToast(t("campaign.applicationSent"));
    } catch {
      setToastTone("error");
      setToast(t("campaign.applicationFailed"));
    } finally {
      setApplying(false);
    }
  };


  if (loading) return <div className="screen"><LoadingState title={t("campaign.loading")} /></div>;
  if (failed || !campaign) return <div className="screen"><ErrorState onRetry={loadCampaign} subtitle={t("common.connectionRetry")} title={t("common.openFailed")} /></div>;

  const contacts = [
    contact?.phone ? { kind: "phone" as const, value: contact.phone } : null,
    contact?.telegram ? { kind: "telegram" as const, value: contact.telegram } : null,
    contact?.websiteUrl ? { kind: "website" as const, value: contact.websiteUrl } : null,
    contact?.email ? { kind: "email" as const, value: contact.email } : null
  ].filter((item): item is NonNullable<typeof item> => item !== null);
  return (
    <div className="screen pb-36">
      <header className="flex items-center justify-between">
        <a className="grid h-11 w-11 place-items-center rounded-2xl bg-white shadow-card" href="#/campaigns"><Icon name="back" /></a>
        <Badge tone={campaign.isPromoted ? "gold" : "blue"}>{campaign.isPromoted ? t("campaign.promoted") : t("campaign.open")}</Badge>
      </header>
      <Card className="mt-5 overflow-hidden p-0">
        <div className="bg-gradient-to-br from-blue-600 via-blue-500 to-cyan-400 p-5 text-white">
          <div className="flex items-center gap-3"><span className="grid h-12 w-12 place-items-center rounded-2xl bg-white/20 font-extrabold">{campaign.logo}</span><div><p className="text-sm text-white/75">{campaign.company}</p><h1 className="mt-1 text-2xl font-extrabold leading-7">{campaign.title}</h1></div></div>
        </div>
        <div className="p-5"><p className="text-sm leading-6 text-brand-muted">{campaign.description}</p><div className="mt-5 grid grid-cols-2 gap-2"><div className="rounded-2xl bg-slate-50 p-3"><p className="text-xs text-brand-muted">{t("common.budget")}</p><p className="mt-1 text-sm font-extrabold">{formatCurrency(campaign.budgetFrom)}–{formatCurrency(campaign.budgetTo)}</p></div><div className="rounded-2xl bg-slate-50 p-3"><p className="text-xs text-brand-muted">{t("campaign.location")}</p><p className="mt-1 text-sm font-extrabold">{cityLabel(campaign.city, language)}</p></div></div></div>
      </Card>
      <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("campaign.suitable")}</h2><div className="flex flex-wrap gap-2">{campaign.categories.map((category) => <Badge key={category} tone="blue">{categoryLabel(category, language)}</Badge>)}</div></section>
      <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("common.requirements")}</h2><Card><ul className="grid gap-3">{campaign.requirements.length ? campaign.requirements.map((item) => <li className="flex gap-2 text-sm text-brand-muted" key={item}><Icon className="h-4 w-4 shrink-0 text-brand-success" name="check" />{item}</li>) : <li className="text-sm text-brand-muted">{t("common.noData")}</li>}</ul></Card></section>
      {hasContacts(contacts) && <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("campaign.businessContact")}</h2><ContactList items={contacts} /></section>}
      <div className="fixed inset-x-0 bottom-[70px] z-30 mx-auto max-w-[430px] bg-white/90 px-5 pb-3 pt-2 backdrop-blur"><Button className="w-full" onClick={() => setApplicationOpen(true)}><Icon name="send" />{t("campaign.apply")}</Button></div>
      <Modal onClose={() => setApplicationOpen(false)} open={applicationOpen} title={t("campaign.applyTitle")}><p className="text-sm leading-6 text-brand-muted">{t("campaign.applyDescription")}</p><Textarea className="mt-4" maxLength={1000} onChange={(event) => setApplicationMessage(event.target.value)} placeholder={t("campaign.applyPlaceholder")} value={applicationMessage} /><Button className="mt-4 w-full" disabled={applying} onClick={apply}>{applying ? t("campaign.sending") : t("campaign.submitApplication")}</Button></Modal>
      <Toast message={toast} tone={toastTone} /><BottomNav />
    </div>
  );
}
