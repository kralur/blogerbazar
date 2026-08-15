import { useCallback, useEffect, useMemo, useState } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { createBusinessProfile, deleteProfileImage, getMyBusinessProfile, updateBusinessProfile, uploadProfileImage } from "../api/marketplace";
import { ProfileMediaPicker, type PendingProfileImage } from "../components/ProfileMediaPicker";
import { FixedActionBar, ReviewItem, ReviewSection, WizardErrorSummary, WizardHeader, WizardLayout, WizardStep } from "../components/Wizard";
import { Button, Icon, Input, Modal, Textarea, Toast } from "../components/ui";
import { RegionSelect } from "../components/RegionSelect";
import { UnsavedChangesDialog, useUnsavedChanges } from "../hooks/useUnsavedChanges";
import { useTelegramBackHandler } from "../hooks/useTelegramBackHandler";
import { notifyProfileDataChanged } from "../hooks/useProfileDataRefresh";
import { cityLabel, useI18n } from "../i18n";
import { normalizeWebsite } from "../lib/contacts";
import { formatPhoneInput } from "../lib/currency";
import { normalizeRegion } from "../lib/taxonomy";
import { useTelegram } from "../telegram/TelegramProvider";

const initial = { name: "", username: "", city: "tashkent-city", description: "", phone: "+998", email: "", website: "" };
type Field = keyof typeof initial;
type Errors = Partial<Record<Field, string>>;
type Step = 0 | 1 | 2;

const stepFields: Record<Exclude<Step, 2>, Field[]> = {
  0: ["name", "username", "city"],
  1: ["description", "phone", "email", "website"]
};

const usernamePattern = /^@[A-Za-z0-9_]{5,32}$/;
const phonePattern = /^\+998\s\d{2}\s\d{3}\s\d{2}\s\d{2}$/;

function initialBusinessForm(username?: string) {
  const normalizedUsername = username?.trim().replace(/^@+/, "");
  return normalizedUsername ? { ...initial, username: `@${normalizedUsername}` } : initial;
}

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

function validationMessages(t: (key: string) => string): Record<string, string> {
  return {
    name: t("form.validation.company"),
    username: t("form.validation.companyUsername"),
    city: t("form.validation.city"),
    description: t("form.validation.description"),
    phone: t("form.validation.phone"),
    email: t("form.validation.email"),
    websiteurl: t("form.validation.website")
  };
}

function fieldFromServerName(value: string): Field | undefined {
  const field = value.toLowerCase();
  if (field === "websiteurl") return "website";
  return ["name", "username", "city", "description", "phone", "email", "website"].includes(field) ? field as Field : undefined;
}

function stepForField(field: Field): Step {
  return stepFields[0].includes(field) ? 0 : 1;
}

export function BusinessProfileForm({ onCompleted, onBackToRole }: { onCompleted?: () => void; onBackToRole?: () => void }) {
  const { language, t } = useI18n();
  const { haptic, isTelegram, user } = useTelegram();
  const [form, setForm] = useState(() => initialBusinessForm(user?.username));
  const [touched, setTouched] = useState<Partial<Record<Field, boolean>>>({});
  const [serverErrors, setServerErrors] = useState<Errors>({});
  const [step, setStep] = useState<Step>(0);
  const [existing, setExisting] = useState(false);
  const [hydrated, setHydrated] = useState(Boolean(onCompleted));
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState(false);
  const [toast, setToast] = useState("");
  const [serverSummary, setServerSummary] = useState("");
  const [logoUrl, setLogoUrl] = useState<string | null>(null);
  const [pendingImage, setPendingImage] = useState<PendingProfileImage>();
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  const unsavedChanges = useUnsavedChanges(dirty);
  const clientErrors = useMemo(() => validate(form, t), [form, language, t]);
  const errors = useMemo(() => ({ ...clientErrors, ...serverErrors }), [clientErrors, serverErrors]);
  const currentStepValid = step === 2 || stepFields[step].every((field) => !errors[field]);
  const stepTitles = [t("wizard.businessCompanyStep"), t("wizard.businessDetailsStep"), t("wizard.businessReviewStep")];

  useEffect(() => {
    if (!user?.username) return;
    setForm((current) => current.username ? current : initialBusinessForm(user.username));
  }, [user?.username]);

  useEffect(() => {
    if (onCompleted) return;
    let active = true;
    getMyBusinessProfile().then((profile) => {
      if (!active) return;
      setForm({ name: profile.name, username: profile.username ?? "", city: normalizeRegion(profile.city) || initial.city, description: profile.description ?? "", phone: formatPhoneInput(profile.phone ?? initial.phone), email: profile.email ?? "", website: profile.websiteUrl ?? "" });
      setLogoUrl(profile.logoUrl ?? null);
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

  const update = (key: Field) => (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const value = key === "phone" ? formatPhoneInput(event.target.value) : event.target.value;
    setDirty(true);
    clearServerError(key);
    setForm((current) => ({ ...current, [key]: value }));
  };

  const selectCity = (event: React.ChangeEvent<HTMLSelectElement>) => {
    setDirty(true);
    clearServerError("city");
    setForm((current) => ({ ...current, city: event.target.value }));
  };

  const blur = (key: Field) => () => {
    setTouched((current) => ({ ...current, [key]: true }));
    if (key === "website") setForm((current) => ({ ...current, website: normalizeWebsite(current.website) }));
  };

  const markStepTouched = useCallback((currentStep: Exclude<Step, 2>) => {
    setTouched((current) => ({ ...current, ...Object.fromEntries(stepFields[currentStep].map((field) => [field, true])) }));
  }, []);

  const continueStep = useCallback(() => {
    if (step === 2) return;
    markStepTouched(step);
    if (!stepFields[step].every((field) => !errors[field])) return;
    haptic.selection();
    setStep((current) => current === 0 ? 1 : 2);
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
      setStep((current) => current === 2 ? 1 : 0);
      return;
    }
    unsavedChanges.requestLeave(leaveForm);
  }, [haptic, leaveForm, saving, step, unsavedChanges]);

  useTelegramBackHandler(goBack, Boolean(onCompleted));

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setTouched({ name: true, username: true, city: true, description: true, phone: true, email: true, website: true });
    if (Object.keys(clientErrors).length || saving) return;
    try {
      setSaving(true);
      setServerErrors({});
      setServerSummary("");
      const input = { name: form.name.trim(), username: form.username.trim(), city: form.city.trim(), logoUrl: logoUrl ?? undefined, description: form.description.trim(), phone: form.phone.trim(), email: form.email.trim() || undefined, websiteUrl: form.website.trim() || undefined };
      if (existing) await updateBusinessProfile(input); else { await createBusinessProfile(input); setExisting(true); }
      let mediaWarning = "";
      try {
        if (pendingImage instanceof File) {
          const media = await uploadProfileImage("business", pendingImage);
          setLogoUrl(media.url);
        } else if (pendingImage === null && logoUrl) {
          await deleteProfileImage("business");
          setLogoUrl(null);
        }
      } catch (error) {
        mediaWarning = getApiErrorMessage(error, t("error.profile_media_unavailable"));
      }
      setPendingImage(undefined);
      setDirty(false);
      notifyProfileDataChanged();
      if (mediaWarning) {
        haptic.warning();
        if (onCompleted) window.sessionStorage.setItem("bloggerbazar.onboarding.media-warning", mediaWarning);
        else setToast(mediaWarning);
      } else haptic.success();
      if (onCompleted) onCompleted();
      else setSuccess(true);
    } catch (error) {
      const message = getApiErrorMessage(error, t("form.submitFailed"), { conflictMessage: t("form.businessProfileConflict"), validationMessages: validationMessages(t) });
      if (error instanceof ApiError && error.code === "validation_failed") {
        const fields = error.validationFields.map(fieldFromServerName).filter((field): field is Field => Boolean(field));
        if (fields.length) {
          setServerErrors(Object.fromEntries(fields.map((field) => [field, validationMessages(t)[field === "website" ? "websiteurl" : field]])));
          setTouched((current) => ({ ...current, ...Object.fromEntries(fields.map((field) => [field, true])) }));
          setStep(stepForField(fields[0]));
        }
        setServerSummary(message);
      } else setToast(message);
    } finally {
      setSaving(false);
    }
  };

  if (!hydrated) return <div className={`screen ${onCompleted ? "screen--without-nav" : "screen--with-nav"}`}><div aria-busy="true" className="wizard-loading" /></div>;

  const reviewLogoUrl = pendingImage === null ? null : previewUrl ?? logoUrl;
  const progressLabel = t("wizard.stepOf", { current: step + 1, total: 3 });
  const submitLabel = existing ? t("wizard.saveChanges") : t("wizard.createProfile");
  const actionLabel = step === 2 ? (saving ? t("form.publishing") : submitLabel) : t("wizard.continue");

  return <form noValidate onSubmit={submit}>
    <WizardLayout actionBar={<FixedActionBar key={step} backLabel={t("common.back")} continueLabel={actionLabel} disabled={step === 2 ? false : !currentStepValid} loading={saving} onBack={goBack} onPrimary={step === 2 ? undefined : continueStep} submit={step === 2} />}>
      <WizardHeader backLabel={t("common.back")} onBack={goBack} progressLabel={progressLabel} showBackButton={!isTelegram} step={step + 1} stepTitle={stepTitles[step]} totalSteps={3} />
      <WizardErrorSummary message={serverSummary} />
      {step === 0 && <WizardStep stepKey={stepTitles[0]}>
        <div className="wizard-fields">
          <Input className="wizard-input" error={touched.name ? errors.name : undefined} label={t("form.companyName")} onBlur={blur("name")} onChange={update("name")} placeholder="Lumi Beauty" required value={form.name} />
          <div><Input className="wizard-input" error={touched.username ? errors.username : undefined} label={t("form.telegramUsername")} onBlur={blur("username")} onChange={update("username")} placeholder="@username" required value={form.username} /><p className="wizard-field-helper">{t("form.usernameBusinessHelper")}</p></div>
          <RegionSelect className="wizard-region-select" error={touched.city ? errors.city : undefined} onChange={selectCity} required value={form.city} />
        </div>
      </WizardStep>}
      {step === 1 && <WizardStep stepKey={stepTitles[1]}>
        <div className="wizard-fields">
          <ProfileMediaPicker className="wizard-media-picker" currentUrl={logoUrl} disabled={saving} name={form.name || t("profile.telegramUser")} onChange={(image) => { setDirty(true); setPendingImage(image); }} pending={pendingImage} />
          <div><Input className="wizard-input" error={touched.website ? errors.website : undefined} label={t("form.websiteOptional")} onBlur={blur("website")} onChange={update("website")} placeholder="https://company.uz" type="url" value={form.website} /><p className="wizard-field-helper">{t("form.websiteHelper")}</p></div>
          <Textarea className="wizard-input" error={touched.description ? errors.description : undefined} label={t("form.aboutCompany")} maxLength={1000} onBlur={blur("description")} onChange={update("description")} placeholder={t("form.companyDescriptionPlaceholder")} required value={form.description} />
          <Input className="wizard-input" error={touched.phone ? errors.phone : undefined} inputMode="tel" label={t("common.phone")} onBlur={blur("phone")} onChange={update("phone")} placeholder="+998 90 123 45 67" required value={form.phone} />
          <Input className="wizard-input" error={touched.email ? errors.email : undefined} label={t("form.emailOptional")} onBlur={blur("email")} onChange={update("email")} placeholder="brand@example.uz" type="email" value={form.email} />
        </div>
      </WizardStep>}
      {step === 2 && <WizardStep stepKey={stepTitles[2]}>
        <div className="wizard-review">
          {reviewLogoUrl && <img alt={t("profileMedia.title")} className="wizard-review__logo" src={reviewLogoUrl} />}
          <ReviewSection editAriaLabel={t("wizard.editSection", { section: stepTitles[0] })} editLabel={t("common.edit")} onEdit={() => setStep(0)} title={stepTitles[0]}>
            <ReviewItem label={t("form.companyName")} value={form.name.trim()} />
            <ReviewItem label={t("form.telegramUsername")} value={form.username.trim()} />
            <ReviewItem label={t("common.city")} value={cityLabel(form.city, language)} />
          </ReviewSection>
          <ReviewSection editAriaLabel={t("wizard.editSection", { section: stepTitles[1] })} editLabel={t("common.edit")} onEdit={() => setStep(1)} title={stepTitles[1]}>
            <ReviewItem label={t("form.websiteOptional")} value={form.website.trim()} />
            <ReviewItem label={t("form.aboutCompany")} value={form.description.trim()} />
            <ReviewItem label={t("common.phone")} value={form.phone} />
            <ReviewItem label={t("form.emailOptional")} value={form.email.trim()} />
          </ReviewSection>
        </div>
      </WizardStep>}
    </WizardLayout>
    <Toast message={toast} tone="error" />
    <Modal onClose={() => setSuccess(false)} open={success} title={t("form.successTitle")}><p className="text-sm leading-6 text-brand-muted">{t("form.successDescription")}</p><Button className="mt-5 w-full" onClick={() => { window.location.hash = "/profile"; }} type="button"><Icon name="check" />{t("form.understood")}</Button></Modal>
    <UnsavedChangesDialog guard={unsavedChanges} />
  </form>;
}
