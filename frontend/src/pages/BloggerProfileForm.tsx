import { useCallback, useEffect, useState, type ChangeEvent, type FormEvent } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { createBloggerProfile, deleteProfileImage, getMyBloggerProfile, updateBloggerProfile, uploadProfileImage } from "../api/marketplace";
import { CategoryMultiSelect } from "../components/CategoryMultiSelect";
import { ProfileMediaPicker, type PendingProfileImage } from "../components/ProfileMediaPicker";
import { RegionSelect } from "../components/RegionSelect";
import { FixedActionBar, ReviewItem, ReviewSection, WizardErrorSummary, WizardHeader, WizardLayout, WizardStep } from "../components/Wizard";
import { Button, Icon, Input, Modal, Textarea, Toast } from "../components/ui";
import { UnsavedChangesDialog, useUnsavedChanges } from "../hooks/useUnsavedChanges";
import { notifyProfileDataChanged } from "../hooks/useProfileDataRefresh";
import { useTelegramBackHandler } from "../hooks/useTelegramBackHandler";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { normalizeWebsite, safeExternalUrl, socialHandle, socialUrl } from "../lib/contacts";
import { formatCurrency, formatNumericInput, formatNumber, formatPhoneInput, normalizeNumericInput } from "../lib/currency";
import { isOtherCategory, normalizeRegion, otherCategoryPrefix } from "../lib/taxonomy";
import { useTelegram } from "../telegram/TelegramProvider";

const initial = { name: "", lastName: "", username: "", city: "tashkent-city", phone: "+998", email: "", totalFollowers: "10 000", averageReach: "25 000", engagementRate: "5.5", storiesPrice: "250 000", reelsPrice: "500 000", postPrice: "350 000", integrationPrice: "900 000", bio: "", portfolioUrl: "", instagram: "", tiktok: "", youtube: "" };
type BloggerForm = typeof initial;
type Field = keyof BloggerForm;
type Errors = Partial<Record<Field | "categories", string>>;
type Step = 0 | 1 | 2 | 3 | 4;

const stepFields: Record<Exclude<Step, 4>, Array<Field | "categories">> = {
  0: ["name", "lastName", "username", "city", "phone", "email"],
  1: ["categories", "totalFollowers", "averageReach", "engagementRate"],
  2: ["storiesPrice", "reelsPrice", "postPrice", "integrationPrice"],
  3: ["bio", "portfolioUrl", "instagram", "tiktok", "youtube"]
};

const usernamePattern = /^@[A-Za-z0-9_]{5,32}$/;
const phonePattern = /^\+998\s\d{2}\s\d{3}\s\d{2}\s\d{2}$/;
const numericFields = ["totalFollowers", "averageReach", "storiesPrice", "reelsPrice", "postPrice", "integrationPrice"] as const;

function initialBloggerForm(firstName?: string, username?: string): BloggerForm {
  const normalizedUsername = username?.trim().replace(/^@+/, "");
  return { ...initial, name: firstName?.trim() ?? "", username: normalizedUsername ? `@${normalizedUsername}` : "" };
}

function validate(form: BloggerForm, categories: string[], t: (key: string) => string): Errors {
  const errors: Errors = {};
  if (!form.name.trim() || form.name.trim().length > 100) errors.name = t("form.validation.name");
  if (form.lastName.trim().length > 100) errors.lastName = t("form.validation.name");
  if (!usernamePattern.test(form.username.trim())) errors.username = t("form.validation.username");
  if (!form.city.trim()) errors.city = t("form.validation.city");
  if (!phonePattern.test(form.phone)) errors.phone = t("form.validation.phone");
  if (form.email && (form.email.length > 254 || !/^\S+@\S+\.\S+$/.test(form.email))) errors.email = t("form.validation.email");
  if (!categories.length || categories.length > 5) errors.categories = t("form.validation.categories");
  if (normalizeNumericInput(form.totalFollowers) <= 0) errors.totalFollowers = t("form.validation.followers");
  if (normalizeNumericInput(form.averageReach) <= 0) errors.averageReach = t("form.validation.reach");
  const engagementRate = Number(form.engagementRate);
  if (!Number.isFinite(engagementRate) || engagementRate < 0.1 || engagementRate > 100) errors.engagementRate = t("form.validation.er");
  if (normalizeNumericInput(form.storiesPrice) <= 0) errors.storiesPrice = t("form.validation.stories");
  if (normalizeNumericInput(form.reelsPrice) <= 0) errors.reelsPrice = t("form.validation.reels");
  if (form.bio.length > 500) errors.bio = t("form.validation.bio");
  if (form.instagram && !/^@?[A-Za-z0-9._]{1,30}$/.test(form.instagram.trim())) errors.instagram = t("form.validation.socialUsername");
  if (form.tiktok && !/^@?[A-Za-z0-9._]{1,30}$/.test(form.tiktok.trim())) errors.tiktok = t("form.validation.socialUsername");
  if (form.youtube && !safeExternalUrl(form.youtube)) errors.youtube = t("form.validation.website");
  if (form.portfolioUrl && !safeExternalUrl(form.portfolioUrl)) errors.portfolioUrl = t("form.validation.website");
  return errors;
}

function validationMessages(t: (key: string) => string): Record<string, string> {
  return {
    name: t("form.validation.name"), lastName: t("form.validation.name"), username: t("form.validation.username"), city: t("form.validation.city"), phone: t("form.validation.phone"), email: t("form.validation.email"), categories: t("form.validation.categories"), totalFollowers: t("form.validation.followers"), averageReach: t("form.validation.reach"), engagementRate: t("form.validation.er"), storiesPrice: t("form.validation.stories"), reelsPrice: t("form.validation.reels"), postPrice: t("form.validation.stories"), integrationPrice: t("form.validation.stories"), bio: t("form.validation.bio"), portfolioUrl: t("form.validation.website"), instagram: t("form.validation.socialUsername"), tiktok: t("form.validation.socialUsername"), youtube: t("form.validation.website")
  };
}

function fieldFromServerName(value: string): Field | "categories" | undefined {
  const field = value.toLowerCase().replace(/\[.*$/, "");
  const mapping: Record<string, Field | "categories"> = {
    lastname: "lastName", totalfollowers: "totalFollowers", averagereach: "averageReach", engagementrate: "engagementRate", storiesprice: "storiesPrice", reelsprice: "reelsPrice", postprice: "postPrice", integrationprice: "integrationPrice", portfolioitems: "portfolioUrl", platforms: "instagram", avatar: "bio", media: "bio"
  };
  if (mapping[field]) return mapping[field];
  return ["name", "username", "city", "phone", "email", "categories", "bio", "portfoliourl", "instagram", "tiktok", "youtube"].includes(field) ? (field === "portfoliourl" ? "portfolioUrl" : field as Field | "categories") : undefined;
}

function stepForField(field: Field | "categories"): Step {
  if (stepFields[0].includes(field)) return 0;
  if (stepFields[1].includes(field)) return 1;
  if (stepFields[2].includes(field)) return 2;
  return 3;
}

function reviewCategory(category: string, language: "ru" | "uz") {
  return isOtherCategory(category) ? category.slice(otherCategoryPrefix.length) : categoryLabel(category, language);
}

export function BloggerProfileForm({ onCompleted, onBackToRole }: { onCompleted?: () => void; onBackToRole?: () => void }) {
  const { t, language } = useI18n();
  const { haptic, isTelegram, user } = useTelegram();
  const [form, setForm] = useState(() => initialBloggerForm(user?.first_name, user?.username));
  const [selectedCategories, setSelectedCategories] = useState<string[]>(["lifestyle"]);
  const [barterEnabled, setBarterEnabled] = useState(true);
  const [touched, setTouched] = useState<Partial<Record<Field | "categories", boolean>>>({});
  const [serverErrors, setServerErrors] = useState<Errors>({});
  const [edited, setEdited] = useState<Partial<Record<"name" | "username", boolean>>>({});
  const [step, setStep] = useState<Step>(0);
  const [existing, setExisting] = useState(false);
  const [hydrated, setHydrated] = useState(Boolean(onCompleted));
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState(false);
  const [toast, setToast] = useState("");
  const [serverSummary, setServerSummary] = useState("");
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
  const [pendingImage, setPendingImage] = useState<PendingProfileImage>();
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  const unsavedChanges = useUnsavedChanges(dirty);
  const clientErrors = validate(form, selectedCategories, t);
  const errors = { ...clientErrors, ...Object.fromEntries(Object.entries(serverErrors).filter(([, value]) => Boolean(value))) } as Errors;
  const currentStepValid = step === 4 || stepFields[step].every((field) => !errors[field]);
  const stepTitles = [t("wizard.bloggerBasicStep"), t("wizard.bloggerAudienceStep"), t("wizard.bloggerPricesStep"), t("wizard.bloggerPortfolioStep"), t("wizard.bloggerReviewStep")];

  useEffect(() => {
    setForm((current) => ({
      ...current,
      name: current.name || edited.name ? current.name : user?.first_name?.trim() ?? current.name,
      username: current.username || edited.username ? current.username : user?.username ? `@${user.username.replace(/^@+/, "")}` : current.username
    }));
  }, [edited.name, edited.username, user?.first_name, user?.username]);

  useEffect(() => {
    if (onCompleted) return;
    let active = true;
    getMyBloggerProfile().then((profile) => {
      if (!active) return;
      const platform = (type: string) => socialHandle(profile.platforms.find((item) => item.type.toLowerCase() === type)?.url ?? "");
      setForm({ name: profile.name, lastName: profile.lastName ?? "", username: profile.username ?? "", city: normalizeRegion(profile.city) || initial.city, phone: formatPhoneInput(profile.phone ?? initial.phone), email: profile.email ?? "", totalFollowers: formatNumericInput(String(profile.totalFollowers)), averageReach: formatNumericInput(String(profile.averageReach ?? "")), engagementRate: String(profile.engagementRate ?? ""), storiesPrice: formatNumericInput(String(profile.storiesPrice ?? "")), reelsPrice: formatNumericInput(String(profile.reelsPrice ?? "")), postPrice: formatNumericInput(String(profile.postPrice ?? "")), integrationPrice: formatNumericInput(String(profile.integrationPrice ?? "")), bio: profile.bio ?? "", portfolioUrl: profile.portfolioItems[0]?.url ?? "", instagram: platform("instagram"), tiktok: platform("tiktok"), youtube: profile.platforms.find((item) => item.type.toLowerCase() === "youtube")?.url ?? "" });
      setSelectedCategories(profile.categories);
      setBarterEnabled(profile.barterEnabled);
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

  const clearServerError = useCallback((field: Field | "categories") => {
    setServerErrors((current) => ({ ...current, [field]: undefined }));
    setServerSummary("");
  }, []);

  const update = (key: Field) => (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const value = key === "phone" ? formatPhoneInput(event.target.value) : event.target.value;
    setDirty(true);
    if (key === "name" || key === "username") setEdited((current) => ({ ...current, [key]: true }));
    clearServerError(key);
    setForm((current) => ({ ...current, [key]: value }));
  };

  const numeric = (key: typeof numericFields[number]) => (event: ChangeEvent<HTMLInputElement>) => {
    setDirty(true);
    clearServerError(key);
    setForm((current) => ({ ...current, [key]: formatNumericInput(event.target.value) }));
  };

  const selectCity = (event: ChangeEvent<HTMLSelectElement>) => {
    setDirty(true);
    clearServerError("city");
    setForm((current) => ({ ...current, city: event.target.value }));
  };

  const updateCategories = (categories: string[]) => {
    setDirty(true);
    clearServerError("categories");
    setSelectedCategories(categories);
  };

  const blur = (field: Field | "categories") => () => {
    setTouched((current) => ({ ...current, [field]: true }));
    if (field === "portfolioUrl" || field === "youtube") setForm((current) => ({ ...current, [field]: normalizeWebsite(current[field]) }));
  };

  const markStepTouched = useCallback((currentStep: Exclude<Step, 4>) => {
    setTouched((current) => ({ ...current, ...Object.fromEntries(stepFields[currentStep].map((field) => [field, true])) }));
  }, []);

  const focusField = useCallback((field: Field | "categories") => {
    window.requestAnimationFrame(() => {
      const container = document.querySelector<HTMLElement>(`[data-wizard-field="${field}"]`);
      container?.querySelector<HTMLElement>("input, textarea, select, button")?.focus();
    });
  }, []);

  const continueStep = useCallback(() => {
    if (step === 4) return;
    markStepTouched(step);
    const invalidField = stepFields[step].find((field) => errors[field]);
    if (invalidField) {
      haptic.error();
      focusField(invalidField);
      return;
    }
    haptic.selection();
    setStep((current) => Math.min(current + 1, 4) as Step);
  }, [errors, focusField, haptic, markStepTouched, step]);

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
      setStep((current) => Math.max(0, current - 1) as Step);
      return;
    }
    unsavedChanges.requestLeave(leaveForm);
  }, [haptic, leaveForm, saving, step, unsavedChanges]);

  useTelegramBackHandler(goBack, Boolean(onCompleted));

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const allFields = Object.values(stepFields).flat();
    setTouched(Object.fromEntries(allFields.map((field) => [field, true])));
    const invalidField = allFields.find((field) => clientErrors[field]);
    if (invalidField) {
      setStep(stepForField(invalidField));
      focusField(invalidField);
      haptic.error();
      return;
    }
    if (saving) return;
    try {
      setSaving(true);
      setServerErrors({});
      setServerSummary("");
      const portfolioUrl = normalizeWebsite(form.portfolioUrl);
      const youtubeUrl = normalizeWebsite(form.youtube);
      const input = { name: form.name.trim(), lastName: form.lastName.trim() || undefined, username: form.username.trim(), city: form.city.trim(), categories: selectedCategories, bio: form.bio.trim() || undefined, avatarUrl, phone: form.phone.trim(), email: form.email.trim() || undefined, totalFollowers: normalizeNumericInput(form.totalFollowers), averageReach: normalizeNumericInput(form.averageReach), engagementRate: Number(form.engagementRate), storiesPrice: normalizeNumericInput(form.storiesPrice), reelsPrice: normalizeNumericInput(form.reelsPrice), postPrice: normalizeNumericInput(form.postPrice) || undefined, integrationPrice: normalizeNumericInput(form.integrationPrice) || undefined, barterEnabled, portfolioItems: portfolioUrl ? [{ title: t("form.blogger.portfolioTitle"), type: "IMAGE" as const, url: portfolioUrl }] : [], platforms: ([form.instagram.trim() ? { type: "instagram", url: socialUrl("instagram", form.instagram) } : null, form.tiktok.trim() ? { type: "tiktok", url: socialUrl("tiktok", form.tiktok) } : null, youtubeUrl ? { type: "youtube", url: youtubeUrl } : null].filter(Boolean) as Array<{ type: string; url: string }>) };
      if (existing) await updateBloggerProfile(input); else { await createBloggerProfile(input); setExisting(true); }
      let mediaWarning = "";
      try {
        if (pendingImage instanceof File) {
          const media = await uploadProfileImage("blogger", pendingImage);
          setAvatarUrl(media.url);
        } else if (pendingImage === null && avatarUrl) {
          await deleteProfileImage("blogger");
          setAvatarUrl(null);
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
      const message = getApiErrorMessage(error, t("form.submitFailed"), { conflictMessage: t("form.bloggerProfileConflict"), validationMessages: validationMessages(t) });
      if (error instanceof ApiError && error.code === "validation_failed") {
        const fields = error.validationFields.map(fieldFromServerName).filter((field): field is Field | "categories" => Boolean(field));
        if (fields.length) {
          setServerErrors(Object.fromEntries(fields.map((field) => [field, validationMessages(t)[field]])));
          setTouched((current) => ({ ...current, ...Object.fromEntries(fields.map((field) => [field, true])) }));
          setStep(stepForField(fields[0]));
          focusField(fields[0]);
        }
        setServerSummary(message);
      } else setToast(message);
    } finally {
      setSaving(false);
    }
  };

  if (!hydrated) return <div className="screen screen--without-nav"><div aria-busy="true" className="wizard-loading" /></div>;

  const reviewAvatarUrl = pendingImage === null ? null : previewUrl ?? avatarUrl;
  const hasPortfolioDetails = Boolean(reviewAvatarUrl || form.bio.trim() || form.portfolioUrl.trim() || form.instagram.trim() || form.tiktok.trim() || form.youtube.trim());
  const progressLabel = t("wizard.stepOf", { current: step + 1, total: 5 });
  const submitLabel = existing ? t("wizard.saveChanges") : t("wizard.createProfile");
  const actionLabel = step === 4 ? (saving ? t("form.publishing") : submitLabel) : t("wizard.continue");

  return <form noValidate onSubmit={submit}>
    <WizardLayout actionBar={<FixedActionBar key={step} backLabel={t("common.back")} continueLabel={actionLabel} disabled={step === 4 ? false : !currentStepValid} loading={saving} onBack={goBack} onPrimary={step === 4 ? undefined : continueStep} submit={step === 4} />}>
      <WizardHeader backLabel={t("common.back")} onBack={goBack} progressLabel={progressLabel} showBackButton={!isTelegram} step={step + 1} stepTitle={stepTitles[step]} totalSteps={5} />
      <WizardErrorSummary message={serverSummary} />
      {step === 0 && <WizardStep stepKey={stepTitles[0]}><div className="wizard-fields">
        <div data-wizard-field="name"><Input className="wizard-input" error={touched.name ? errors.name : undefined} label={t("form.name")} maxLength={100} onBlur={blur("name")} onChange={update("name")} placeholder={t("form.blogger.namePlaceholder")} required value={form.name} /></div>
        <div data-wizard-field="lastName"><Input className="wizard-input" error={touched.lastName ? errors.lastName : undefined} label={t("form.lastName")} maxLength={100} onBlur={blur("lastName")} onChange={update("lastName")} placeholder={t("form.blogger.lastNamePlaceholder")} value={form.lastName} /></div>
        <div data-wizard-field="username"><Input className="wizard-input" error={touched.username ? errors.username : undefined} label={t("form.telegramUsername")} onBlur={blur("username")} onChange={update("username")} placeholder="@username" required value={form.username} /><p className="wizard-field-helper">{t("form.usernameHelper")}</p></div>
        <div data-wizard-field="city"><RegionSelect className="wizard-region-select" error={touched.city ? errors.city : undefined} onChange={selectCity} required value={form.city} /></div>
        <div data-wizard-field="phone"><Input className="wizard-input" error={touched.phone ? errors.phone : undefined} inputMode="tel" label={t("common.phone")} onBlur={blur("phone")} onChange={update("phone")} placeholder="+998 90 123 45 67" required value={form.phone} /></div>
        <div data-wizard-field="email"><Input className="wizard-input" error={touched.email ? errors.email : undefined} label={t("form.emailOptional")} maxLength={254} onBlur={blur("email")} onChange={update("email")} placeholder="you@email.com" type="email" value={form.email} /></div>
      </div></WizardStep>}
      {step === 1 && <WizardStep stepKey={stepTitles[1]}><div className="wizard-fields">
        <div data-wizard-field="categories"><CategoryMultiSelect error={touched.categories ? errors.categories : undefined} onChange={updateCategories} required value={selectedCategories} /></div>
        <div data-wizard-field="totalFollowers"><Input className="wizard-input" error={touched.totalFollowers ? errors.totalFollowers : undefined} inputMode="numeric" label={t("common.followers")} onBlur={blur("totalFollowers")} onChange={numeric("totalFollowers")} placeholder="25 000" required value={form.totalFollowers} /></div>
        <div data-wizard-field="averageReach"><Input className="wizard-input" error={touched.averageReach ? errors.averageReach : undefined} inputMode="numeric" label={t("common.reach")} onBlur={blur("averageReach")} onChange={numeric("averageReach")} placeholder="15 000" required value={form.averageReach} /></div>
        <div data-wizard-field="engagementRate"><Input className="wizard-input" error={touched.engagementRate ? errors.engagementRate : undefined} inputMode="decimal" label={t("search.er")} max="100" min="0.1" onBlur={blur("engagementRate")} onChange={update("engagementRate")} placeholder="5.5" required step="0.1" type="number" value={form.engagementRate} /></div>
      </div></WizardStep>}
      {step === 2 && <WizardStep stepKey={stepTitles[2]}><div className="wizard-fields">
        <div data-wizard-field="storiesPrice"><Input className="wizard-input" error={touched.storiesPrice ? errors.storiesPrice : undefined} inputMode="numeric" label={t("card.stories")} onBlur={blur("storiesPrice")} onChange={numeric("storiesPrice")} placeholder="200 000" required value={form.storiesPrice} /></div>
        <div data-wizard-field="reelsPrice"><Input className="wizard-input" error={touched.reelsPrice ? errors.reelsPrice : undefined} inputMode="numeric" label={t("card.reels")} onBlur={blur("reelsPrice")} onChange={numeric("reelsPrice")} placeholder="500 000" required value={form.reelsPrice} /></div>
        <div data-wizard-field="postPrice"><Input className="wizard-input" inputMode="numeric" label={t("card.post")} onBlur={blur("postPrice")} onChange={numeric("postPrice")} placeholder="350 000" value={form.postPrice} /></div>
        <div data-wizard-field="integrationPrice"><Input className="wizard-input" inputMode="numeric" label={t("card.integration")} onBlur={blur("integrationPrice")} onChange={numeric("integrationPrice")} placeholder="900 000" value={form.integrationPrice} /></div>
        <button aria-checked={barterEnabled} aria-label={t("form.barterTitle")} className="wizard-toggle" onClick={() => { setDirty(true); setBarterEnabled((value) => !value); }} role="switch" type="button"><span><strong>{t("form.barterTitle")}</strong><small>{t("form.barterSubtitle")}</small></span><span aria-hidden="true" className="wizard-toggle__control"><span /></span></button>
      </div></WizardStep>}
      {step === 3 && <WizardStep stepKey={stepTitles[3]}><div className="wizard-fields">
        <ProfileMediaPicker className="wizard-media-picker" currentUrl={avatarUrl} disabled={saving} name={form.name || t("profile.telegramUser")} onChange={(image) => { setDirty(true); setPendingImage(image); }} pending={pendingImage} />
        <div data-wizard-field="bio"><Textarea className="wizard-input" error={touched.bio ? errors.bio : undefined} label={t("form.aboutMe")} maxLength={500} onBlur={blur("bio")} onChange={update("bio")} placeholder={t("form.aboutMePlaceholder")} value={form.bio} /></div>
        <div data-wizard-field="portfolioUrl"><Input className="wizard-input" error={touched.portfolioUrl ? errors.portfolioUrl : undefined} label={t("form.portfolioUrl")} onBlur={blur("portfolioUrl")} onChange={update("portfolioUrl")} placeholder="https://..." type="url" value={form.portfolioUrl} /></div>
        <div data-wizard-field="instagram"><Input className="wizard-input" error={touched.instagram ? errors.instagram : undefined} label={t("form.instagram")} onBlur={blur("instagram")} onChange={update("instagram")} placeholder="@username" value={form.instagram} /></div>
        <div data-wizard-field="tiktok"><Input className="wizard-input" error={touched.tiktok ? errors.tiktok : undefined} label={t("form.tiktok")} onBlur={blur("tiktok")} onChange={update("tiktok")} placeholder="@username" value={form.tiktok} /></div>
        <div data-wizard-field="youtube"><Input className="wizard-input" error={touched.youtube ? errors.youtube : undefined} label={t("form.youtube")} onBlur={blur("youtube")} onChange={update("youtube")} placeholder="https://youtube.com/@channel" type="url" value={form.youtube} /></div>
      </div></WizardStep>}
      {step === 4 && <WizardStep stepKey={stepTitles[4]}><div className="wizard-review">
        <ReviewSection editAriaLabel={t("wizard.changeSection", { section: stepTitles[0] })} editLabel={t("wizard.change")} onEdit={() => setStep(0)} title={stepTitles[0]}>
          <ReviewItem label={t("form.name")} value={[form.name.trim(), form.lastName.trim()].filter(Boolean).join(" ")} /><ReviewItem label={t("form.telegramUsername")} value={form.username.trim()} /><ReviewItem label={t("common.city")} value={cityLabel(form.city, language)} /><ReviewItem label={t("common.phone")} value={form.phone} /><ReviewItem label={t("form.emailOptional")} value={form.email.trim()} />
        </ReviewSection>
        <ReviewSection editAriaLabel={t("wizard.changeSection", { section: stepTitles[1] })} editLabel={t("wizard.change")} onEdit={() => setStep(1)} title={stepTitles[1]}>
          <ReviewItem label={t("common.categories")} value={selectedCategories.map((category) => reviewCategory(category, language)).join(", ")} /><ReviewItem label={t("common.followers")} value={formatNumber(normalizeNumericInput(form.totalFollowers))} /><ReviewItem label={t("common.reach")} value={formatNumber(normalizeNumericInput(form.averageReach))} /><ReviewItem label={t("search.er")} value={`${form.engagementRate}%`} />
        </ReviewSection>
        <ReviewSection editAriaLabel={t("wizard.changeSection", { section: stepTitles[2] })} editLabel={t("wizard.change")} onEdit={() => setStep(2)} title={stepTitles[2]}>
          <ReviewItem label={t("card.stories")} value={formatCurrency(normalizeNumericInput(form.storiesPrice))} /><ReviewItem label={t("card.reels")} value={formatCurrency(normalizeNumericInput(form.reelsPrice))} /><ReviewItem label={t("card.post")} value={form.postPrice ? formatCurrency(normalizeNumericInput(form.postPrice)) : undefined} /><ReviewItem label={t("card.integration")} value={form.integrationPrice ? formatCurrency(normalizeNumericInput(form.integrationPrice)) : undefined} /><ReviewItem label={t("form.barterTitle")} value={barterEnabled ? t("common.yes") : t("common.no")} />
        </ReviewSection>
        <ReviewSection editAriaLabel={t("wizard.changeSection", { section: stepTitles[3] })} editLabel={t("wizard.change")} emptyLabel={t("common.notSpecified")} isEmpty={!hasPortfolioDetails} onEdit={() => setStep(3)} title={stepTitles[3]}>
          {reviewAvatarUrl && <img alt={t("profileMedia.title")} className="wizard-review__logo" src={reviewAvatarUrl} />}<ReviewItem label={t("form.aboutMe")} value={form.bio.trim()} /><ReviewItem label={t("form.portfolioUrl")} value={form.portfolioUrl.trim()} /><ReviewItem label={t("form.instagram")} value={form.instagram.trim()} /><ReviewItem label={t("form.tiktok")} value={form.tiktok.trim()} /><ReviewItem label={t("form.youtube")} value={form.youtube.trim()} />
        </ReviewSection>
      </div></WizardStep>}
    </WizardLayout>
    <Toast message={toast} tone="error" />
    <Modal onClose={() => setSuccess(false)} open={success} title={t("form.successTitle")}><p className="text-sm leading-6 text-brand-muted">{t("form.successDescription")}</p><Button className="mt-5 w-full" onClick={() => { window.location.hash = "/profile"; }} type="button"><Icon name="check" />{t("form.understood")}</Button></Modal>
    <UnsavedChangesDialog guard={unsavedChanges} />
  </form>;
}
