import { useState } from "react";
import { useI18n } from "../i18n";
import { contactUrl, copyText, displayContact, type ContactItem } from "../lib/contacts";
import { Card, Icon, Toast } from "./ui";

const iconByKind: Record<ContactItem["kind"], string> = { phone: "phone", telegram: "send", instagram: "link", tiktok: "link", youtube: "link", website: "link", email: "mail" };

export function hasContacts(items: ContactItem[]) {
  return items.some((item) => Boolean(item.value.trim()));
}

export function ContactList({ items }: { items: ContactItem[] }) {
  const { t } = useI18n();
  const [toast, setToast] = useState("");
  const visibleItems = items.filter((item) => Boolean(item.value.trim())).map((item) => ({ ...item, href: contactUrl(item) })).filter((item) => item.href);
  if (!visibleItems.length) return null;

  const copy = async (value: string) => {
    try {
      await copyText(value);
      setToast(t("contacts.copied"));
    } catch {
      setToast(t("contacts.copyFailed"));
    }
  };

  return <><Card className="divide-y divide-brand-line p-0">{visibleItems.map((item) => <div className="flex items-center gap-3 p-3" key={`${item.kind}-${item.value}`}><span className="grid h-10 w-10 shrink-0 place-items-center rounded-2xl bg-blue-50 text-brand-blue"><Icon name={iconByKind[item.kind]} /></span><a className="min-w-0 flex-1" href={item.href!} rel={item.kind === "phone" || item.kind === "email" ? undefined : "noreferrer"} target={item.kind === "phone" || item.kind === "email" ? undefined : "_blank"}><span className="block text-xs font-semibold text-brand-muted">{t(`contacts.${item.kind}`)}</span><span className="mt-0.5 block truncate text-sm font-bold">{displayContact(item)}</span></a><button aria-label={t("contacts.copyAria", { value: displayContact(item) })} className="grid h-10 w-10 shrink-0 place-items-center rounded-2xl text-brand-muted transition hover:bg-slate-100 active:scale-95" onClick={() => void copy(displayContact(item))} type="button"><Icon name="copy" /></button></div>)}</Card><Toast message={toast} /></>;
}
