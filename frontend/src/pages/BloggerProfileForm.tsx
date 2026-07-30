import { useEffect, useMemo, useState } from "react";
import { createBloggerProfile, getMyBloggerProfile, updateBloggerProfile } from "../api/marketplace";
import { BottomNav, Button, Card, Chip, Icon, Input, Modal, Textarea, Toast } from "../components/ui";
import { categoryLabel, useI18n } from "../i18n";
import { formatNumericInput, formatPhoneInput, normalizeNumericInput } from "../lib/currency";
import { useTelegram } from "../telegram/TelegramProvider";

const categories = ["lifestyle", "beauty", "food", "technology", "sport", "travel", "finance", "gaming", "fashion"];
const initial = { name: "", lastName: "", username: "", city: "tashkent", phone: "", email: "", totalFollowers: "10 000", averageReach: "25 000", engagementRate: "5.5", storiesPrice: "250 000", reelsPrice: "500 000", postPrice: "350 000", integrationPrice: "900 000", bio: "", portfolioUrl: "" };
type Field = keyof typeof initial;
type Errors = Partial<Record<Field | "categories", string>>;
const usernamePattern = /^@[A-Za-z0-9_]{5,32}$/;
const phonePattern = /^\+998\s\d{2}\s\d{3}\s\d{2}\s\d{2}$/;
const numericFields = ["totalFollowers", "averageReach", "storiesPrice", "reelsPrice", "postPrice", "integrationPrice"] as const;

function validate(form: typeof initial, selectedCategories: string[], t: (key: string) => string): Errors {
  const errors: Errors = {};
  if (!form.name.trim()) errors.name = t("form.validation.name");
  if (!usernamePattern.test(form.username.trim())) errors.username = t("form.validation.username");
  if (!form.city.trim()) errors.city = t("form.validation.city");
  if (!phonePattern.test(form.phone)) errors.phone = t("form.validation.phone");
  if (form.email && !/^\S+@\S+\.\S+$/.test(form.email)) errors.email = t("form.validation.email");
  if (!selectedCategories.length) errors.categories = t("form.validation.categories");
  if (normalizeNumericInput(form.totalFollowers) <= 0) errors.totalFollowers = t("form.validation.followers");
  if (normalizeNumericInput(form.averageReach) <= 0) errors.averageReach = t("form.validation.reach");
  const engagementRate = Number(form.engagementRate);
  if (!Number.isFinite(engagementRate) || engagementRate < 0.1 || engagementRate > 100) errors.engagementRate = t("form.validation.er");
  if (normalizeNumericInput(form.storiesPrice) <= 0) errors.storiesPrice = t("form.validation.stories");
  if (normalizeNumericInput(form.reelsPrice) <= 0) errors.reelsPrice = t("form.validation.reels");
  return errors;
}

export function BloggerProfileForm() {
  const { t, language } = useI18n();
  const { user } = useTelegram();
  const [form, setForm] = useState(initial);
  const [selectedCategories, setSelectedCategories] = useState<string[]>(["lifestyle"]);
  const [barterEnabled, setBarterEnabled] = useState(true);
  const [touched, setTouched] = useState<Partial<Record<Field | "categories", boolean>>>({});
  const [existing, setExisting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState(false);
  const [toast, setToast] = useState("");
  const errors = useMemo(() => validate(form, selectedCategories, t), [form, selectedCategories, t, language]);
  const valid = Object.keys(errors).length === 0;

  const update = (key: Field) => (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const value = key === "phone" ? formatPhoneInput(event.target.value) : event.target.value;
    setForm((current) => ({ ...current, [key]: value }));
  };
  const numeric = (key: typeof numericFields[number]) => (event: React.ChangeEvent<HTMLInputElement>) => setForm((current) => ({ ...current, [key]: formatNumericInput(event.target.value) }));
  const blur = (key: Field | "categories") => () => setTouched((current) => ({ ...current, [key]: true }));

  useEffect(() => {
    if (user?.username) setForm((current) => current.username ? current : { ...current, username: `@${user.username}` });
    getMyBloggerProfile().then((profile) => {
      setForm({ name: profile.name, lastName: profile.lastName ?? "", username: profile.username ?? "", city: profile.city, phone: profile.phone ?? "", email: profile.email ?? "", totalFollowers: formatNumericInput(String(profile.totalFollowers)), averageReach: formatNumericInput(String(profile.averageReach ?? "")), engagementRate: String(profile.engagementRate ?? ""), storiesPrice: formatNumericInput(String(profile.storiesPrice ?? "")), reelsPrice: formatNumericInput(String(profile.reelsPrice ?? "")), postPrice: formatNumericInput(String(profile.postPrice ?? "")), integrationPrice: formatNumericInput(String(profile.integrationPrice ?? "")), bio: profile.bio ?? "", portfolioUrl: profile.portfolioItems[0]?.url ?? "" });
      setSelectedCategories(profile.categories);
      setBarterEnabled(profile.barterEnabled);
      setExisting(true);
    }).catch(() => undefined);
  }, [user?.username]);

  const toggleCategory = (category: string) => {
    setTouched((current) => ({ ...current, categories: true }));
    setSelectedCategories((current) => current.includes(category) ? current.filter((value) => value !== category) : [...current, category]);
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setTouched({ name: true, username: true, city: true, phone: true, email: true, categories: true, totalFollowers: true, averageReach: true, engagementRate: true, storiesPrice: true, reelsPrice: true });
    if (!valid || saving) return;
    try {
      setSaving(true);
      const input = { name: form.name.trim(), lastName: form.lastName.trim() || undefined, username: form.username.trim(), city: form.city.trim(), categories: selectedCategories, bio: form.bio.trim() || undefined, phone: form.phone.trim(), email: form.email.trim() || undefined, totalFollowers: normalizeNumericInput(form.totalFollowers), averageReach: normalizeNumericInput(form.averageReach), engagementRate: Number(form.engagementRate), storiesPrice: normalizeNumericInput(form.storiesPrice), reelsPrice: normalizeNumericInput(form.reelsPrice), postPrice: normalizeNumericInput(form.postPrice) || undefined, integrationPrice: normalizeNumericInput(form.integrationPrice) || undefined, barterEnabled, portfolioItems: form.portfolioUrl.trim() ? [{ title: t("form.blogger.portfolioTitle"), type: "IMAGE" as const, url: form.portfolioUrl.trim() }] : [] };
      if (existing) await updateBloggerProfile(input); else await createBloggerProfile(input);
      setSuccess(true);
    } catch (error) {
      setToast(error instanceof Error ? error.message : t("form.submitFailed"));
    } finally {
      setSaving(false);
    }
  };

  return <form className="screen space-y-5 px-4 pt-5" noValidate onSubmit={submit}>
    <header><p className="text-sm font-semibold text-brand-muted">{t("form.blogger.eyebrow")}</p><h1 className="mt-1 text-3xl font-extrabold tracking-tight">{existing ? t("form.blogger.editTitle") : t("form.blogger.createTitle")}</h1><p className="mt-2 text-sm leading-5 text-brand-muted">{t("form.blogger.subtitle")}</p></header>
    <Card><div className="flex items-center gap-4"><div className="grid h-16 w-16 place-items-center rounded-3xl bg-blue-50 text-brand-blue"><Icon name="user" /></div><div><p className="font-extrabold">{t("form.photo")}</p><p className="mt-1 text-sm text-brand-muted">{t("form.photoHelper")}</p></div></div></Card>
    <section className="grid gap-3"><h2 className="font-extrabold">{t("form.personal")}</h2><div className="grid grid-cols-2 gap-3"><Input error={touched.name ? errors.name : undefined} label={t("form.name")} onBlur={blur("name")} onChange={update("name")} placeholder={t("form.blogger.namePlaceholder")} required value={form.name} /><Input label={t("form.lastName")} onChange={update("lastName")} placeholder={t("form.blogger.lastNamePlaceholder")} value={form.lastName} /></div><div><Input error={touched.username ? errors.username : undefined} label={t("form.telegramUsername")} onBlur={blur("username")} onChange={update("username")} placeholder="@madina" required value={form.username} /><p className="mt-1 text-xs text-brand-muted">{t("form.usernameHelper")}</p></div><Input error={touched.city ? errors.city : undefined} label={t("common.city")} onBlur={blur("city")} onChange={update("city")} placeholder={t("form.blogger.cityPlaceholder")} required value={form.city} /><div className="grid grid-cols-2 gap-3"><Input error={touched.phone ? errors.phone : undefined} inputMode="tel" label={t("common.phone")} onBlur={blur("phone")} onChange={update("phone")} placeholder="+998 90 123 45 67" required value={form.phone} /><Input error={touched.email ? errors.email : undefined} label={t("form.emailOptional")} onBlur={blur("email")} onChange={update("email")} placeholder="you@email.com" type="email" value={form.email} /></div></section>
    <section><h2 className="font-extrabold">{t("form.audience")}</h2><div aria-describedby={errors.categories ? "categories-error" : undefined} aria-invalid={touched.categories && !!errors.categories} className="no-scrollbar mt-3 flex flex-wrap gap-2" onBlur={blur("categories")}>{categories.map((category) => <button aria-label={t("form.categoryAria", { category: categoryLabel(category, language) })} aria-pressed={selectedCategories.includes(category)} key={category} onClick={() => toggleCategory(category)} type="button"><Chip active={selectedCategories.includes(category)}>{categoryLabel(category, language)}</Chip></button>)}</div>{touched.categories && errors.categories && <p className="mt-2 text-xs font-semibold text-brand-danger" id="categories-error">{errors.categories}</p>}<div className="mt-4 grid gap-3"><div className="grid grid-cols-2 gap-3"><Input error={touched.totalFollowers ? errors.totalFollowers : undefined} inputMode="numeric" label={t("common.followers")} onBlur={blur("totalFollowers")} onChange={numeric("totalFollowers")} required value={form.totalFollowers} /><Input error={touched.averageReach ? errors.averageReach : undefined} inputMode="numeric" label={t("common.reach")} onBlur={blur("averageReach")} onChange={numeric("averageReach")} required value={form.averageReach} /></div><Input error={touched.engagementRate ? errors.engagementRate : undefined} label={t("search.er")} max="100" min="0.1" onBlur={blur("engagementRate")} onChange={update("engagementRate")} required step="0.1" type="number" value={form.engagementRate} /></div></section>
    <section><h2 className="font-extrabold">{t("form.adPrices")}</h2><div className="mt-3 grid grid-cols-2 gap-3"><Input error={touched.storiesPrice ? errors.storiesPrice : undefined} inputMode="numeric" label={t("card.stories")} onBlur={blur("storiesPrice")} onChange={numeric("storiesPrice")} required value={form.storiesPrice} /><Input error={touched.reelsPrice ? errors.reelsPrice : undefined} inputMode="numeric" label={t("card.reels")} onBlur={blur("reelsPrice")} onChange={numeric("reelsPrice")} required value={form.reelsPrice} /><Input inputMode="numeric" label={t("card.post")} onChange={numeric("postPrice")} value={form.postPrice} /><Input inputMode="numeric" label={t("card.integration")} onChange={numeric("integrationPrice")} value={form.integrationPrice} /></div><button aria-pressed={barterEnabled} className="mt-3 flex w-full items-center justify-between rounded-2xl border border-brand-line bg-white p-4 text-left focus:outline-none focus:ring-4 focus:ring-blue-100" onClick={() => setBarterEnabled((value) => !value)} type="button"><span><strong>{t("form.barterTitle")}</strong><span className="mt-1 block text-sm text-brand-muted">{t("form.barterSubtitle")}</span></span><span className={`h-7 w-12 rounded-full p-1 transition ${barterEnabled ? "bg-brand-blue" : "bg-slate-200"}`}><span className={`block h-5 w-5 rounded-full bg-white transition ${barterEnabled ? "translate-x-5" : ""}`} /></span></button></section>
    <section className="grid gap-3"><h2 className="font-extrabold">{t("form.portfolio")}</h2><Input label={t("form.portfolioUrl")} onChange={update("portfolioUrl")} placeholder="https://…" type="url" value={form.portfolioUrl} /><Textarea label={t("form.aboutMe")} onChange={update("bio")} placeholder={t("form.aboutMePlaceholder")} value={form.bio} /></section>
    <Button aria-busy={saving} className="w-full" disabled={!valid || saving} type="submit"><Icon name="check" />{saving ? t("form.publishing") : t("form.publishProfile")}</Button><Toast message={toast} tone="error" /><Modal onClose={() => setSuccess(false)} open={success} title={t("form.successTitle")}><p className="text-sm leading-6 text-brand-muted">{t("form.successDescription")}</p><Button className="mt-5 w-full" onClick={() => { window.location.hash = "/profile"; }} type="button">{t("form.understood")}</Button></Modal><BottomNav />
  </form>;
}
