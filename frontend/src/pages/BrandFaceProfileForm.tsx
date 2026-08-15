import { useCallback, useEffect, useState, type ChangeEvent } from "react";
import { getApiErrorMessage } from "../api/client";
import { deleteProfileImage, getMyBrandFaceProfile, upsertBrandFaceProfile, uploadProfileImage } from "../api/marketplace";
import { CategoryMultiSelect } from "../components/CategoryMultiSelect";
import { ProfileMediaPicker, type PendingProfileImage } from "../components/ProfileMediaPicker";
import { RegionSelect } from "../components/RegionSelect";
import { BottomNav, Button, Card, Input, LoadingState, Textarea, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { formatNumericInput, normalizeNumericInput } from "../lib/currency";
import { normalizeRegion } from "../lib/taxonomy";
import { useTelegram } from "../telegram/TelegramProvider";
import { UnsavedChangesDialog, useUnsavedChanges } from "../hooks/useUnsavedChanges";
import { useTelegramBackHandler } from "../hooks/useTelegramBackHandler";
import { notifyProfileDataChanged } from "../hooks/useProfileDataRefresh";

type BrandFaceForm = { name: string; city: string; languages: string; experience: string; instagram: string; telegram: string; portfolioUrl: string; collaborationPrice: string; description: string };
const emptyForm: BrandFaceForm = { name: "", city: "tashkent-city", languages: "", experience: "", instagram: "", telegram: "", portfolioUrl: "", collaborationPrice: "", description: "" };
const values = (value: string) => value.split(",").map((item) => item.trim()).filter(Boolean);

export function BrandFaceProfileForm({ onCompleted, onBackToRole }: { onCompleted?: () => void; onBackToRole?: () => void }) {
  const { t } = useI18n();
  const { haptic, user } = useTelegram();
  const [form, setForm] = useState<BrandFaceForm>(() => ({ ...emptyForm, name: user?.first_name ?? "", telegram: user?.username ? `@${user.username}` : "" }));
  const [categories, setCategories] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error" | "warning">("success");
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
  const [pendingImage, setPendingImage] = useState<PendingProfileImage>();
  const [dirty, setDirty] = useState(false);
  const unsavedChanges = useUnsavedChanges(dirty);
  const leaveForm = useCallback(() => {
    if (onBackToRole) {
      onBackToRole();
      return;
    }
    if (window.history.length > 1) window.history.back();
    else window.location.hash = "/profile";
  }, [onBackToRole]);
  const goBack = useCallback(() => {
    if (!saving) unsavedChanges.requestLeave(leaveForm);
  }, [leaveForm, saving, unsavedChanges]);
  useTelegramBackHandler(goBack, Boolean(onCompleted));
  const set = (key: keyof BrandFaceForm) => (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    setDirty(true);
    setForm((current) => ({ ...current, [key]: event.target.value }));
  };

  useEffect(() => {
    getMyBrandFaceProfile().then((profile) => {
      setForm({ name: profile.name, city: normalizeRegion(profile.city) || emptyForm.city, languages: profile.languages.join(", "), experience: profile.experience ?? "", instagram: profile.instagram ?? "", telegram: profile.telegram ?? "", portfolioUrl: profile.portfolioUrl ?? "", collaborationPrice: profile.collaborationPrice ? formatNumericInput(String(profile.collaborationPrice)) : "", description: profile.description ?? "" });
      setCategories(profile.categories);
      setAvatarUrl(profile.avatarUrl ?? null);
      setDirty(false);
    }).catch(() => undefined).finally(() => setLoading(false));
  }, []);

  const valid = Boolean(form.name.trim() && form.city && values(form.languages).length && categories.length && /^@[A-Za-z0-9_]{5,32}$/.test(form.telegram));
  const submit = async () => {
    if (!valid || saving) return;
    try {
      setSaving(true);
      await upsertBrandFaceProfile({ name: form.name.trim(), city: form.city, age: null, gender: null, languages: values(form.languages), categories, experience: form.experience.trim() || null, instagram: form.instagram.trim() || null, telegram: form.telegram.trim(), portfolioUrl: form.portfolioUrl.trim() || null, collaborationPrice: form.collaborationPrice ? normalizeNumericInput(form.collaborationPrice) : null, description: form.description.trim() || null, avatarUrl });
      let mediaWarning = "";
      try {
        if (pendingImage instanceof File) {
          const media = await uploadProfileImage("brand-face", pendingImage);
          setAvatarUrl(media.url);
        } else if (pendingImage === null && avatarUrl) {
          await deleteProfileImage("brand-face");
          setAvatarUrl(null);
        }
      } catch (error) {
        mediaWarning = getApiErrorMessage(error, t("error.profile_media_unavailable"));
      }
      setPendingImage(undefined);
      setDirty(false);
      notifyProfileDataChanged();
      if (onCompleted) {
        if (mediaWarning) {
          haptic.warning();
          window.sessionStorage.setItem("bloggerbazar.onboarding.media-warning", mediaWarning);
        } else haptic.success();
        onCompleted();
      } else {
        setToastTone(mediaWarning ? "warning" : "success");
        setToast(mediaWarning || t("brandFace.saved"));
      }
    } catch (error) {
      setToastTone("error");
      setToast(getApiErrorMessage(error, t("brandFace.failed"), { validationMessages: { name: t("form.validation.name"), city: t("form.validation.city"), languages: t("brandFace.languagesRequired"), categories: t("form.validation.categories"), telegram: t("form.validation.username") } }));
    } finally { setSaving(false); }
  };

  if (loading) return <div className={`screen ${onCompleted ? "screen--without-nav" : "screen--with-nav"}`}><LoadingState /></div>;
  return <div className={`screen ${onCompleted ? "screen--without-nav" : "screen--with-nav"} space-y-5 pt-5`}><header data-content-header><a className="text-sm font-bold text-brand-blue" href="#/profile">← {t("profile.title")}</a><p className="mt-5 text-sm font-bold text-brand-blue">{t("brandFace.eyebrow")}</p><h1 className="mt-1 text-3xl font-extrabold">{t("brandFace.createTitle")}</h1><p className="mt-2 text-sm leading-6 text-brand-muted">{t("brandFace.subtitle")}</p></header><ProfileMediaPicker currentUrl={avatarUrl} disabled={saving} name={form.name || t("profile.telegramUser")} onChange={(image) => { setDirty(true); setPendingImage(image); }} pending={pendingImage} /><Card className="grid gap-4"><Input label={t("form.name")} onChange={set("name")} placeholder={t("form.blogger.namePlaceholder")} required value={form.name} /><RegionSelect onChange={(event) => { setDirty(true); setForm((current) => ({ ...current, city: event.target.value })); }} required value={form.city} /><Input label={t("brandFace.languages")} onChange={set("languages")} placeholder={t("brandFace.languagesPlaceholder")} required value={form.languages} /><CategoryMultiSelect onChange={(nextCategories) => { setDirty(true); setCategories(nextCategories); }} required value={categories} /><Input label={t("form.telegramUsername")} onChange={set("telegram")} placeholder="@username" required value={form.telegram} /><Input label={t("brandFace.instagram")} onChange={set("instagram")} placeholder="@username" value={form.instagram} /><Input label={t("brandFace.portfolio")} onChange={set("portfolioUrl")} placeholder="https://..." type="url" value={form.portfolioUrl} /><Input inputMode="numeric" label={t("brandFace.price")} onChange={(event) => { setDirty(true); setForm((current) => ({ ...current, collaborationPrice: formatNumericInput(event.target.value) })); }} placeholder="200 000" value={form.collaborationPrice} /><Textarea label={t("brandFace.experience")} maxLength={1000} onChange={set("experience")} placeholder={t("brandFace.experiencePlaceholder")} value={form.experience} /><Textarea label={t("brandFace.description")} maxLength={1000} onChange={set("description")} placeholder={t("brandFace.descriptionPlaceholder")} value={form.description} /></Card><Button aria-busy={saving} className="w-full" disabled={!valid || saving} onClick={submit} type="button">{saving ? t("brandFace.saving") : t("brandFace.submit")}</Button><Toast message={toast} tone={toastTone} /><UnsavedChangesDialog guard={unsavedChanges} />{!onCompleted && <BottomNav />}</div>;
}
