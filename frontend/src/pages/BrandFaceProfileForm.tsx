import { useCallback, useEffect, useState, type ChangeEvent, type FormEvent } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { addBrandFacePhoto, brandFaceFormats, deleteProfileImage, getMyBrandFaceProfile, removeBrandFacePhoto, upsertBrandFaceProfile, uploadProfileImage, type BrandFaceFormat } from "../api/marketplace";
import { BrandFaceGallery } from "../components/BrandFaceGallery";
import { LanguageMultiSelect } from "../components/LanguageMultiSelect";
import { normalizeSpokenLanguages, type SpokenLanguage } from "../lib/languages";
import { CategoryMultiSelect } from "../components/CategoryMultiSelect";
import { ProfileMediaPicker, type PendingProfileImage } from "../components/ProfileMediaPicker";
import { RegionSelect } from "../components/RegionSelect";
import { TelegramHandleField, useTelegramHandle } from "../components/TelegramHandleField";
import { FixedActionBar, ReviewItem, ReviewSection, WizardErrorSummary, WizardHeader, WizardLayout, WizardStep } from "../components/Wizard";
import { Button, Icon, Input, Modal, Textarea, Toast } from "../components/ui";
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
  gender: string;
  age: string;
  instagram: string;
  showreelUrl: string;
  telegram: string;
  portfolioUrl: string;
  collaborationPrice: string;
  description: string;
};

type Field = keyof BrandFaceForm | "categories" | "formats" | "languages";
type Errors = Partial<Record<Field, string>>;
type Step = 0 | 1 | 2 | 3;

const initialForm: BrandFaceForm = {
  name: "",
  city: "tashkent-city",
  gender: "",
  age: "",
  instagram: "",
  showreelUrl: "",
  telegram: "",
  portfolioUrl: "",
  collaborationPrice: "",
  description: ""
};

const stepFields: Record<Exclude<Step, 3>, Field[]> = {
  0: ["name", "city", "gender", "age", "languages"],
  1: ["categories", "formats"],
  2: ["instagram", "showreelUrl", "portfolioUrl", "collaborationPrice", "description"]
};

const instagramPattern = /^@[A-Za-z0-9._]{1,30}$/;

function initialBrandFaceForm(firstName?: string, username?: string) {
  const normalizedUsername = username?.trim().replace(/^@+/, "");
  return {
    ...initialForm,
    name: firstName?.trim() ?? "",
    telegram: normalizedUsername ? `@${normalizedUsername}` : ""
  };
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

type Translate = (key: string, params?: Record<string, string | number>) => string;

const minAge = 16;
const maxAge = 80;

function validate(form: BrandFaceForm, languages: SpokenLanguage[], categories: string[], formats: BrandFaceFormat[], t: Translate): Errors {
  const errors: Errors = {};
  const price = form.collaborationPrice ? normalizeNumericInput(form.collaborationPrice) : null;

  if (!form.name.trim() || form.name.trim().length > 100) errors.name = t("form.validation.name");
  if (!form.city.trim()) errors.city = t("form.validation.city");
  // A business picks a face by gender, age and format first (QA Q20).
  if (form.gender !== "female" && form.gender !== "male") errors.gender = t("brandFace.genderRequired");
  const age = Number(form.age);
  if (!form.age.trim() || !Number.isInteger(age) || age < minAge || age > maxAge) errors.age = t("brandFace.ageInvalid", { min: minAge, max: maxAge });
  if (!formats.length) errors.formats = t("brandFace.formatsRequired");
  if (!languages.length) errors.languages = t("brandFace.languagesRequired");
  else if (languages.length > 5) errors.languages = t("brandFace.languagesLimit");
  if (!categories.length) errors.categories = t("form.validation.categories");
  else if (categories.length > 5) errors.categories = t("brandFace.categoriesLimit");
  else if (categories.some((category) => category.trim().length > 50)) errors.categories = t("brandFace.categoryTooLong");
  // Instagram is required: a business looks at the brand face's photos there before making an offer (QA Q17).
  if (!form.instagram.trim()) errors.instagram = t("brandFace.instagramRequired");
  else if (!instagramPattern.test(form.instagram.trim())) errors.instagram = t("form.validation.socialUsername");
  if (form.portfolioUrl.trim() && !isSecureUrl(form.portfolioUrl)) errors.portfolioUrl = t("form.validation.website");
  if (form.showreelUrl.trim() && !isSecureUrl(form.showreelUrl)) errors.showreelUrl = t("form.validation.website");
  if (price !== null && price <= 0) errors.collaborationPrice = t("brandFace.priceInvalid");
  if (form.description.length > 2000) errors.description = t("brandFace.textTooLong");
  return errors;
}

function validationMessages(t: Translate): Record<string, string> {
  return {
    name: t("form.validation.name"),
    city: t("form.validation.city"),
    languages: t("brandFace.languagesRequired"),
    categories: t("form.validation.categories"),
    telegram: t("form.validation.username"),
    instagram: t("form.validation.socialUsername"),
    portfoliourl: t("form.validation.website"),
    showreelurl: t("form.validation.website"),
    gender: t("brandFace.genderRequired"),
    age: t("brandFace.ageInvalid", { min: minAge, max: maxAge }),
    formats: t("brandFace.formatsRequired"),
    collaborationprice: t("brandFace.priceInvalid"),
    description: t("brandFace.textTooLong")
  };
}

function fieldFromServerName(value: string): Field | undefined {
  const field = value.toLowerCase().replace(/\[.*$/, "");
  if (field === "portfoliourl") return "portfolioUrl";
  if (field === "collaborationprice") return "collaborationPrice";
  if (field === "showreelurl") return "showreelUrl";
  return ["name", "city", "gender", "age", "languages", "categories", "formats", "telegram", "instagram", "description"].includes(field) ? field as Field : undefined;
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
  const telegramUsername = useTelegramHandle();
  const [categories, setCategories] = useState<string[]>([]);
  const [formats, setFormats] = useState<BrandFaceFormat[]>([]);
  const [languages, setLanguages] = useState<SpokenLanguage[]>([]);
  const [savedPhotos, setSavedPhotos] = useState<string[]>([]);
  const [pendingPhotos, setPendingPhotos] = useState<File[]>([]);
  const [removedPhotos, setRemovedPhotos] = useState<string[]>([]);
  const [touched, setTouched] = useState<Partial<Record<Field, boolean>>>({});
  const [edited, setEdited] = useState<Partial<Record<"name" | "telegram", boolean>>>({});
  const [serverErrors, setServerErrors] = useState<Errors>({});
  const [step, setStep] = useState<Step>(0);
  const [existing, setExisting] = useState(false);
  const [hydrated, setHydrated] = useState(Boolean(onCompleted));
  const [saving, setSaving] = useState(false);
  const [toast, setToast] = useState("");
  const [success, setSuccess] = useState(false);
  const [toastTone, setToastTone] = useState<"success" | "error" | "warning">("success");
  const [serverSummary, setServerSummary] = useState("");
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
  const [pendingImage, setPendingImage] = useState<PendingProfileImage>();
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  const unsavedChanges = useUnsavedChanges(dirty);
  const clientErrors = validate(form, languages, categories, formats, t);
  const errors = { ...clientErrors, ...Object.fromEntries(Object.entries(serverErrors).filter(([, value]) => Boolean(value))) } as Errors;
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
        gender: profile.gender === "female" || profile.gender === "male" ? profile.gender : "",
        age: profile.age ? String(profile.age) : "",
        instagram: profile.instagram ?? "",
        showreelUrl: profile.showreelUrl ?? "",
        telegram: profile.telegram ?? "",
        portfolioUrl: profile.portfolioUrl ?? "",
        collaborationPrice: profile.collaborationPrice ? formatNumericInput(String(profile.collaborationPrice)) : "",
        // "Experience" is no longer a separate field (QA Q17): an old value moves into "About" so nothing is lost.
        description: [profile.description?.trim(), profile.experience?.trim()].filter(Boolean).join("\n\n")
      });
      setCategories(profile.categories);
      setLanguages(normalizeSpokenLanguages(profile.languages));
      setFormats((profile.formats ?? []).filter((format): format is BrandFaceFormat => brandFaceFormats.includes(format)));
      setSavedPhotos(profile.photoUrls ?? []);
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

  const toggleFormat = (format: BrandFaceFormat) => {
    setDirty(true);
    clearServerError("formats");
    setTouched((current) => ({ ...current, formats: true }));
    setFormats((current) => current.includes(format) ? current.filter((item) => item !== format) : [...current, format]);
  };

  const selectGender = (gender: "female" | "male") => {
    setDirty(true);
    clearServerError("gender");
    setTouched((current) => ({ ...current, gender: true }));
    setForm((current) => ({ ...current, gender }));
  };

  const updateAge = (event: ChangeEvent<HTMLInputElement>) => {
    setDirty(true);
    clearServerError("age");
    setForm((current) => ({ ...current, age: event.target.value.replace(/\D/g, "").slice(0, 2) }));
  };

  const update = (key: Exclude<keyof BrandFaceForm, "city" | "collaborationPrice" | "gender" | "age">) => (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
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
        age: Number(form.age),
        gender: form.gender,
        languages,
        categories,
        experience: null,
        instagram: form.instagram.trim() || null,
        telegram: telegramUsername ?? undefined,
        portfolioUrl: form.portfolioUrl.trim() || null,
        collaborationPrice: form.collaborationPrice ? normalizeNumericInput(form.collaborationPrice) : null,
        description: form.description.trim() || null,
        avatarUrl,
        formats,
        showreelUrl: form.showreelUrl.trim() || null
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
      // The gallery is saved after the profile, like the avatar; a failed photo keeps the rest of the save.
      try {
        let photos = savedPhotos;
        for (const url of removedPhotos) photos = (await removeBrandFacePhoto(url)).photoUrls;
        for (const file of pendingPhotos) photos = (await addBrandFacePhoto(file)).photoUrls;
        setSavedPhotos(photos);
        setRemovedPhotos([]);
        setPendingPhotos([]);
      } catch (error) {
        mediaWarning = mediaWarning || getApiErrorMessage(error, t("error.profile_media_unavailable"));
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
      // Like the blogger and business forms: confirm, then return to the profile (QA Q16).
      if (mediaWarning) {
        haptic.warning();
        setToastTone("warning");
        setToast(mediaWarning);
      } else haptic.success();
      setSuccess(true);
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
  const hasPortfolioDetails = Boolean(reviewAvatarUrl || form.instagram.trim() || form.portfolioUrl.trim() || form.collaborationPrice || form.description.trim());
  const photoCount = savedPhotos.length + pendingPhotos.length;

  return <form noValidate onSubmit={submit}>
    <WizardLayout actionBar={<FixedActionBar key={step} backLabel={t("common.back")} continueLabel={actionLabel} disabled={step === 3 ? false : !currentStepValid} loading={saving} onBack={goBack} onPrimary={step === 3 ? undefined : continueStep} submit={step === 3} />}>
      <WizardHeader backLabel={t("common.back")} onBack={goBack} progressLabel={progressLabel} showBackButton={!isTelegram} showLanguage={Boolean(onCompleted)} step={step + 1} stepTitle={stepTitles[step]} totalSteps={4} />
      <WizardErrorSummary message={serverSummary} />
      {step === 0 && <WizardStep stepKey={stepTitles[0]}>
        <div className="wizard-fields">
          <div data-wizard-field="name"><Input className="wizard-input" error={touched.name ? errors.name : undefined} label={t("form.name")} maxLength={100} name="name" onBlur={blur("name")} onChange={update("name")} placeholder={t("form.blogger.namePlaceholder")} required value={form.name} /></div>
          <div data-wizard-field="city"><RegionSelect className="wizard-region-select" error={touched.city ? errors.city : undefined} onChange={selectCity} required value={form.city} /></div>
          <div data-wizard-field="gender"><p className="ds-field-label">{t("brandFace.gender")}<span aria-hidden="true" className="ml-1 text-brand-danger">*</span></p><div aria-label={t("brandFace.gender")} className="brand-face-chips mt-2" role="group">{(["female", "male"] as const).map((gender) => <button aria-pressed={form.gender === gender} className="brand-face-chip" key={gender} onClick={() => selectGender(gender)} type="button">{t(`brandFace.gender.${gender}`)}</button>)}</div>{touched.gender && errors.gender && <p className="mt-2 text-xs font-semibold text-brand-danger" role="alert">{errors.gender}</p>}</div>
          <div data-wizard-field="age"><Input className="wizard-input" error={touched.age ? errors.age : undefined} inputMode="numeric" label={t("brandFace.age")} maxLength={2} name="age" onBlur={blur("age")} onChange={updateAge} placeholder="24" required value={form.age} /></div>
          <div data-wizard-field="languages"><LanguageMultiSelect error={touched.languages ? errors.languages : undefined} onChange={(next) => { setDirty(true); clearServerError("languages"); setTouched((current) => ({ ...current, languages: true })); setLanguages(next); }} required value={languages} /><p className="wizard-field-helper">{t("brandFace.languagesHelper")}</p></div>
        </div>
      </WizardStep>}
      {step === 1 && <WizardStep stepKey={stepTitles[1]}>
        <div className="wizard-fields">
          <div data-wizard-field="categories"><CategoryMultiSelect error={touched.categories ? errors.categories : undefined} onChange={updateCategories} required value={categories} /></div>
          <div data-wizard-field="formats"><p className="ds-field-label">{t("brandFace.formats")}<span aria-hidden="true" className="ml-1 text-brand-danger">*</span></p><p className="wizard-field-helper">{t("brandFace.formatsHelper")}</p><div aria-label={t("brandFace.formats")} className="brand-face-chips mt-2" role="group">{brandFaceFormats.map((format) => <button aria-pressed={formats.includes(format)} className="brand-face-chip" key={format} onClick={() => toggleFormat(format)} type="button">{t(`brandFace.format.${format}`)}</button>)}</div>{touched.formats && errors.formats && <p className="mt-2 text-xs font-semibold text-brand-danger" role="alert">{errors.formats}</p>}</div>
          <TelegramHandleField />
        </div>
      </WizardStep>}
      {step === 2 && <WizardStep stepKey={stepTitles[2]}>
        <div className="wizard-fields">
          <ProfileMediaPicker className="wizard-media-picker" currentUrl={avatarUrl} disabled={saving} name={form.name || t("profile.telegramUser")} onChange={(image) => { setDirty(true); setPendingImage(image); }} pending={pendingImage} />
          <BrandFaceGallery disabled={saving} onAdd={(file) => { setDirty(true); setPendingPhotos((current) => [...current, file]); }} onRemovePending={(index) => { setDirty(true); setPendingPhotos((current) => current.filter((_, position) => position !== index)); }} onRemoveSaved={(url) => { setDirty(true); setSavedPhotos((current) => current.filter((item) => item !== url)); setRemovedPhotos((current) => [...current, url]); }} pending={pendingPhotos} saved={savedPhotos} />
          <div data-wizard-field="instagram"><Input className="wizard-input" error={touched.instagram ? errors.instagram : undefined} label={t("brandFace.instagram")} name="instagram" onBlur={blur("instagram")} onChange={update("instagram")} placeholder="@username" required value={form.instagram} /></div>
          <div data-wizard-field="showreelUrl"><Input className="wizard-input" error={touched.showreelUrl ? errors.showreelUrl : undefined} label={t("brandFace.showreel")} name="showreelUrl" onBlur={blur("showreelUrl")} onChange={update("showreelUrl")} placeholder="https://instagram.com/reel/..." type="url" value={form.showreelUrl} /><p className="wizard-field-helper">{t("brandFace.showreelHelper")}</p></div>
          <div data-wizard-field="portfolioUrl"><Input className="wizard-input" error={touched.portfolioUrl ? errors.portfolioUrl : undefined} label={t("brandFace.portfolio")} name="portfolioUrl" onBlur={blur("portfolioUrl")} onChange={update("portfolioUrl")} placeholder="https://..." type="url" value={form.portfolioUrl} /></div>
          <div data-wizard-field="collaborationPrice"><Input className="wizard-input" error={touched.collaborationPrice ? errors.collaborationPrice : undefined} inputMode="numeric" label={t("brandFace.price")} name="collaborationPrice" onBlur={blur("collaborationPrice")} onChange={updatePrice} placeholder="200 000" value={form.collaborationPrice} /></div>
          <div data-wizard-field="description"><Textarea className="wizard-input" error={touched.description ? errors.description : undefined} label={t("brandFace.description")} maxLength={2000} name="description" onBlur={blur("description")} onChange={update("description")} placeholder={t("brandFace.descriptionPlaceholder")} value={form.description} /></div>
        </div>
      </WizardStep>}
      {step === 3 && <WizardStep stepKey={stepTitles[3]}>
        <div className="wizard-review">
          {reviewAvatarUrl && <img alt={t("profileMedia.title")} className="wizard-review__logo" src={reviewAvatarUrl} />}
          <ReviewSection editAriaLabel={t("wizard.changeSection", { section: stepTitles[0] })} editLabel={t("wizard.change")} onEdit={() => setStep(0)} title={stepTitles[0]}>
            <ReviewItem label={t("form.name")} value={form.name.trim()} />
            <ReviewItem label={t("common.city")} value={cityLabel(form.city, language)} />
            <ReviewItem label={t("brandFace.gender")} value={form.gender ? t(`brandFace.gender.${form.gender}`) : undefined} />
            <ReviewItem label={t("brandFace.age")} value={form.age} />
            <ReviewItem label={t("brandFace.languages")} value={languages.map((code) => t(`language.${code}`)).join(", ")} />
          </ReviewSection>
          <ReviewSection editAriaLabel={t("wizard.changeSection", { section: stepTitles[1] })} editLabel={t("wizard.change")} onEdit={() => setStep(1)} title={stepTitles[1]}>
            <ReviewItem label={t("common.categories")} value={categories.map((category) => isOtherCategory(category) ? category.slice(otherCategoryPrefix.length) : categoryLabel(category, language)).join(", ")} />
            <ReviewItem label={t("brandFace.formats")} value={formats.map((format) => t(`brandFace.format.${format}`)).join(", ")} />
            <ReviewItem label={t("form.telegramUsername")} value={telegramUsername ?? t("form.telegramNoUsername")} />
          </ReviewSection>
          <ReviewSection editAriaLabel={t("wizard.changeSection", { section: stepTitles[2] })} editLabel={t("wizard.change")} emptyLabel={t("common.notSpecified")} isEmpty={!hasPortfolioDetails} onEdit={() => setStep(2)} title={stepTitles[2]}>
            <ReviewItem label={t("brandFace.galleryTitle")} value={photoCount ? t("brandFace.galleryCount", { count: photoCount }) : undefined} />
            <ReviewItem label={t("brandFace.instagram")} value={form.instagram.trim()} />
            <ReviewItem label={t("brandFace.showreel")} value={form.showreelUrl.trim()} />
            <ReviewItem label={t("brandFace.portfolio")} value={form.portfolioUrl.trim()} />
            <ReviewItem label={t("brandFace.price")} value={form.collaborationPrice ? formatCurrency(normalizeNumericInput(form.collaborationPrice)) : undefined} />
            <ReviewItem label={t("brandFace.description")} value={form.description.trim()} />
          </ReviewSection>
        </div>
      </WizardStep>}
    </WizardLayout>
    <Toast message={toast} tone={toastTone} />
    <UnsavedChangesDialog guard={unsavedChanges} />
    <Modal onClose={() => setSuccess(false)} open={success} title={t("form.successTitle")}><p className="text-sm leading-6 text-brand-muted">{t("brandFace.saved")}</p><Button className="mt-5 w-full" onClick={() => { window.location.hash = "/profile"; }} type="button"><Icon name="check" />{t("form.understood")}</Button></Modal>
  </form>;
}
