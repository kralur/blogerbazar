import { useEffect, useMemo, useState } from "react";
import { createBloggerProfile, deleteProfileImage, getMyBloggerProfile, updateBloggerProfile, uploadProfileImage } from "../api/marketplace";
import { getApiErrorMessage } from "../api/client";
import { CategoryMultiSelect } from "../components/CategoryMultiSelect";
import { ProfileMediaPicker, type PendingProfileImage } from "../components/ProfileMediaPicker";
import { RegionSelect } from "../components/RegionSelect";
import { BottomNav, Button, Icon, Input, Modal, Textarea, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { formatNumericInput, formatPhoneInput, normalizeNumericInput } from "../lib/currency";
import { normalizeRegion } from "../lib/taxonomy";
import { useTelegram } from "../telegram/TelegramProvider";
import { normalizeWebsite, safeExternalUrl, socialHandle, socialUrl } from "../lib/contacts";
import { UnsavedChangesDialog, useUnsavedChanges } from "../hooks/useUnsavedChanges";

const initial = { name: "", lastName: "", username: "", city: "tashkent-city", phone: "+998", email: "", totalFollowers: "10 000", averageReach: "25 000", engagementRate: "5.5", storiesPrice: "250 000", reelsPrice: "500 000", postPrice: "350 000", integrationPrice: "900 000", bio: "", portfolioUrl: "", instagram: "", tiktok: "", youtube: "" };
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
  if (form.instagram && !/^@?[A-Za-z0-9._]{1,30}$/.test(form.instagram.trim())) errors.instagram = t("form.validation.socialUsername");
  if (form.tiktok && !/^@?[A-Za-z0-9._]{1,30}$/.test(form.tiktok.trim())) errors.tiktok = t("form.validation.socialUsername");
  if (form.youtube && !safeExternalUrl(form.youtube)) errors.youtube = t("form.validation.website");
  if (form.portfolioUrl && !safeExternalUrl(form.portfolioUrl)) errors.portfolioUrl = t("form.validation.website");
  return errors;
}

export function BloggerProfileForm({ onCompleted }: { onCompleted?: () => void }) {
  const { t, language } = useI18n();
  const { haptic, user } = useTelegram();
  const [form, setForm] = useState(initial);
  const [selectedCategories, setSelectedCategories] = useState<string[]>(["lifestyle"]);
  const [barterEnabled, setBarterEnabled] = useState(true);
  const [touched, setTouched] = useState<Partial<Record<Field | "categories", boolean>>>({});
  const [existing, setExisting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState(false);
  const [toast, setToast] = useState("");
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
  const [pendingImage, setPendingImage] = useState<PendingProfileImage>();
  const [dirty, setDirty] = useState(false);
  const unsavedChanges = useUnsavedChanges(dirty);
  const errors = useMemo(() => validate(form, selectedCategories, t), [form, selectedCategories, t, language]);
  const valid = Object.keys(errors).length === 0;

  const update = (key: Field) => (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const value = key === "phone" ? formatPhoneInput(event.target.value) : event.target.value;
    setDirty(true);
    setForm((current) => ({ ...current, [key]: value }));
  };
  const numeric = (key: typeof numericFields[number]) => (event: React.ChangeEvent<HTMLInputElement>) => { setDirty(true); setForm((current) => ({ ...current, [key]: formatNumericInput(event.target.value) })); };
  const blur = (key: Field | "categories") => () => setTouched((current) => ({ ...current, [key]: true }));

  useEffect(() => {
    if (user?.username) setForm((current) => current.username ? current : { ...current, username: `@${user.username}` });
    getMyBloggerProfile().then((profile) => {
      const platform = (type: string) => socialHandle(profile.platforms.find((item) => item.type.toLowerCase() === type)?.url ?? "");
      setForm({ name: profile.name, lastName: profile.lastName ?? "", username: profile.username ?? "", city: normalizeRegion(profile.city) || initial.city, phone: formatPhoneInput(profile.phone ?? initial.phone), email: profile.email ?? "", totalFollowers: formatNumericInput(String(profile.totalFollowers)), averageReach: formatNumericInput(String(profile.averageReach ?? "")), engagementRate: String(profile.engagementRate ?? ""), storiesPrice: formatNumericInput(String(profile.storiesPrice ?? "")), reelsPrice: formatNumericInput(String(profile.reelsPrice ?? "")), postPrice: formatNumericInput(String(profile.postPrice ?? "")), integrationPrice: formatNumericInput(String(profile.integrationPrice ?? "")), bio: profile.bio ?? "", portfolioUrl: profile.portfolioItems[0]?.url ?? "", instagram: platform("instagram"), tiktok: platform("tiktok"), youtube: profile.platforms.find((item) => item.type.toLowerCase() === "youtube")?.url ?? "" });
      setSelectedCategories(profile.categories);
      setBarterEnabled(profile.barterEnabled);
      setAvatarUrl(profile.avatarUrl ?? null);
      setExisting(true);
      setDirty(false);
    }).catch(() => undefined);
  }, [user?.username]);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setTouched({ name: true, username: true, city: true, phone: true, email: true, categories: true, totalFollowers: true, averageReach: true, engagementRate: true, storiesPrice: true, reelsPrice: true });
    if (!valid || saving) return;
    try {
      setSaving(true);
      const portfolioUrl = normalizeWebsite(form.portfolioUrl);
      const youtubeUrl = normalizeWebsite(form.youtube);
      const input = { name: form.name.trim(), lastName: form.lastName.trim() || undefined, username: form.username.trim(), city: form.city.trim(), categories: selectedCategories, bio: form.bio.trim() || undefined, avatarUrl, phone: form.phone.trim(), email: form.email.trim() || undefined, totalFollowers: normalizeNumericInput(form.totalFollowers), averageReach: normalizeNumericInput(form.averageReach), engagementRate: Number(form.engagementRate), storiesPrice: normalizeNumericInput(form.storiesPrice), reelsPrice: normalizeNumericInput(form.reelsPrice), postPrice: normalizeNumericInput(form.postPrice) || undefined, integrationPrice: normalizeNumericInput(form.integrationPrice) || undefined, barterEnabled, portfolioItems: portfolioUrl ? [{ title: t("form.blogger.portfolioTitle"), type: "IMAGE" as const, url: portfolioUrl }] : [], platforms: ([form.instagram.trim() ? { type: "instagram", url: socialUrl("instagram", form.instagram) } : null, form.tiktok.trim() ? { type: "tiktok", url: socialUrl("tiktok", form.tiktok) } : null, youtubeUrl ? { type: "youtube", url: youtubeUrl } : null].filter(Boolean) as Array<{ type: string; url: string }>) };
      if (existing) await updateBloggerProfile(input); else { await createBloggerProfile(input); setExisting(true); }
      if (pendingImage instanceof File) {
        const media = await uploadProfileImage("blogger", pendingImage);
        setAvatarUrl(media.url);
      } else if (pendingImage === null && avatarUrl) {
        await deleteProfileImage("blogger");
        setAvatarUrl(null);
      }
      setPendingImage(undefined);
      setDirty(false);
      haptic.success();
      if (onCompleted) onCompleted();
      else setSuccess(true);
    } catch (error) {
      setToast(getApiErrorMessage(error, t("form.submitFailed"), {
        conflictMessage: t("form.bloggerProfileConflict"),
        validationMessages: {
          name: t("form.validation.name"),
          username: t("form.validation.username"),
          city: t("form.validation.city"),
          phone: t("form.validation.phone"),
          email: t("form.validation.email"),
          categories: t("form.validation.categories"),
          totalfollowers: t("form.validation.followers"),
          averagereach: t("form.validation.reach"),
          engagementrate: t("form.validation.er"),
          storiesprice: t("form.validation.stories"),
          reelsprice: t("form.validation.reels")
        }
      }));
    } finally {
      setSaving(false);
    }
  };

  return <form className="screen space-y-5 px-4 pt-5" noValidate onSubmit={submit}>
    <header><p className="text-sm font-semibold text-brand-muted">{t("form.blogger.eyebrow")}</p><h1 className="mt-1 text-3xl font-extrabold tracking-tight">{existing ? t("form.blogger.editTitle") : t("form.blogger.createTitle")}</h1><p className="mt-2 text-sm leading-5 text-brand-muted">{t("form.blogger.subtitle")}</p></header>
    <section className="grid gap-3"><h2 className="font-extrabold">{t("form.personal")}</h2><ProfileMediaPicker currentUrl={avatarUrl} disabled={saving} name={form.name || t("profile.telegramUser")} onChange={(image) => { setDirty(true); setPendingImage(image); }} pending={pendingImage} /><div className="grid grid-cols-2 gap-3"><Input error={touched.name ? errors.name : undefined} label={t("form.name")} onBlur={blur("name")} onChange={update("name")} placeholder={t("form.blogger.namePlaceholder")} required value={form.name} /><Input label={t("form.lastName")} onChange={update("lastName")} placeholder={t("form.blogger.lastNamePlaceholder")} value={form.lastName} /></div><div><Input error={touched.username ? errors.username : undefined} label={t("form.telegramUsername")} onBlur={blur("username")} onChange={update("username")} placeholder="@username" required value={form.username} /><p className="mt-1 text-xs text-brand-muted">{t("form.usernameHelper")}</p></div><RegionSelect error={touched.city ? errors.city : undefined} onChange={(event) => { setDirty(true); setForm((current) => ({ ...current, city: event.target.value })); }} required value={form.city} /><div className="grid grid-cols-2 gap-3"><Input error={touched.phone ? errors.phone : undefined} inputMode="tel" label={t("common.phone")} onBlur={blur("phone")} onChange={update("phone")} placeholder="+998 90 123 45 67" required value={form.phone} /><Input error={touched.email ? errors.email : undefined} label={t("form.emailOptional")} onBlur={blur("email")} onChange={update("email")} placeholder="you@email.com" type="email" value={form.email} /></div></section>
    <section><h2 className="font-extrabold">{t("form.audience")}</h2><div className="mt-3"><CategoryMultiSelect error={touched.categories ? errors.categories : undefined} onChange={(categories) => { setTouched((current) => ({ ...current, categories: true })); setDirty(true); setSelectedCategories(categories); }} required value={selectedCategories} /></div><div className="mt-4 grid gap-3"><div className="grid grid-cols-2 gap-3"><Input error={touched.totalFollowers ? errors.totalFollowers : undefined} inputMode="numeric" label={t("common.followers")} onBlur={blur("totalFollowers")} onChange={numeric("totalFollowers")} placeholder="25 000" required value={form.totalFollowers} /><Input error={touched.averageReach ? errors.averageReach : undefined} inputMode="numeric" label={t("common.reach")} onBlur={blur("averageReach")} onChange={numeric("averageReach")} placeholder="15 000" required value={form.averageReach} /></div><Input error={touched.engagementRate ? errors.engagementRate : undefined} label={t("search.er")} max="100" min="0.1" onBlur={blur("engagementRate")} onChange={update("engagementRate")} placeholder="5.5" required step="0.1" type="number" value={form.engagementRate} /></div></section>
    <section><h2 className="font-extrabold">{t("form.adPrices")}</h2><div className="mt-3 grid grid-cols-2 gap-3"><Input error={touched.storiesPrice ? errors.storiesPrice : undefined} inputMode="numeric" label={t("card.stories")} onBlur={blur("storiesPrice")} onChange={numeric("storiesPrice")} required value={form.storiesPrice} /><Input error={touched.reelsPrice ? errors.reelsPrice : undefined} inputMode="numeric" label={t("card.reels")} onBlur={blur("reelsPrice")} onChange={numeric("reelsPrice")} required value={form.reelsPrice} /><Input inputMode="numeric" label={t("card.post")} onChange={numeric("postPrice")} value={form.postPrice} /><Input inputMode="numeric" label={t("card.integration")} onChange={numeric("integrationPrice")} value={form.integrationPrice} /></div><button aria-pressed={barterEnabled} className="mt-3 flex w-full items-center justify-between rounded-2xl border border-brand-line bg-white p-4 text-left focus:outline-none focus:ring-4 focus:ring-blue-100" onClick={() => { setDirty(true); setBarterEnabled((value) => !value); }} type="button"><span><strong>{t("form.barterTitle")}</strong><span className="mt-1 block text-sm text-brand-muted">{t("form.barterSubtitle")}</span></span><span className={`h-7 w-12 rounded-full p-1 transition ${barterEnabled ? "bg-brand-blue" : "bg-slate-200"}`}><span className={`block h-5 w-5 rounded-full bg-white transition ${barterEnabled ? "translate-x-5" : ""}`} /></span></button></section>
    <section className="grid gap-3"><h2 className="font-extrabold">{t("form.portfolio")}</h2><Input error={touched.portfolioUrl ? errors.portfolioUrl : undefined} label={t("form.portfolioUrl")} onBlur={blur("portfolioUrl")} onChange={update("portfolioUrl")} placeholder="https://…" type="url" value={form.portfolioUrl} /><Textarea label={t("form.aboutMe")} onChange={update("bio")} placeholder={t("form.aboutMePlaceholder")} value={form.bio} /></section>
    <section className="grid gap-3"><h2 className="font-extrabold">{t("form.socialLinks")}</h2><Input error={touched.instagram ? errors.instagram : undefined} label={t("form.instagram")} onBlur={blur("instagram")} onChange={update("instagram")} placeholder="@username" value={form.instagram} /><Input error={touched.tiktok ? errors.tiktok : undefined} label={t("form.tiktok")} onBlur={blur("tiktok")} onChange={update("tiktok")} placeholder="@username" value={form.tiktok} /><Input error={touched.youtube ? errors.youtube : undefined} label={t("form.youtube")} onBlur={blur("youtube")} onChange={update("youtube")} placeholder="https://youtube.com/@channel" type="url" value={form.youtube} /></section>
    <Button aria-busy={saving} className="w-full" disabled={!valid || saving} type="submit"><Icon name="check" />{saving ? t("form.publishing") : t("form.publishProfile")}</Button><Toast message={toast} tone="error" /><Modal onClose={() => setSuccess(false)} open={success} title={t("form.successTitle")}><p className="text-sm leading-6 text-brand-muted">{t("form.successDescription")}</p><Button className="mt-5 w-full" onClick={() => { window.location.hash = "/profile"; }} type="button">{t("form.understood")}</Button></Modal><UnsavedChangesDialog guard={unsavedChanges} />{!onCompleted && <BottomNav />}
  </form>;
}
