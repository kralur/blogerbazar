import { useEffect, useState } from "react";
import { applyToCampaign, createContactUnlockInvoice, createContactUnlockOrder, getCampaign, getContactUnlockStatus, getUnlockedContact, type CampaignDetails, type ContactDetails } from "../api/marketplace";
import { openTelegramInvoice } from "../api/client";
import { Badge, BottomNav, Button, Card, Icon, Modal, Textarea, Toast, UnlockBlock } from "../components/ui";
import { useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";

export function CampaignDetails({ id }: { id: string }) {
  const { t } = useI18n();
  const [campaign, setCampaign] = useState<CampaignDetails | null>(null);
  const [contact, setContact] = useState<ContactDetails | null>(null);
  const [unlocking, setUnlocking] = useState(false);
  const [applicationOpen, setApplicationOpen] = useState(false);
  const [applicationMessage, setApplicationMessage] = useState("");
  const [applying, setApplying] = useState(false);
  const [toast, setToast] = useState("");

  useEffect(() => {
    getCampaign(id).then(setCampaign).catch(() => setCampaign(null));
  }, [id]);

  useEffect(() => {
    const businessId = campaign?.businessId;
    if (!businessId) return;

    getContactUnlockStatus("Business", businessId)
      .then((status) => status.isUnlocked ? getUnlockedContact("Business", businessId).then(setContact) : undefined)
      .catch(() => undefined);
  }, [campaign?.businessId, id]);

  const apply = async () => {
    try {
      setApplying(true);
      await applyToCampaign(id, applicationMessage.trim() || "Готов(а) обсудить интеграцию");
      setApplicationOpen(false);
      setApplicationMessage("");
      setToast(t("campaign.applicationSent"));
    } catch {
      setToast(t("campaign.applicationFailed"));
    } finally {
      setApplying(false);
    }
  };

  const unlockContact = async () => {
    const businessId = campaign?.businessId;
    if (!businessId) {
      setToast(t("campaign.paymentUnavailable"));
      return;
    }

    try {
      setUnlocking(true);
      const order = await createContactUnlockOrder("Business", businessId);
      if (order.isUnlocked) {
        setContact(await getUnlockedContact("Business", businessId));
        setToast(t("campaign.contactUnlocked"));
        return;
      }

      const invoice = await createContactUnlockInvoice(order.reference);
      const status = await openTelegramInvoice(invoice.invoiceLink);
      if (status === "paid") {
        setToast("Платёж принят. Контакты откроются после подтверждения.");
        window.setTimeout(() => {
          getContactUnlockStatus("Business", businessId)
            .then((result) => result.isUnlocked ? getUnlockedContact("Business", businessId).then(setContact) : undefined)
            .catch(() => undefined);
        }, 1200);
      } else if (status === "pending") {
        setToast("Платёж обрабатывается.");
      } else {
        setToast("Оплата не завершена.");
      }
    } catch {
      setToast(t("campaign.invoiceFailed"));
    } finally {
      setUnlocking(false);
    }
  };

  if (!campaign) return <div className="screen"><Card>{t("campaign.loading")}</Card></div>;

  const contactLine = contact?.phone ?? contact?.email;
  return (
    <div className="screen pb-36">
      <header className="flex items-center justify-between pt-2">
        <a className="grid h-11 w-11 place-items-center rounded-2xl bg-white shadow-card" href="#/campaigns"><Icon name="back" /></a>
        <Badge tone={campaign.isPromoted ? "gold" : "blue"}>{campaign.isPromoted ? t("campaign.promoted") : t("campaign.open")}</Badge>
      </header>
      <Card className="mt-5 overflow-hidden p-0">
        <div className="bg-gradient-to-br from-blue-600 via-blue-500 to-cyan-400 p-5 text-white">
          <div className="flex items-center gap-3"><span className="grid h-12 w-12 place-items-center rounded-2xl bg-white/20 font-extrabold">{campaign.logo}</span><div><p className="text-sm text-white/75">{campaign.company}</p><h1 className="mt-1 text-2xl font-extrabold leading-7">{campaign.title}</h1></div></div>
        </div>
        <div className="p-5"><p className="text-sm leading-6 text-brand-muted">{campaign.description}</p><div className="mt-5 grid grid-cols-2 gap-2"><div className="rounded-2xl bg-slate-50 p-3"><p className="text-xs text-brand-muted">{t("campaign.budget")}</p><p className="mt-1 text-sm font-extrabold">{formatCurrency(campaign.budgetFrom)}–{formatCurrency(campaign.budgetTo)}</p></div><div className="rounded-2xl bg-slate-50 p-3"><p className="text-xs text-brand-muted">{t("campaign.location")}</p><p className="mt-1 text-sm font-extrabold">{campaign.city}</p></div></div></div>
      </Card>
      <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("campaign.suitable")}</h2><div className="flex flex-wrap gap-2">{campaign.categories.map((category) => <Badge key={category} tone="blue">{category}</Badge>)}</div></section>
      <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("campaign.requirements")}</h2><Card><ul className="grid gap-3">{campaign.requirements.map((item) => <li className="flex gap-2 text-sm text-brand-muted" key={item}><Icon className="h-4 w-4 shrink-0 text-brand-success" name="check" />{item}</li>)}</ul></Card></section>
      <section className="mt-5"><h2 className="mb-3 font-extrabold">{t("campaign.businessContact")}</h2>{contactLine ? <Card><p className="text-sm font-bold">{contactLine}</p>{contact?.phone && contact.email && <p className="mt-1 text-sm text-brand-muted">{contact.email}</p>}</Card> : <UnlockBlock cta={unlocking ? t("campaign.invoiceCreating") : undefined} disabled={unlocking} onUnlock={unlockContact} subtitle={t("campaign.unlockSubtitle")} title={t("campaign.contactLocked")} />}</section>
      <div className="fixed inset-x-0 bottom-[70px] z-30 mx-auto max-w-[430px] bg-white/90 px-5 pb-3 pt-2 backdrop-blur"><Button className="w-full" onClick={() => setApplicationOpen(true)}><Icon name="send" />{t("campaign.apply")}</Button></div>
      <Modal onClose={() => setApplicationOpen(false)} open={applicationOpen} title={t("campaign.applyTitle")}><p className="text-sm leading-6 text-brand-muted">{t("campaign.applyDescription")}</p><Textarea className="mt-4" maxLength={1000} onChange={(event) => setApplicationMessage(event.target.value)} placeholder={t("campaign.applyPlaceholder")} value={applicationMessage} /><Button className="mt-4 w-full" disabled={applying} onClick={apply}>{applying ? t("campaign.sending") : t("campaign.submitApplication")}</Button></Modal>
      <Toast message={toast} /><BottomNav />
    </div>
  );
}
