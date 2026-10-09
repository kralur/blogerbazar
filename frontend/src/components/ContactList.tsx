import { useState } from "react";
import { useI18n } from "../i18n";
import { contactUrl, copyText, displayContact, type ContactItem } from "../lib/contacts";
import { BottomSheet, Button, Card, Icon, Toast } from "./ui";
import { useTelegram } from "../telegram/TelegramProvider";

const iconByKind: Record<ContactItem["kind"], string> = { phone: "phone", telegram: "send", instagram: "instagram", tiktok: "tiktok", youtube: "youtube", website: "link", email: "mail" };

export function hasContacts(items: ContactItem[]) {
  return items.some((item) => Boolean(item.value.trim()));
}

// onSendToChat: inside a deal, the phone can also arrive as a contact card in the bot chat, where Telegram's own
// Call and Add to contacts buttons work (a Mini App cannot start a call on iPhone).
export function ContactList({ items, onSendToChat }: { items: ContactItem[]; onSendToChat?: () => Promise<string> }) {
  const { t } = useI18n();
  const { haptic, openLink } = useTelegram();
  const [toast, setToast] = useState("");
  const [phoneActions, setPhoneActions] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const sendToChat = async () => {
    if (!onSendToChat || sending) return;
    setSending(true);
    try {
      setToast(await onSendToChat());
      haptic.success();
      setPhoneActions(null);
    } catch (error) {
      haptic.error();
      setToast(error instanceof Error && error.message ? error.message : t("contacts.sendToChatFailed"));
    } finally {
      setSending(false);
    }
  };
  const visibleItems = items.filter((item) => Boolean(item.value.trim())).map((item) => ({ ...item, href: contactUrl(item) })).filter((item) => item.href);
  if (!visibleItems.length) return null;

  const copy = async (value: string) => {
    try {
      await copyText(value);
      haptic.success();
      setToast(t("contacts.copied"));
    } catch {
      haptic.error();
      setToast(t("contacts.copyFailed"));
    }
  };

  // A Mini App cannot start a call on iPhone, so a phone opens a choice: the contact card in the bot chat
  // (inside a deal), a Telegram chat with that number, or a copy.
  const openContact = (item: (typeof visibleItems)[number]) => {
    haptic.selection();
    if (item.kind === "phone") {
      setPhoneActions(displayContact(item));
      return;
    }
    openLink(item.href!);
  };
  const phoneDigits = phoneActions?.replace(/[^\d+]/g, "") ?? "";

  return <><Card className="divide-y divide-brand-line p-0">{visibleItems.map((item) => <div className="flex items-center gap-3 p-3" key={`${item.kind}-${item.value}`}><span aria-hidden="true" className="grid h-10 w-10 shrink-0 place-items-center rounded-2xl bg-brand-soft text-brand-ink"><Icon name={iconByKind[item.kind]} /></span><a className="min-w-0 flex-1" href={item.href!} onClick={(event) => { event.preventDefault(); openContact(item); }}><span className="block text-xs font-semibold text-brand-muted">{t(`contacts.${item.kind}`)}</span><span className="contact-list__value mt-0.5 block text-sm font-bold [overflow-wrap:anywhere] [text-wrap:balance]">{displayContact(item)}</span></a><button aria-label={t("contacts.copyAria", { value: displayContact(item) })} className="grid h-10 w-10 shrink-0 place-items-center rounded-2xl text-brand-muted transition hover:bg-brand-soft active:scale-95" onClick={() => void copy(displayContact(item))} type="button"><Icon name="copy" /></button></div>)}</Card><Toast message={toast} tone={toast === t("contacts.copied") ? "copied" : "success"} />
    <BottomSheet onClose={() => setPhoneActions(null)} open={phoneActions !== null} title={phoneActions ?? ""} variant="neutral">
      <div className="grid gap-2">
        {onSendToChat && <Button aria-busy={sending} disabled={sending} onClick={() => void sendToChat()} type="button"><Icon name="phone" />{t("contacts.sendToChat")}</Button>}
        {onSendToChat && <p className="text-xs leading-5 text-brand-muted">{t("contacts.sendToChatHint")}</p>}
        <Button onClick={() => { setPhoneActions(null); openLink(`https://t.me/${phoneDigits}`); }} type="button" variant={onSendToChat ? "secondary" : undefined}><Icon name="send" />{t("contacts.writeInTelegram")}</Button>
        <Button onClick={() => { const value = phoneActions ?? ""; setPhoneActions(null); void copy(value); }} type="button" variant="secondary"><Icon name="copy" />{t("contacts.copyNumber")}</Button>
      </div>
    </BottomSheet></>;
}
