import { useEffect, useMemo, useState } from "react";
import { createBusinessProfile, deleteProfileImage, getMyBusinessProfile, updateBusinessProfile, uploadProfileImage } from "../api/marketplace";
import { getApiErrorMessage } from "../api/client";
import { formatPhoneInput } from "../lib/currency";
import { ProfileMediaPicker, type PendingProfileImage } from "../components/ProfileMediaPicker";
import { BottomNav, Button, Icon, Input, Modal, Textarea, Toast } from "../components/ui";
import { RegionSelect } from "../components/RegionSelect";
import { normalizeRegion } from "../lib/taxonomy";
import { useTelegram } from "../telegram/TelegramProvider";
import { useI18n } from "../i18n";

const initial = { name: "", username: "", city: "tashkent-city", description: "", phone: "+998", email: "", website: "" };
type Field = keyof typeof initial;
type Errors = Partial<Record<Field, string>>;
const usernamePattern = /^@[A-Za-z0-9_]{5,32}$/;
const phonePattern = /^\+998\s\d{2}\s\d{3}\s\d{2}\s\d{2}$/;

function validate(form: typeof initial, t: (key: string) => string): Errors {
  const errors: Errors = {};
  if (!form.name.trim()) errors.name = t("form.validation.company");
  if (!usernamePattern.test(form.username.trim())) errors.username = t("form.validation.companyUsername");
  if (!form.city.trim()) errors.city = t("form.validation.city");
  if (!form.description.trim()) errors.description = t("form.validation.description");
  if (!phonePattern.test(form.phone)) errors.phone = t("form.validation.phone");
  if (form.email && !/^\S+@\S+\.\S+$/.test(form.email)) errors.email = t("form.validation.email");
  if (form.website && !/^https:\/\//i.test(form.website)) errors.website = t("form.validation.website");
  return errors;
}

export function BusinessProfileForm({ onCompleted }: { onCompleted?: () => void }) {
  const { language, t } = useI18n();
  const { user } = useTelegram();
  const [form, setForm] = useState(initial);
  const [touched, setTouched] = useState<Partial<Record<Field, boolean>>>({});
  const [existing, setExisting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState(false);
  const [toast, setToast] = useState("");
  const [logoUrl, setLogoUrl] = useState<string | null>(null);
  const [pendingImage, setPendingImage] = useState<PendingProfileImage>();
  const errors = useMemo(() => validate(form, t), [form, language, t]);
  const valid = Object.keys(errors).length === 0;

  const update = (key: Field) => (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const value = key === "phone" ? formatPhoneInput(event.target.value) : event.target.value;
    setForm((current) => ({ ...current, [key]: value }));
  };
  const blur = (key: Field) => () => setTouched((current) => ({ ...current, [key]: true }));

  useEffect(() => {
    if (user?.username) setForm((current) => current.username ? current : { ...current, username: `@${user.username}` });
    getMyBusinessProfile().then((profile) => {
      setForm({ name: profile.name, username: profile.username ?? "", city: normalizeRegion(profile.city) || initial.city, description: profile.description ?? "", phone: formatPhoneInput(profile.phone ?? initial.phone), email: profile.email ?? "", website: profile.websiteUrl ?? "" });
      setLogoUrl(profile.logoUrl ?? null);
      setExisting(true);
    }).catch(() => undefined);
  }, [user?.username]);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setTouched({ name: true, username: true, city: true, description: true, phone: true, email: true, website: true });
    if (!valid || saving) return;
    try {
      setSaving(true);
      const input = { name: form.name.trim(), username: form.username.trim(), city: form.city.trim(), logoUrl: logoUrl ?? undefined, description: form.description.trim(), phone: form.phone.trim(), email: form.email.trim() || undefined, websiteUrl: form.website.trim() || undefined };
      if (existing) await updateBusinessProfile(input); else { await createBusinessProfile(input); setExisting(true); }
      if (pendingImage instanceof File) {
        const media = await uploadProfileImage("business", pendingImage);
        setLogoUrl(media.url);
      } else if (pendingImage === null && logoUrl) {
        await deleteProfileImage("business");
        setLogoUrl(null);
      }
      setPendingImage(undefined);
      if (onCompleted) onCompleted();
      else setSuccess(true);
    } catch (error) {
      setToast(getApiErrorMessage(error, t("form.submitFailed"), {
        conflictMessage: t("form.businessProfileConflict"),
        validationMessages: {
          name: t("form.validation.company"),
          username: t("form.validation.companyUsername"),
          city: t("form.validation.city"),
          description: t("form.validation.description"),
          phone: t("form.validation.phone"),
          email: t("form.validation.email"),
          websiteurl: t("form.validation.website")
        }
      }));
    } finally {
      setSaving(false);
    }
  };

  return <form className="screen space-y-5 px-4 pt-5" noValidate onSubmit={submit}><header><p className="text-sm font-semibold text-brand-muted">{t("form.business.eyebrow")}</p><h1 className="mt-1 text-3xl font-extrabold tracking-tight">{existing ? t("form.business.editTitle") : t("form.business.createTitle")}</h1><p className="mt-2 text-sm leading-5 text-brand-muted">{t("form.business.subtitle")}</p></header><ProfileMediaPicker currentUrl={logoUrl} disabled={saving} name={form.name || t("profile.telegramUser")} onChange={setPendingImage} pending={pendingImage} /><section className="grid gap-3"><Input error={touched.name ? errors.name : undefined} label={t("form.companyName")} onBlur={blur("name")} onChange={update("name")} placeholder="Lumi Beauty" required value={form.name} /><div><Input error={touched.username ? errors.username : undefined} label={t("form.telegramUsername")} onBlur={blur("username")} onChange={update("username")} placeholder="@username" required value={form.username} /><p className="mt-1 text-xs text-brand-muted">{t("form.usernameBusinessHelper")}</p></div><RegionSelect error={touched.city ? errors.city : undefined} onChange={(event) => setForm((current) => ({ ...current, city: event.target.value }))} required value={form.city} /><div><Input error={touched.website ? errors.website : undefined} label={t("form.websiteOptional")} onBlur={blur("website")} onChange={update("website")} placeholder="https://company.uz" type="url" value={form.website} /><p className="mt-1 text-xs text-brand-muted">{t("form.websiteHelper")}</p></div><Textarea error={touched.description ? errors.description : undefined} label={t("form.aboutCompany")} maxLength={1000} onBlur={blur("description")} onChange={update("description")} placeholder={t("form.companyDescriptionPlaceholder")} required value={form.description} /></section><section className="grid gap-3"><h2 className="font-extrabold">{t("form.contacts")}</h2><Input error={touched.phone ? errors.phone : undefined} inputMode="tel" label={t("common.phone")} onBlur={blur("phone")} onChange={update("phone")} placeholder="+998 90 123 45 67" required value={form.phone} /><Input error={touched.email ? errors.email : undefined} label={t("form.emailOptional")} onBlur={blur("email")} onChange={update("email")} placeholder="brand@example.uz" type="email" value={form.email} /></section><Button aria-busy={saving} className="w-full" disabled={!valid || saving} type="submit"><Icon name="check" />{saving ? t("form.publishing") : t("form.publishProfile")}</Button><Toast message={toast} tone="error" /><Modal onClose={() => setSuccess(false)} open={success} title={t("form.successTitle")}><p className="text-sm leading-6 text-brand-muted">{t("form.successDescription")}</p><Button className="mt-5 w-full" onClick={() => { window.location.hash = "/profile"; }} type="button">{t("form.understood")}</Button></Modal>{!onCompleted && <BottomNav />}</form>;
}
