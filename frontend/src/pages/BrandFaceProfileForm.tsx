import { useCallback, useEffect, useMemo, useState, type ChangeEvent, type FormEvent } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { deleteProfileImage, getMyBrandFaceProfile, upsertBrandFaceProfile, uploadProfileImage } from "../api/marketplace";
import { CategoryMultiSelect } from "../components/CategoryMultiSelect";
import { ProfileMediaPicker, type PendingProfileImage } from "../components/ProfileMediaPicker";
import { RegionSelect } from "../components/RegionSelect";
import { FixedActionBar, ReviewItem, ReviewSection, WizardErrorSummary, WizardHeader, WizardLayout, WizardStep } from "../components/Wizard";
import { Input, Textarea, Toast } from "../components/ui";
import { UnsavedChangesDialog, useUnsavedChanges } from "../hooks/useUnsavedChanges";
import { notifyProfileDataChanged } from "../hooks/useProfileDataRefresh";
import { useTelegramBackHandler } from "../hooks/useTelegramBackHandler";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatCurrency, formatNumericInput, normalizeNumericInput } from "../lib/currency";
import { isOtherCategory, normalizeRegion, otherCategoryPrefix } from "../lib/taxonomy";
import { useTelegram } from "../telegram/TelegramProvider";

type BrandFaceForm = {
  name: string;
  city: string;
  languages: string;
  experience: string;
  instagram: string;
  telegram: string;
  portfolioUrl: string;
  collaborationPrice: string;
  description: string;
};

type Field = keyof BrandFaceForm | "categories";
type Errors = Partial<Record<Field, string>>;
type Step = 0 | 1 | 2 | 3;

const initialForm: BrandFaceForm = {
  name: "",
  city: "tashkent-city",
  languages: "",
  experience: "",
  instagram: "",
  telegram: "",
  portfolioUrl: "",
  collaborationPrice: "",
  description: ""
};

const stepFields: Record<Exclude<Step, 3>, Field[]> = {
  0: ["name", "city", "languages"],
  1: ["categories", "telegram"],
  2: ["instagram", "portfolioUrl", "collaborationPrice", "experience", "description"]
};

const usernamePattern = /^@[A-Za-z0-9_]{5,32}$/;
const instagramPattern = /^@[A-Za-z0-9._]{1,30}$/;

function initialBrandFaceForm(firstName?: string, username?: string) {
  const normalizedUsername = username?.trim().replace(/^@+/, "");
  return {
    ...initialForm,
    name: firstName?.trim() ?? "",
    telegram: normalizedUsername ? `@${normalizedUsername}` : ""
  };
}

function normalizedLanguages(value: string) {
  const unique = new Set<string>();
  const result: string[] = [];
  for (const item of value.split(",").map((entry) => entry.trim()).filter(Boolean)) {
    const key = item.toLocaleLowerCase();
    if (!unique.has(key)) {
      unique.add(key);
      result.push(item);
    }
  }
  return result;
}

function isSecureUrl(value: string) {
  if (!/^https:\/\//i.test(value.trim())) return false;
  try {
    const url = new URL(value.trim());
    return url.protocol === "https:" && Boolean(url.host);
  } catch {
    return false;
  }
}

function validate(form: BrandFaceForm, categories: string[], t: (key: string) => string): Errors {
  const errors: Errors = {};
  const languages = normalizedLanguages(form.languages);
  const price = form.collaborationPrice ? normalizeNumericInput(form.collaborationPrice) : null;

  if (!form.name.trim() || form.name.trim().length > 100) errors.name = t("form.validation.name");
  if (!form.city.trim()) errors.city = t("form.validation.city");
  if (!languages.length) errors.languages = t("brandFace.languagesRequired");
  else if (languages.length > 5) errors.languages = t("brandFace.languagesLimit");
  else if (languages.some((language) => language.length > 32)) errors.languages = t("brandFace.languageTooLong");
  if (!categories.length) errors.categories = t("form.validation.categories");
  else if (categories.length > 5) errors.categories = t("brandFace.categoriesLimit");
  else if (categories.some((category) => category.trim().length > 50)) errors.categories = t("brandFace.categoryTooLong");
  if (!usernamePattern.test(form.telegram.trim())) errors.telegram = t("form.validation.username");
  if (form.instagram.trim() && !instagramPattern.test(form.instagram.trim())) errors.instagram = t("form.validation.socialUsername");
  if (form.portfolioUrl.trim() && !isSecureUrl(form.portfolioUrl)) errors.portfolioUrl = t("form.validation.website");
  if (price !== null && price <= 0) errors.collaborationPrice = t("brandFace.priceInvalid");
  if (form.experience.length > 2000) errors.experience = t("brandFace.textTooLong");
  if (form.description.length > 2000) errors.description = t("brandFace.textTooLong");
  return errors;
}

function validationMessages(t: (key: string) => string): Record<string, string> {
  return {
    name: t("form.validation.name"),
    city: t("form.validation.city"),
    languages: t("brandFace.languagesRequired"),
    categories: t("form.validation.categories"),
    telegram: t("form.validation.username"),
    instagram: t("form.validation.socialUsername"),
    portfoliourl: t("form.validation.website"),
    collaborationprice: t("brandFace.priceInvalid"),
    experience: t("brandFace.textTooLong"),
    description: t("brandFace.textTooLong")
  };
}

function fieldFromServerName(value: string): Field | undefined {
  const field = value.toLowerCase().replace(/\[.*$/, "");
  if (field === "portfoliourl") return "portfolioUrl";
  if (field === "collaborationprice") return "collaborationPrice";
  return ["name", "city", "languages", "categories", "telegram", "instagram", "experience", "description"].includes(field) ? field as Field : undefined;
}

function stepForField(field: Field): Step {
  if (stepFields[0].includes(field)) return 0;
  if (stepFields[1].includes(field)) return 1;
  return 2;
}

export function BrandFaceProfileForm({ onCompleted, onBackToRole }: { onCompleted?: () => void; onBackToRole?: () => void }) {
  const { language, t } = useI18n();
  const { haptic, isTelegram, user } = useTelegram();
  const [form, setForm] = useState(() => initialBrandFaceForm(user?.first_name, user?.username));
  const [categories, setCategories] = useState<string[]>([]);
  const [touched, setTouched] = useState<Partial<Record<Field, boolean>>>({});
  const [edited, setEdited] = useState<Partial<Record<"name" | "telegram", boolean>>>({});
  const [serverErrors, setServerErrors] = useState<Errors>({});
  const [step, setStep] = useState<Step>(0);
  const [existing, setExisting] = useState(false);
  const [hydrated, setHydrated] = useState(Boolean(onCompleted));
  const [saving, setSaving] = useState(false);
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error" | "warning">("success");
  const [serverSummary, setServerSummary] = useState("");
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
  const [pendingImage, setPendingImage] = useState<PendingProfileImage>();
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  const unsavedChanges = useUnsavedChanges(dirty);
  const clientErrors = useMemo(() => validate(form, categories, t), [categories, form, language, t]);
  const errors = useMemo(() => ({ ...clientErrors, ...serverErrors }), [clientErrors, serverErrors]);
  const currentStepValid = step === 3 || stepFields[step].every((field) => !errors[field]);
  const stepTitles = [t("wizard.brandFaceAboutStep"), t("wizard.brandFacePositioningStep"), t("wizard.brandFacePortfolioStep"), t("wizard.brandFaceReviewStep")];

  useEffect(() => {
    setForm((current) => ({
      ...current,
      name: current.name || edited.name ? current.name : user?.first_name?.trim() ?? current.name,
      telegram: current.telegram || edited.telegram ? current.telegram : user?.username ? `@${user.username.replace(/^@+/, "")}` : current.telegram
    }));
  }, [edited.name, edited.telegram, user?.first_name, user?.username]);

  useEffect(() => {
    if (onCompleted) return;
    let active = true;
    getMyBrandFaceProfile().then((profile) => {
      if (!active) return;
      setForm({
        name: profile.name,
        city: normalizeRegion(profile.city) || initialForm.city,
        languages: profile.languages.join(", "),
        experience: profile.experience ?? "",
        instagram: profile.instagram ?? "",
        telegram: profile.telegram ?? "",
        portfolioUrl: profile.portfolioUrl ?? "",
        collaborationPrice: profile.collaborationPrice ? formatNumericInput(String(profile.collaborationPrice)) : "",
        description: profile.description ?? ""
      });
      setCategories(profile.categories);
      setAvatarUrl(profile.avatarUrl ?? null);
      setExisting(true);
      setDirty(false);
    }).catch(() => undefined).finally(() => {
      if (active) setHydrated(true);
    });
    return () => { active = false; };
  }, [onCompleted]);

  useEffect(() => {
    if (!(pendingImage instanceof File)) {
      setPreviewUrl(null);
      return;
    }
    const nextPreviewUrl = URL.createObjectURL(pendingImage);
    setPreviewUrl(nextPreviewUrl);
    return () => URL.revokeObjectURL(nextPreviewUrl);
  }, [pendingImage]);

  const clearServerError = useCallback((field: Field) => {
    setServerErrors((current) => ({ ...current, [field]: undefined }));
    setServerSummary("");
  }, []);

  const update = (key: Exclude<keyof BrandFaceForm, "city" | "collaborationPrice">) => (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    setDirty(true);
    if (key === "name" || key === "telegram") setEdited((current) => ({ ...current, [key]: true }));
    clearServerError(key);
    setForm((current) => ({ ...current, [key]: event.target.value }));
  };

  const updatePrice = (event: ChangeEvent<HTMLInputElement>) => {
    setDirty(true);
    clearServerError("collaborationPrice");
    setForm((current) => ({ ...current, collaborationPrice: formatNumericInput(event.target.value) }));
  };

  const selectCity = (event: ChangeEvent<HTMLSelectElement>) => {
    setDirty(true);
    clearServerError("city");
    setForm((current) => ({ ...current, city: event.target.value }));
  };

  const updateCategories = (nextCategories: string[]) => {
    setDirty(true);
    clearServerError("categories");
    setCategories(nextCategories);
  };

  const blur = (field: Field) => () => setTouched((current) => ({ ...current, [field]: true }));

  const markStepTouched = useCallback((currentStep: Exclude<Step, 3>) => {
    setTouched((current) => ({ ...current, ...Object.fromEntries(stepFields[currentStep].map((field) => [field, true])) }));
  }, []);

  const continueStep = useCallback(() => {
    if (step === 3) return;
    markStepTouched(step);
    if (!stepFields[step].every((field) => !errors[field])) {
      haptic.error();
      return;
    }
    haptic.selection();
    setStep((current) => Math.min(current + 1, 3) as Step);
  }, [errors, haptic, markStepTouched, step]);

  const leaveForm = useCallback(() => {
    if (onBackToRole) {
      onBackToRole();
      return;
    }
    if (window.history.length > 1) window.history.back();
    else window.location.hash = "/profile";
  }, [onBackToRole]);

  const goBack = useCallback(() => {
    if (saving) return;
    if (step > 0) {
      haptic.selection();
      setStep((current) => Math.max(current - 1, 0) as Step);
      return;
    }
    unsavedChanges.requestLeave(leaveForm);
  }, [haptic, leaveForm, saving, step, unsavedChanges]);

  useTelegramBackHandler(goBack, Boolean(onCompleted));

  const focusField = useCallback((field: Field) => {
    window.requestAnimationFrame(() => {
      const container = document.querySelector<HTMLElement>(`[data-wizard-field="${field}"]`);
      const control = container?.querySelector<HTMLElement>("input, textarea, select, button");
      control?.focus();
    });
  }, []);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const allFields = Object.values(stepFields).flat();
    setTouched(Object.fromEntries(allFields.map((field) => [field, true])));
    if (Object.keys(clientErrors).length) {
      const field = allFields.find((current) => clientErrors[current]);
      if (field) {
        setStep(stepForField(field));
        focusField(field);
      }
      haptic.error();
      return;
    }
    if (saving) return;

    try {
      setSaving(true);
      setServerErrors({});
      setServerSummary("");
      await upsertBrandFaceProfile({
        name: form.name.trim(),
        city: form.city,
        age: null,
        gender: null,
        languages: normalizedLanguages(form.languages),
        categories,
        experience: form.experience.trim() || null,
        instagram: form.instagram.trim() || null,
        telegram: form.telegram.trim(),
        portfolioUrl: form.portfolioUrl.trim() || null,
        collaborationPrice: form.collaborationPrice ? normalizeNumericInput(form.collaborationPrice) : null,
        description: form.description.trim() || null,
        avatarUrl
      });
      setExisting(true);
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
        return;
      }
      haptic.success();
      setToastTone(mediaWarning ? "warning" : "success");
      setToast(mediaWarning || t("brandFace.saved"));
    } catch (error) {
      const message = getApiErrorMessage(error, t("brandFace.failed"), { validationMessages: validationMessages(t) });
      if (error instanceof ApiError && error.code === "validation_failed") {
        const fields = error.validationFields.map(fieldFromServerName).filter((field): field is Field => Boolean(field));
        if (fields.length) {
          setServerErrors(Object.fromEntries(fields.map((field) => [field, validationMessages(t)[field === "portfolioUrl" ? "portfoliourl" : field.toLowerCase()]])));
          setTouched((current) => ({ ...current, ...Object.fromEntries(fields.map((field) => [field, true])) }));
          const firstField = fields[0];
          setStep(stepForField(firstField));
          focusField(firstField);
        }
        setServerSummary(message);
      } else {
        setToastTone("error");
        setToast(message);
      }
    } finally {
      setSaving(false);
    }
  };

  if (!hydrated) return <div className="screen screen--with-nav"><div aria-busy="true" className="wizard-loading" /></div>;

  const reviewAvatarUrl = pendingImage === null ? null : previewUrl ?? avatarUrl;
  const progressLabel = t("wizard.stepOf", { current: step + 1, total: 4 });
  const submitLabel = existing ? t("wizard.saveChanges") : t("wizard.createProfile");
  const actionLabel = step === 3 ? (saving ? t("brandFace.saving") : submitLabel) : t("wizard.continue");

  return <form noValidate onSubmit={submit}>
    <WizardLayout actionBar={<FixedActionBar key={step} backLabel={t("common.back")} continueLabel={actionLabel} disabled={step === 3 ? false : !currentStepValid} loading={saving} onBack={goBack} onPrimary={step === 3 ? undefined : continueStep} submit={step === 3} />}>
      <WizardHeader backLabel={t("common.back")} onBack={goBack} progressLabel={progressLabel} showBackButton={!isTelegram} step={step + 1} stepTitle={stepTitles[step]} totalSteps={4} />
      <WizardErrorSummary message={serverSummary} />
      {step === 0 && <WizardStep stepKey={stepTitles[0]}>
        <div className="wizard-fields">
          <div data-wizard-field="name"><Input className="wizard-input" error={touched.name ? errors.name : undefined} label={t("form.name")} maxLength={100} name="name" onBlur={blur("name")} onChange={update("name")} placeholder={t("form.blogger.namePlaceholder")} required value={form.name} /></div>
          <div data-wizard-field="city"><RegionSelect className="wizard-region-select" error={touched.city ? errors.city : undefined} onChange={selectCity} required value={form.city} /></div>
          <div data-wizard-field="languages"><Input className="wizard-input" error={touched.languages ? errors.languages : undefined} label={t("brandFace.languages")} maxLength={164} name="languages" onBlur={blur("languages")} onChange={update("languages")} placeholder={t("brandFace.languagesPlaceholder")} required value={form.languages} /><p className="wizard-field-helper">{t("brandFace.languagesHelper")}</p></div>
        </div>
      </WizardStep>}
      {step === 1 && <WizardStep stepKey={stepTitles[1]}>
        <div className="wizard-fields">
          <div data-wizard-field="categories"><CategoryMultiSelect error={touched.categories ? errors.categories : undefined} onChange={updateCategories} required value={categories} /></div>
          <div data-wizard-field="telegram"><Input className="wizard-input" error={touched.telegram ? errors.telegram : undefined} label={t("form.telegramUsername")} name="telegram" onBlur={blur("telegram")} onChange={update("telegram")} placeholder="@username" required value={form.telegram} /><p className="wizard-field-helper">{t("form.usernameHelper")}</p></div>
        </div>
      </WizardStep>}
      {step === 2 && <WizardStep stepKey={stepTitles[2]}>
        <div className="wizard-fields">
          <ProfileMediaPicker className="wizard-media-picker" currentUrl={avatarUrl} disabled={saving} name={form.name || t("profile.telegramUser")} onChange={(image) => { setDirty(true); setPendingImage(image); }} pending={pendingImage} />
          <div data-wizard-field="instagram"><Input className="wizard-input" error={touched.instagram ? errors.instagram : undefined} label={t("brandFace.instagram")} name="instagram" onBlur={blur("instagram")} onChange={update("instagram")} placeholder="@username" value={form.instagram} /></div>
          <div data-wizard-field="portfolioUrl"><Input className="wizard-input" error={touched.portfolioUrl ? errors.portfolioUrl : undefined} label={t("brandFace.portfolio")} name="portfolioUrl" onBlur={blur("portfolioUrl")} onChange={update("portfolioUrl")} placeholder="https://..." type="url" value={form.portfolioUrl} /></div>
          <div data-wizard-field="collaborationPrice"><Input className="wizard-input" error={touched.collaborationPrice ? errors.collaborationPrice : undefined} inputMode="numeric" label={t("brandFace.price")} name="collaborationPrice" onBlur={blur("collaborationPrice")} onChange={updatePrice} placeholder="200 000" value={form.collaborationPrice} /></div>
          <div data-wizard-field="experience"><Textarea className="wizard-input" error={touched.experience ? errors.experience : undefined} label={t("brandFace.experience")} maxLength={2000} name="experience" onBlur={blur("experience")} onChange={update("experience")} placeholder={t("brandFace.experiencePlaceholder")} value={form.experience} /></div>
          <div data-wizard-field="description"><Textarea className="wizard-input" error={touched.description ? errors.description : undefined} label={t("brandFace.description")} maxLength={2000} name="description" onBlur={blur("description")} onChange={update("description")} placeholder={t("brandFace.descriptionPlaceholder")} value={form.description} /></div>
        </div>
      </WizardStep>}
      {step === 3 && <WizardStep stepKey={stepTitles[3]}>
        <div className="wizard-review">
          {reviewAvatarUrl && <img alt={t("profileMedia.title")} className="wizard-review__logo" src={reviewAvatarUrl} />}
          <ReviewSection editAriaLabel={t("wizard.editSection", { section: stepTitles[0] })} editLabel={t("common.edit")} onEdit={() => setStep(0)} title={stepTitles[0]}>
            <ReviewItem label={t("form.name")} value={form.name.trim()} />
            <ReviewItem label={t("common.city")} value={cityLabel(form.city, language)} />
            <ReviewItem label={t("brandFace.languages")} value={normalizedLanguages(form.languages).join(", ")} />
          </ReviewSection>
          <ReviewSection editAriaLabel={t("wizard.editSection", { section: stepTitles[1] })} editLabel={t("common.edit")} onEdit={() => setStep(1)} title={stepTitles[1]}>
            <ReviewItem label={t("common.categories")} value={categories.map((category) => isOtherCategory(category) ? category.slice(otherCategoryPrefix.length) : categoryLabel(category, language)).join(", ")} />
            <ReviewItem label={t("form.telegramUsername")} value={form.telegram.trim()} />
          </ReviewSection>
          <ReviewSection editAriaLabel={t("wizard.editSection", { section: stepTitles[2] })} editLabel={t("common.edit")} onEdit={() => setStep(2)} title={stepTitles[2]}>
            <ReviewItem label={t("brandFace.instagram")} value={form.instagram.trim()} />
            <ReviewItem label={t("brandFace.portfolio")} value={form.portfolioUrl.trim()} />
            <ReviewItem label={t("brandFace.price")} value={form.collaborationPrice ? formatCurrency(normalizeNumericInput(form.collaborationPrice)) : undefined} />
            <ReviewItem label={t("brandFace.experience")} value={form.experience.trim()} />
            <ReviewItem label={t("brandFace.description")} value={form.description.trim()} />
          </ReviewSection>
        </div>
      </WizardStep>}
    </WizardLayout>
    <Toast message={toast} tone={toastTone} />
    <UnsavedChangesDialog guard={unsavedChanges} />
  </form>;
}
