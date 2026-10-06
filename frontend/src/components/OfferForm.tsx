import { useRef, useState, type FormEvent } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { createOffer, type Offer, type OfferFormat } from "../api/marketplace";
import { useI18n } from "../i18n";
import { offerFormatLabelKey, offerFormats } from "../lib/offerStatus";
import { Button, Input, Modal, Textarea } from "./ui";

export function OfferForm({ bloggerId, open, onClose, onSent }: { bloggerId: string; open: boolean; onClose: () => void; onSent: (offer: Offer) => void }) {
  const { t } = useI18n();
  const [format, setFormat] = useState<OfferFormat>("reels");
  const [budget, setBudget] = useState("");
  const [deadline, setDeadline] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [sending, setSending] = useState(false);
  const sendingRef = useRef(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (sendingRef.current) return;
    if (!message.trim()) {
      setError(t("offers.messageRequired"));
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
      setDeadline("");
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
    <form className="grid gap-3" onSubmit={submit}>
      <div>
        <p className="mb-2 text-sm font-bold">{t("offers.format")}</p>
        <div className="grid grid-cols-2 gap-2">{offerFormats.map((value) => <button aria-pressed={format === value} className={`rounded-2xl border px-3 py-2.5 text-sm font-bold ${format === value ? "choice-selected" : "border-brand-line"}`} key={value} onClick={() => setFormat(value)} type="button">{t(offerFormatLabelKey(value))}</button>)}</div>
      </div>
      <Input inputMode="numeric" label={t("offers.budget")} min={0} onChange={(event) => setBudget(event.target.value.replace(/[^\d]/g, ""))} placeholder={t("offers.budgetPlaceholder")} value={budget} />
      <Input label={t("offers.deadline")} onChange={(event) => setDeadline(event.target.value)} type="date" value={deadline} />
      <Textarea label={t("offers.message")} maxLength={1000} onChange={(event) => setMessage(event.target.value)} placeholder={t("offers.messagePlaceholder")} value={message} />
      {error && <p className="text-sm font-semibold text-red-600" role="alert">{error}</p>}
      <p className="text-xs text-brand-muted">{t("offers.formHint")}</p>
      <Button disabled={sending} type="submit">{sending ? t("offers.sending") : t("offers.send")}</Button>
    </form>
  </Modal>;
}
