import { useRef, useState } from "react";
import { getApiErrorMessage } from "../api/client";
import { addDealPublication, confirmDealPublication, deleteDealPublication, setDealPrice, updateDealPublicationViews, type DealDetails, type DealPublication } from "../api/marketplace";
import { useI18n } from "../i18n";
import { formatCurrency, formatNumericInput } from "../lib/currency";
import { useTelegram } from "../telegram/TelegramProvider";
import { Badge, Button, Card, Input, Modal } from "./ui";
import { DetailSection, FactGrid } from "./details/DetailBlocks";

// D51: the deal's agreed price and links to the published ads. Everything here is optional.
export const maxDealPublications = 5;

export const digitsToNumber = (value: string) => {
  const digits = value.replace(/\D/g, "");
  return digits ? Number(digits) : null;
};

export const isHttpsLink = (value: string) => {
  try { return new URL(value.trim()).protocol === "https:"; } catch { return false; }
};

export function hasDealResults(deal: DealDetails) {
  return Boolean(deal.canSetPrice || deal.canAddPublication || deal.canConfirmPublications || deal.agreedPrice != null || (deal.publications?.length ?? 0) > 0);
}

type Editor = { kind: "price" } | { kind: "add" } | { kind: "views"; publication: DealPublication } | null;

export function DealResults({ deal, onChanged, onMessage }: { deal: DealDetails; onChanged: () => Promise<void>; onMessage: (message: string, tone: "success" | "error") => void }) {
  const { t } = useI18n();
  const { openLink } = useTelegram();
  const [editor, setEditor] = useState<Editor>(null);
  const [price, setPrice] = useState("");
  const [url, setUrl] = useState("");
  const [views, setViews] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  // A ref, not state: a fast double tap must not send the same action twice before the re-render.
  const busyRef = useRef(false);
  const publications = deal.publications ?? [];

  if (!hasDealResults(deal)) return null;

  const open = (next: Editor) => {
    setError("");
    setPrice(next?.kind === "price" && deal.agreedPrice != null ? formatNumericInput(String(deal.agreedPrice)) : "");
    setUrl("");
    setViews(next?.kind === "views" && next.publication.views != null ? formatNumericInput(String(next.publication.views)) : "");
    setEditor(next);
  };

  const run = async (action: () => Promise<unknown>, successKey: string, failureKey: string) => {
    if (busyRef.current) return;
    busyRef.current = true;
    setBusy(true);
    try {
      await action();
      setEditor(null);
      onMessage(t(successKey), "success");
      await onChanged();
    } catch (failure) {
      onMessage(getApiErrorMessage(failure, t(failureKey)), "error");
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  };

  const save = () => {
    if (editor?.kind === "price") {
      const value = digitsToNumber(price);
      if (value == null) { setError(t("deals.priceRequired")); return; }
      void run(() => setDealPrice(deal.id, value), "deals.priceSaved", "deals.resultFailed");
    } else if (editor?.kind === "add") {
      if (!isHttpsLink(url)) { setError(t("deals.publicationLinkInvalid")); return; }
      void run(() => addDealPublication(deal.id, url, digitsToNumber(views)), "deals.publicationAdded", "deals.resultFailed");
    } else if (editor?.kind === "views") {
      const publication = editor.publication;
      void run(() => updateDealPublicationViews(deal.id, publication.id, digitsToNumber(views)), "deals.viewsSaved", "deals.resultFailed");
    }
  };

  const follow = (event: React.MouseEvent, href: string) => {
    if (!openLink) return;
    event.preventDefault();
    openLink(href);
  };

  return <DetailSection title={t("deals.results")}>
    <FactGrid facts={[{ label: t("deals.agreedPrice"), value: deal.agreedPrice != null ? formatCurrency(deal.agreedPrice) : t("deals.priceMissing"), wide: true }]} />
    {deal.canSetPrice && <Button className="mt-2 w-full" disabled={busy} onClick={() => open({ kind: "price" })} type="button" variant="secondary">{deal.agreedPrice != null ? t("deals.changePrice") : t("deals.setPrice")}</Button>}
    <div className="mt-3 grid gap-2">
      {publications.map((publication) => <Card key={publication.id}>
        <a className="block break-all text-sm font-semibold text-brand-accent" href={publication.url} onClick={(event) => follow(event, publication.url)} rel="noopener noreferrer" target="_blank">{publication.url.replace(/^https:\/\//, "")}</a>
        <p className="mt-1 text-sm text-brand-muted">{publication.views != null ? t("deals.viewsValue", { views: formatNumericInput(String(publication.views)) }) : t("deals.viewsMissing")}</p>
        <div className="mt-2 flex flex-wrap items-center gap-2">
          <Badge tone={publication.confirmed ? "green" : "gray"}>{publication.confirmed ? t("deals.publicationConfirmed") : t("deals.publicationUnconfirmed")}</Badge>
          {deal.canConfirmPublications && !publication.confirmed && <Button disabled={busy} onClick={() => void run(() => confirmDealPublication(deal.id, publication.id), "deals.publicationConfirmedToast", "deals.resultFailed")} type="button">{t("deals.confirmPublication")}</Button>}
          {deal.canAddPublication && <Button disabled={busy} onClick={() => open({ kind: "views", publication })} type="button" variant="secondary">{t("deals.editViews")}</Button>}
          {deal.canAddPublication && !publication.confirmed && <Button disabled={busy} onClick={() => void run(() => deleteDealPublication(deal.id, publication.id), "deals.publicationDeleted", "deals.resultFailed")} type="button" variant="secondary">{t("deals.deletePublication")}</Button>}
        </div>
      </Card>)}
    </div>
    {publications.length === 0 && <p className="mt-2 text-sm leading-6 text-brand-muted">{deal.canAddPublication ? t("deals.publicationsHintCreator") : t("deals.publicationsHintBusiness")}</p>}
    {deal.canAddPublication && publications.length < maxDealPublications && <Button className="mt-3 w-full" disabled={busy} onClick={() => open({ kind: "add" })} type="button" variant="secondary">{t("deals.addPublication")}</Button>}
    <Modal onClose={() => !busy && setEditor(null)} open={editor !== null} title={editor?.kind === "price" ? (deal.agreedPrice != null ? t("deals.changePrice") : t("deals.setPrice")) : editor?.kind === "add" ? t("deals.addPublication") : t("deals.editViews")}>
      <div className="grid gap-3">
        {editor?.kind === "price" && <Input inputMode="numeric" label={t("deals.agreedPrice")} onChange={(event) => setPrice(formatNumericInput(event.target.value))} suffix={t("currency.uzs")} value={price} />}
        {editor?.kind === "add" && <Input inputMode="url" label={t("deals.publicationLink")} onChange={(event) => setUrl(event.target.value)} placeholder="https://" type="url" value={url} />}
        {editor?.kind !== "price" && <Input inputMode="numeric" label={t("deals.viewsOptional")} onChange={(event) => setViews(formatNumericInput(event.target.value))} value={views} />}
        {error && <p className="text-sm font-semibold text-brand-danger" role="alert">{error}</p>}
      </div>
      <div className="mt-5 grid grid-cols-2 gap-3"><Button disabled={busy} onClick={() => setEditor(null)} type="button" variant="secondary">{t("common.cancel")}</Button><Button disabled={busy} onClick={save} type="button">{t("deals.saveResult")}</Button></div>
    </Modal>
  </DetailSection>;
}
