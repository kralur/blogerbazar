import { useEffect, useState, type ChangeEvent } from "react";
import { getMyBrandFaceProfile, upsertBrandFaceProfile } from "../api/marketplace";
import { getApiErrorMessage } from "../api/client";
import { BottomNav, Button, Card, Input, LoadingState, Textarea, Toast } from "../components/ui";
import { useI18n } from "../i18n";
import { useTelegram } from "../telegram/TelegramProvider";

type BrandFaceForm = { name: string; city: string; languages: string; categories: string; experience: string; instagram: string; telegram: string; portfolioUrl: string; collaborationPrice: string; description: string };
const emptyForm: BrandFaceForm = { name: "", city: "", languages: "", categories: "", experience: "", instagram: "", telegram: "", portfolioUrl: "", collaborationPrice: "", description: "" };
const values = (value: string) => value.split(",").map((item) => item.trim()).filter(Boolean);

export function BrandFaceProfileForm({ onCompleted }: { onCompleted?: () => void }) {
  const { t } = useI18n();
  const { user } = useTelegram();
  const [form, setForm] = useState<BrandFaceForm>(() => ({ ...emptyForm, name: user?.first_name ?? "", telegram: user?.username ? `@${user.username}` : "" }));
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [toast, setToast] = useState("");
  const [failed, setFailed] = useState(false);
  const set = (key: keyof BrandFaceForm) => (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => setForm((current) => ({ ...current, [key]: event.target.value }));

  useEffect(() => { getMyBrandFaceProfile().then((profile) => setForm({ name: profile.name, city: profile.city, languages: profile.languages.join(", "), categories: profile.categories.join(", "), experience: profile.experience ?? "", instagram: profile.instagram ?? "", telegram: profile.telegram ?? "", portfolioUrl: profile.portfolioUrl ?? "", collaborationPrice: profile.collaborationPrice ? String(profile.collaborationPrice) : "", description: profile.description ?? "" })).catch(() => undefined).finally(() => setLoading(false)); }, []);
  const valid = Boolean(form.name.trim() && form.city.trim() && values(form.languages).length && values(form.categories).length && /^@[A-Za-z0-9_]{5,32}$/.test(form.telegram));

  const submit = async () => {
    try {
      setSaving(true); setFailed(false);
      await upsertBrandFaceProfile({ name: form.name.trim(), city: form.city.trim(), age: null, gender: null, languages: values(form.languages), categories: values(form.categories), experience: form.experience.trim() || null, instagram: form.instagram.trim() || null, telegram: form.telegram.trim(), portfolioUrl: form.portfolioUrl.trim() || null, collaborationPrice: form.collaborationPrice ? Number(form.collaborationPrice) : null, description: form.description.trim() || null, avatarUrl: null });
      if (onCompleted) onCompleted();
      else setToast(t("brandFace.saved"));
    } catch (error) {
      setToast(getApiErrorMessage(error, t("brandFace.failed"), {
        validationMessages: {
          name: t("form.validation.name"),
          city: t("form.validation.city"),
          languages: t("brandFace.languagesRequired"),
          categories: t("form.validation.categories"),
          telegram: t("form.validation.username")
        }
      }));
      setFailed(true);
    } finally { setSaving(false); }
  };

  if (loading) return <div className="screen"><LoadingState /></div>;
  return <div className="screen space-y-5 pb-28 pt-5"><header><a className="text-sm font-bold text-brand-blue" href="#/profile">← {t("profile.title")}</a><p className="mt-5 text-sm font-bold text-brand-blue">{t("brandFace.eyebrow")}</p><h1 className="mt-1 text-3xl font-extrabold">{t("brandFace.createTitle")}</h1><p className="mt-2 text-sm leading-6 text-brand-muted">{t("brandFace.subtitle")}</p></header><Card className="grid gap-4"><Input label={t("form.name")} onChange={set("name")} value={form.name} /><Input label={t("common.city")} onChange={set("city")} value={form.city} /><Input label={t("brandFace.languages")} onChange={set("languages")} placeholder={t("brandFace.languagesPlaceholder")} value={form.languages} /><Input label={t("common.categories")} onChange={set("categories")} placeholder={t("brandFace.categoriesPlaceholder")} value={form.categories} /><Input label={t("form.telegramUsername")} onChange={set("telegram")} value={form.telegram} /><Input label={t("brandFace.instagram")} onChange={set("instagram")} value={form.instagram} /><Input label={t("brandFace.portfolio")} onChange={set("portfolioUrl")} type="url" value={form.portfolioUrl} /><Input label={t("brandFace.price")} min="1" onChange={set("collaborationPrice")} type="number" value={form.collaborationPrice} /><Textarea label={t("brandFace.experience")} onChange={set("experience")} value={form.experience} /><Textarea label={t("brandFace.description")} onChange={set("description")} value={form.description} /></Card><Button className="w-full" disabled={!valid || saving} onClick={submit} type="button">{saving ? t("brandFace.saving") : t("brandFace.submit")}</Button>{failed && <p className="text-center text-sm text-red-600">{t("brandFace.failed")}</p>}<Toast message={toast} />{!onCompleted && <BottomNav />}</div>;
}
