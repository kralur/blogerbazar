import { useEffect, useRef, useState, type FormEvent } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { createOffer, getMyCampaign, getMyCampaigns, type MyCampaign, type Offer, type OfferFormat } from "../api/marketplace";
import { useI18n } from "../i18n";
import { formatNumericInput, isPastDay, localDay } from "../lib/currency";
import { offerFormatLabelKey, offerFormats } from "../lib/offerStatus";
import { FilterSelect } from "./catalog/CatalogShared";
import { Button, Input, Modal, Textarea } from "./ui";

const MessageLimit = 1000;

export function OfferForm({ bloggerId, open, onClose, onSent }: { bloggerId: string; open: boolean; onClose: () => void; onSent: (offer: Offer) => void }) {
  const { t } = useI18n();
  const [format, setFormat] = useState<OfferFormat>("reels");
  const [budget, setBudget] = useState("");
  const [messageError, setMessageError] = useState("");
  const [deadline, setDeadline] = useState(() => localDay(7));
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [sending, setSending] = useState(false);
  const sendingRef = useRef(false);
  const [campaigns, setCampaigns] = useState<MyCampaign[]>([]);
  const [campaignId, setCampaignId] = useState("");

  // A business can start from one of its open campaigns instead of typing the terms again.
  useEffect(() => {
    if (!open) return;
    const controller = new AbortController();
    getMyCampaigns({ status: 1, pageSize: 20 }, controller.signal)
      .then((page) => setCampaigns(page.items.filter((campaign) => !isPastDay(campaign.deadline))))
      .catch(() => setCampaigns([]));
    return () => controller.abort();
  }, [open]);

  const fillFromCampaign = async (id: string) => {
    setCampaignId(id);
    if (!id) return;
    try {
      const campaign = await getMyCampaign(id);
      const campaignBudget = campaign.maxBudget ?? campaign.minBudget;
      if (campaignBudget != null) setBudget(String(campaignBudget));
      if (campaign.deadline && !isPastDay(campaign.deadline)) setDeadline(campaign.deadline.slice(0, 10));
      setMessage([campaign.title, campaign.description].filter(Boolean).join("\n\n").slice(0, MessageLimit));
      setMessageError("");
    } catch {
      setError(t("offers.campaignFillFailed"));
    }
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (sendingRef.current) return;
    if (!message.trim()) {
      setMessageError(t("offers.messageRequired"));
      return;
    }

    sendingRef.current = true;
    setSending(true);
    setError("");
    try {
      const offer = await createOffer({
        bloggerId,
        format,
        offeredBudget: budget.trim() ? Number(budget) : null,
        deadline: deadline ? new Date(`${deadline}T00:00:00Z`).toISOString() : null,
        message: message.trim()
      });
      setBudget("");
      setDeadline(localDay(7));
      setCampaignId("");
      setMessage("");
      onSent(offer);
    } catch (failure) {
      setError(failure instanceof ApiError && failure.status === 403
        ? t("offers.businessOnly")
        : getApiErrorMessage(failure, t("offers.sendFailed"), { conflictMessage: t("offers.conflict") }));
    } finally {
      sendingRef.current = false;
      setSending(false);
    }
  };

  return <Modal onClose={() => !sending && onClose()} open={open} title={t("offers.formTitle")}>
    <form className="grid gap-3" noValidate onSubmit={submit}>
      {campaigns.length > 0 && <FilterSelect label={t("offers.fromCampaign")} onChange={(value) => void fillFromCampaign(value)} options={[["", t("offers.fromCampaignNone")], ...campaigns.map((campaign) => [campaign.id, campaign.title])]} value={campaignId} />}
      <div>
        <p className="mb-2 text-sm font-bold">{t("offers.format")}</p>
        <div className="grid grid-cols-2 gap-2">{offerFormats.map((value) => <button aria-pressed={format === value} className={`rounded-2xl border px-3 py-2.5 text-sm font-bold ${format === value ? "choice-selected" : "border-brand-line"}`} key={value} onClick={() => setFormat(value)} type="button">{t(offerFormatLabelKey(value))}</button>)}</div>
      </div>
      <Input inputMode="numeric" label={t("offers.budget")} onChange={(event) => setBudget(event.target.value.replace(/\D/g, ""))} placeholder={t("offers.budgetPlaceholder")} value={formatNumericInput(budget)} />
      <Input label={t("offers.deadline")} min={localDay(0)} onChange={(event) => setDeadline(event.target.value)} type="date" value={deadline} />
      <Textarea error={messageError} label={t("offers.message")} maxLength={MessageLimit} onChange={(event) => { setMessage(event.target.value); if (messageError) setMessageError(""); }} placeholder={t("offers.messagePlaceholder")} required value={message} />
      {error && <p className="text-sm font-semibold text-red-600" role="alert">{error}</p>}
      <p className="text-xs text-brand-muted">{t("offers.formHint")}</p>
      <Button disabled={sending} type="submit">{sending ? t("offers.sending") : t("offers.send")}</Button>
    </form>
  </Modal>;
}
