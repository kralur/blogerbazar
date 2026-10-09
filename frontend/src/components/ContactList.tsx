import { useState } from "react";
import { useI18n } from "../i18n";
import { contactUrl, copyText, displayContact, type ContactItem } from "../lib/contacts";
import { BottomSheet, Button, Card, Icon, Toast } from "./ui";
import { useTelegram } from "../telegram/TelegramProvider";
import { isMobileTelegram } from "../telegram/telegramTheme";

const iconByKind: Record<ContactItem["kind"], string> = { phone: "phone", telegram: "send", instagram: "instagram", tiktok: "tiktok", youtube: "youtube", website: "link", email: "mail" };

export function hasContacts(items: ContactItem[]) {
  return items.some((item) => Boolean(item.value.trim()));
}

export function ContactList({ items }: { items: ContactItem[] }) {
  const { language, t } = useI18n();
  const { haptic, openLink } = useTelegram();
  const [toast, setToast] = useState("");
  const [phoneActions, setPhoneActions] = useState<string | null>(null);
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

  // Telegram on iPhone often blocks call links from a Mini App, so a phone opens a choice instead of failing
  // silently: call, write to that number in Telegram, or copy it.
  const openContact = (item: (typeof visibleItems)[number]) => {
    haptic.selection();
    if (item.kind === "phone") {
      setPhoneActions(displayContact(item));
      return;
    }
    openLink(item.href!);
  };
  const phoneDigits = phoneActions?.replace(/[^\d+]/g, "") ?? "";
  const canCall = isMobileTelegram(window.Telegram?.WebApp?.platform);

  return <><Card className="divide-y divide-brand-line p-0">{visibleItems.map((item) => <div className="flex items-center gap-3 p-3" key={`${item.kind}-${item.value}`}><span aria-hidden="true" className="grid h-10 w-10 shrink-0 place-items-center rounded-2xl bg-brand-soft text-brand-ink"><Icon name={iconByKind[item.kind]} /></span><a className="min-w-0 flex-1" href={item.href!} onClick={(event) => { event.preventDefault(); openContact(item); }}><span className="block text-xs font-semibold text-brand-muted">{t(`contacts.${item.kind}`)}</span><span className="contact-list__value mt-0.5 block text-sm font-bold [overflow-wrap:anywhere] [text-wrap:balance]">{displayContact(item)}</span></a><button aria-label={t("contacts.copyAria", { value: displayContact(item) })} className="grid h-10 w-10 shrink-0 place-items-center rounded-2xl text-brand-muted transition hover:bg-brand-soft active:scale-95" onClick={() => void copy(displayContact(item))} type="button"><Icon name="copy" /></button></div>)}</Card><Toast message={toast} tone="copied" />
    <BottomSheet onClose={() => setPhoneActions(null)} open={phoneActions !== null} title={phoneActions ?? ""} variant="neutral">
      <div className="grid gap-2">
        {/* Telegram blocks tel: inside a Mini App (iPhone ignores even a tapped link), so the call goes through
            our small call page opened in the browser, where the phone offers to dial. Desktop has no phone to call with. */}
        {canCall && <Button onClick={() => { setPhoneActions(null); openLink(`${window.location.origin}/call.html?n=${encodeURIComponent(phoneDigits)}&lang=${language}`); }} type="button"><Icon name="phone" />{t("contacts.call")}</Button>}
        <Button onClick={() => { setPhoneActions(null); openLink(`https://t.me/${phoneDigits}`); }} type="button" variant="secondary"><Icon name="send" />{t("contacts.writeInTelegram")}</Button>
        <Button onClick={() => { const value = phoneActions ?? ""; setPhoneActions(null); void copy(value); }} type="button" variant="secondary"><Icon name="copy" />{t("contacts.copyNumber")}</Button>
        {canCall && <p className="text-xs leading-5 text-brand-muted">{t("contacts.callHint")}</p>}
      </div>
    </BottomSheet></>;
}
