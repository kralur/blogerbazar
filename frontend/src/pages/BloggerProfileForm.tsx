import { useCallback, useEffect, useState, type ChangeEvent, type FormEvent } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { createBloggerProfile, deleteProfileImage, getMyBloggerProfile, updateBloggerProfile, uploadProfileImage } from "../api/marketplace";
import { CategoryMultiSelect } from "../components/CategoryMultiSelect";
import { ProfileMediaPicker, type PendingProfileImage } from "../components/ProfileMediaPicker";
import { RegionSelect } from "../components/RegionSelect";
import { TelegramHandleField, useTelegramHandle } from "../components/TelegramHandleField";
import { useVerifiedPhone, VerifiedPhoneField } from "../components/VerifiedPhoneField";
import { FixedActionBar, ReviewItem, ReviewSection, WizardErrorSummary, WizardHeader, WizardLayout, WizardStep } from "../components/Wizard";
import { Button, Icon, Input, Modal, Textarea, Toast } from "../components/ui";
import { UnsavedChangesDialog, useUnsavedChanges } from "../hooks/useUnsavedChanges";
import { notifyProfileDataChanged } from "../hooks/useProfileDataRefresh";
import { useTelegramBackHandler } from "../hooks/useTelegramBackHandler";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { normalizeWebsite, safeExternalUrl, socialHandle, socialUrl } from "../lib/contacts";
import { maxFollowers, formatCurrency, formatDecimalInput, formatNumericInput, formatNumber, formatPercentage, formatPhoneInput, normalizeDecimalInput, normalizeNumericInput } from "../lib/currency";
import { isOtherCategory, normalizeRegion, otherCategoryPrefix } from "../lib/taxonomy";
import { platformLabel } from "../lib/platforms";
import { useTelegram } from "../telegram/TelegramProvider";

// Audience is entered per platform; the profile totals the catalog filters on are derived from it.
const platformKinds = ["instagram", "telegram", "tiktok", "youtube"] as const;
type PlatformKind = typeof platformKinds[number];
const emptyPlatformStats = { instagramFollowers: "", instagramReach: "", instagramEr: "", telegramFollowers: "", telegramReach: "", telegramEr: "", tiktokFollowers: "", tiktokReach: "", tiktokEr: "", youtubeFollowers: "", youtubeReach: "", youtubeEr: "" };
const initial = { name: "", lastName: "", username: "", city: "tashkent-city", phone: "+998", email: "", storiesPrice: "", reelsPrice: "", postPrice: "", integrationPrice: "", bio: "", portfolioUrl: "", instagram: "", telegram: "", tiktok: "", youtube: "", ...emptyPlatformStats };
type BloggerForm = typeof initial;
type Field = keyof BloggerForm;
type ErrorKey = Field | "categories" | "platforms";
type Errors = Partial<Record<ErrorKey, string>>;
type Step = 0 | 1 | 2 | 3 | 4;

const followersField = (kind: PlatformKind) => `${kind}Followers` as const;
const reachField = (kind: PlatformKind) => `${kind}Reach` as const;
const erField = (kind: PlatformKind) => `${kind}Er` as const;

const stepFields: Record<Exclude<Step, 4>, ErrorKey[]> = {
  0: ["name", "lastName", "city", "email"],
  1: ["categories", "platforms", ...platformKinds.flatMap((kind): ErrorKey[] => [kind, followersField(kind), reachField(kind), erField(kind)])],
  2: ["storiesPrice", "reelsPrice", "postPrice", "integrationPrice"],
  3: ["bio", "portfolioUrl"]
};

const handlePatterns: Record<Exclude<PlatformKind, "youtube">, RegExp> = { instagram: /^@?[A-Za-z0-9._]{1,30}$/, tiktok: /^@?[A-Za-z0-9._]{1,30}$/, telegram: /^@?[A-Za-z0-9_]{5,32}$/ };

const erPattern = /^\d{1,3}([.,]\d{1,2})?$/;

function filledPlatforms(form: BloggerForm) {
  return platformKinds.filter((kind) => form[kind].trim());
}

export function platformTotals(form: BloggerForm) {
  const filled = filledPlatforms(form).map((kind) => ({ followers: normalizeNumericInput(form[followersField(kind)]), reach: normalizeNumericInput(form[reachField(kind)]), er: normalizeDecimalInput(form[erField(kind)]) }));
  const totalFollowers = filled.reduce((sum, item) => sum + item.followers, 0);
  const averageReach = filled.reduce((sum, item) => sum + item.reach, 0);
  // ER of the whole profile is weighted by audience size, so a small channel does not skew it.
  const weighted = filled.filter((item) => Number.isFinite(item.er) && item.followers > 0);
  const weight = weighted.reduce((sum, item) => sum + item.followers, 0);
  const engagementRate = weight > 0 ? Math.round(weighted.reduce((sum, item) => sum + item.er * item.followers, 0) / weight * 100) / 100 : NaN;
  return { totalFollowers, averageReach, engagementRate };
}

function initialBloggerForm(firstName?: string, username?: string): BloggerForm {
  const normalizedUsername = username?.trim().replace(/^@+/, "");
  return { ...initial, name: firstName?.trim() ?? "", username: normalizedUsername ? `@${normalizedUsername}` : "" };
}

function validate(form: BloggerForm, categories: string[], t: (key: string) => string): Errors {
  const errors: Errors = {};
  if (!form.name.trim() || form.name.trim().length > 100) errors.name = t("form.validation.name");
  if (form.lastName.trim().length > 100) errors.lastName = t("form.validation.name");
  if (!form.city.trim()) errors.city = t("form.validation.city");
  if (form.email && (form.email.length > 254 || !/^\S+@\S+\.\S+$/.test(form.email))) errors.email = t("form.validation.email");
  if (!categories.length || categories.length > 5) errors.categories = t("form.validation.categories");
  if (!filledPlatforms(form).length) errors.platforms = t("form.validation.platforms");
  else if (platformTotals(form).totalFollowers > maxFollowers) errors.platforms = t("form.validation.followersTooMany");
  for (const kind of filledPlatforms(form)) {
    const handle = form[kind].trim();
    if (kind === "youtube" ? !safeExternalUrl(handle) : !handlePatterns[kind].test(handle)) errors[kind] = t(kind === "youtube" ? "form.validation.website" : "form.validation.socialUsername");
    const followers = normalizeNumericInput(form[followersField(kind)]);
    if (followers <= 0) errors[followersField(kind)] = t("form.validation.followers");
    else if (followers > maxFollowers) errors[followersField(kind)] = t("form.validation.followersTooMany");
    if (normalizeNumericInput(form[reachField(kind)]) <= 0) errors[reachField(kind)] = t("form.validation.reach");
    const rawEr = form[erField(kind)].trim();
    const engagementRate = normalizeDecimalInput(rawEr);
    // "5,5,5" or "1e3" must not quietly become 5.55 or 13.
    if (rawEr && !erPattern.test(rawEr)) errors[erField(kind)] = t("form.validation.erFormat");
    else if (!Number.isFinite(engagementRate) || engagementRate < 0.1 || engagementRate > 100) errors[erField(kind)] = t("form.validation.er");
  }
  if (normalizeNumericInput(form.storiesPrice) <= 0) errors.storiesPrice = t("form.validation.stories");
  if (normalizeNumericInput(form.reelsPrice) <= 0) errors.reelsPrice = t("form.validation.reels");
  if (form.bio.length > 500) errors.bio = t("form.validation.bio");
  if (form.portfolioUrl && !safeExternalUrl(form.portfolioUrl)) errors.portfolioUrl = t("form.validation.website");
  return errors;
}

function validationMessages(t: (key: string) => string): Record<string, string> {
  return {
    name: t("form.validation.name"), lastName: t("form.validation.name"), username: t("form.validation.username"), city: t("form.validation.city"), phone: t("form.validation.phone"), email: t("form.validation.email"), categories: t("form.validation.categories"), platforms: t("form.validation.platforms"), storiesPrice: t("form.validation.stories"), reelsPrice: t("form.validation.reels"), postPrice: t("form.validation.stories"), integrationPrice: t("form.validation.stories"), bio: t("form.validation.bio"), portfolioUrl: t("form.validation.website"), instagram: t("form.validation.socialUsername"), telegram: t("form.validation.socialUsername"), tiktok: t("form.validation.socialUsername"), youtube: t("form.validation.website")
  };
}

function fieldFromServerName(value: string): ErrorKey | undefined {
  const field = value.toLowerCase().replace(/\[.*$/, "");
  // Profile totals are derived from the platforms, so their server errors point back at the platforms block.
  const mapping: Record<string, ErrorKey> = {
    lastname: "lastName", totalfollowers: "platforms", averagereach: "platforms", engagementrate: "platforms", storiesprice: "storiesPrice", reelsprice: "reelsPrice", postprice: "postPrice", integrationprice: "integrationPrice", portfolioitems: "portfolioUrl", platforms: "platforms", avatar: "bio", media: "bio"
  };
  if (mapping[field]) return mapping[field];
  return ["name", "username", "city", "phone", "email", "categories", "bio", "portfoliourl"].includes(field) ? (field === "portfoliourl" ? "portfolioUrl" : field as ErrorKey) : undefined;
}

function stepForField(field: ErrorKey): Step {
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
  const telegramUsername = useTelegramHandle();
  const verifiedPhone = useVerifiedPhone();
  const [selectedCategories, setSelectedCategories] = useState<string[]>(["lifestyle"]);
  const [barterEnabled, setBarterEnabled] = useState(true);
  const [touched, setTouched] = useState<Partial<Record<ErrorKey, boolean>>>({});
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
      const stats: Record<string, string> = {};
      const handles: Partial<Record<PlatformKind, string>> = {};
      const saved = platformKinds.flatMap((kind) => {
        const item = profile.platforms.find((platform) => platform.type.toLowerCase() === kind);
        return item ? [{ kind, item }] : [];
      });
      for (const { kind, item } of saved) {
        handles[kind] = kind === "youtube" ? item.url : kind === "telegram" ? `@${socialHandle(item.url)}` : socialHandle(item.url);
        // Profiles saved before per-platform stats have only totals; with a single platform those totals are that platform's.
        const legacy = saved.length === 1 && item.followers == null;
        stats[followersField(kind)] = formatNumericInput(String((legacy ? profile.totalFollowers : item.followers) ?? ""));
        stats[reachField(kind)] = formatNumericInput(String((legacy ? profile.averageReach : item.averageReach) ?? ""));
        stats[erField(kind)] = formatDecimalInput((legacy ? profile.engagementRate : item.engagementRate) ?? "");
      }
      setForm({ ...initial, ...stats, name: profile.name, lastName: profile.lastName ?? "", username: profile.username ?? "", city: normalizeRegion(profile.city) || initial.city, phone: formatPhoneInput(profile.phone ?? initial.phone), email: profile.email ?? "", storiesPrice: formatNumericInput(String(profile.storiesPrice ?? "")), reelsPrice: formatNumericInput(String(profile.reelsPrice ?? "")), postPrice: formatNumericInput(String(profile.postPrice ?? "")), integrationPrice: formatNumericInput(String(profile.integrationPrice ?? "")), bio: profile.bio ?? "", portfolioUrl: profile.portfolioItems[0]?.url ?? "", instagram: handles.instagram ?? "", telegram: handles.telegram ?? "", tiktok: handles.tiktok ?? "", youtube: handles.youtube ?? "" });
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

  const clearServerError = useCallback((field: ErrorKey) => {
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

  const numeric = (key: Field) => (event: ChangeEvent<HTMLInputElement>) => {
    setDirty(true);
    clearServerError(key);
    clearServerError("platforms");
    setForm((current) => ({ ...current, [key]: formatNumericInput(event.target.value) }));
  };

  const decimal = (key: Field) => (event: ChangeEvent<HTMLInputElement>) => {
    setDirty(true);
    clearServerError(key);
    clearServerError("platforms");
    setForm((current) => ({ ...current, [key]: event.target.value }));
  };

  const updateHandle = (kind: PlatformKind) => (event: ChangeEvent<HTMLInputElement>) => {
    clearServerError("platforms");
    update(kind)(event);
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

  const blur = (field: ErrorKey) => () => {
    setTouched((current) => ({ ...current, [field]: true }));
    if (field === "portfolioUrl" || field === "youtube") setForm((current) => ({ ...current, [field]: normalizeWebsite(current[field]) }));
    // Only a well-formed ER is tidied up; anything else stays as typed so its error is visible.
    if (field.endsWith("Er")) setForm((current) => erPattern.test(current[field as Field].trim()) ? { ...current, [field]: formatDecimalInput(current[field as Field]) } : current);
  };

  const markStepTouched = useCallback((currentStep: Exclude<Step, 4>) => {
    setTouched((current) => ({ ...current, ...Object.fromEntries(stepFields[currentStep].map((field) => [field, true])) }));
  }, []);

  const focusField = useCallback((field: ErrorKey) => {
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
      const totals = platformTotals(form);
      const platformUrl = (kind: PlatformKind) => kind === "youtube" ? normalizeWebsite(form.youtube) : socialUrl(kind, form[kind]);
      const platforms = filledPlatforms(form).map((kind) => ({ type: kind, url: platformUrl(kind), followers: normalizeNumericInput(form[followersField(kind)]), averageReach: normalizeNumericInput(form[reachField(kind)]), engagementRate: normalizeDecimalInput(form[erField(kind)]) }));
      const input = { name: form.name.trim(), lastName: form.lastName.trim() || undefined, username: telegramUsername ?? undefined, city: form.city.trim(), categories: selectedCategories, bio: form.bio.trim() || undefined, avatarUrl, phone: verifiedPhone ?? undefined, email: form.email.trim() || undefined, totalFollowers: totals.totalFollowers, averageReach: totals.averageReach, engagementRate: totals.engagementRate, storiesPrice: normalizeNumericInput(form.storiesPrice), reelsPrice: normalizeNumericInput(form.reelsPrice), postPrice: normalizeNumericInput(form.postPrice) || undefined, integrationPrice: normalizeNumericInput(form.integrationPrice) || undefined, barterEnabled, portfolioItems: portfolioUrl ? [{ title: t("form.blogger.portfolioTitle"), type: "IMAGE" as const, url: portfolioUrl }] : [], platforms };
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
        const fields = error.validationFields.map(fieldFromServerName).filter((field): field is ErrorKey => Boolean(field));
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
  const hasPortfolioDetails = Boolean(reviewAvatarUrl || form.bio.trim() || form.portfolioUrl.trim());
  const totals = platformTotals(form);
  const progressLabel = t("wizard.stepOf", { current: step + 1, total: 5 });
  const submitLabel = existing ? t("wizard.saveChanges") : t("wizard.createProfile");
  const actionLabel = step === 4 ? (saving ? t("form.publishing") : submitLabel) : t("wizard.continue");

  return <form noValidate onSubmit={submit}>
    <WizardLayout actionBar={<FixedActionBar key={step} backLabel={t("common.back")} continueLabel={actionLabel} disabled={step === 4 ? false : !currentStepValid} loading={saving} onBack={goBack} onPrimary={step === 4 ? undefined : continueStep} submit={step === 4} />}>
      <WizardHeader backLabel={t("common.back")} onBack={goBack} progressLabel={progressLabel} showBackButton={!isTelegram} showLanguage={Boolean(onCompleted)} step={step + 1} stepTitle={stepTitles[step]} totalSteps={5} />
      <WizardErrorSummary message={serverSummary} />
      {step === 0 && <WizardStep stepKey={stepTitles[0]}><div className="wizard-fields">
        <div data-wizard-field="name"><Input className="wizard-input" error={touched.name ? errors.name : undefined} label={t("form.name")} maxLength={100} onBlur={blur("name")} onChange={update("name")} placeholder={t("form.blogger.namePlaceholder")} required value={form.name} /></div>
        <div data-wizard-field="lastName"><Input className="wizard-input" error={touched.lastName ? errors.lastName : undefined} label={t("form.lastName")} maxLength={100} onBlur={blur("lastName")} onChange={update("lastName")} placeholder={t("form.blogger.lastNamePlaceholder")} value={form.lastName} /></div>
        <TelegramHandleField />
        <div data-wizard-field="city"><RegionSelect className="wizard-region-select" error={touched.city ? errors.city : undefined} onChange={selectCity} required value={form.city} /></div>
        <VerifiedPhoneField phone={verifiedPhone} />
        <div data-wizard-field="email"><Input className="wizard-input" error={touched.email ? errors.email : undefined} label={t("form.emailOptional")} maxLength={254} onBlur={blur("email")} onChange={update("email")} placeholder="you@email.com" type="email" value={form.email} /></div>
      </div></WizardStep>}
      {step === 1 && <WizardStep stepKey={stepTitles[1]}><div className="wizard-fields">
        <div data-wizard-field="categories"><CategoryMultiSelect error={touched.categories ? errors.categories : undefined} onChange={updateCategories} required value={selectedCategories} /></div>
        <fieldset className="wizard-platforms" data-wizard-field="platforms">
          <legend>{t("form.platformsTitle")}</legend>
          <p className="wizard-field-helper">{t("form.platformsHelper")}</p>
          {platformKinds.map((kind) => {
            const filled = Boolean(form[kind].trim());
            return <div className="wizard-platform" key={kind}>
              <div data-wizard-field={kind}><Input className="wizard-input" error={touched[kind] ? errors[kind] : undefined} id={`platform-${kind}`} label={t(`form.${kind}`)} onBlur={blur(kind)} onChange={updateHandle(kind)} placeholder={kind === "youtube" ? "https://youtube.com/@channel" : kind === "telegram" ? "@channel" : "@username"} type={kind === "youtube" ? "url" : "text"} value={form[kind]} /></div>
              {filled && <div className="wizard-platform__stats">
                <div data-wizard-field={followersField(kind)}><Input className="wizard-input" error={touched[followersField(kind)] ? errors[followersField(kind)] : undefined} id={`platform-${kind}-followers`} inputMode="numeric" label={t("common.followers")} onBlur={blur(followersField(kind))} onChange={numeric(followersField(kind))} placeholder="10 000" required value={form[followersField(kind)]} /></div>
                <div data-wizard-field={reachField(kind)}><Input className="wizard-input" error={touched[reachField(kind)] ? errors[reachField(kind)] : undefined} id={`platform-${kind}-reach`} inputMode="numeric" label={t("form.reachShort")} onBlur={blur(reachField(kind))} onChange={numeric(reachField(kind))} placeholder="5 000" required value={form[reachField(kind)]} /></div>
                <div data-wizard-field={erField(kind)}><Input className="wizard-input" error={touched[erField(kind)] ? errors[erField(kind)] : undefined} id={`platform-${kind}-er`} inputMode="decimal" label={t("form.erShort")} maxLength={6} onBlur={blur(erField(kind))} onChange={decimal(erField(kind))} placeholder="5,5" required suffix="%" value={form[erField(kind)]} /></div>
              </div>}
            </div>;
          })}
          {touched.platforms && errors.platforms && <p className="wizard-platforms__error" role="alert">{errors.platforms}</p>}
          {totals.totalFollowers > 0 && <p className="wizard-platforms__total">{t("form.totalFollowers", { count: formatNumber(totals.totalFollowers) })}</p>}
          <p className="wizard-field-helper">{t("form.engagementRateHelper")}</p>
        </fieldset>
      </div></WizardStep>}
      {step === 2 && <WizardStep stepKey={stepTitles[2]}><div className="wizard-fields">
        <div data-wizard-field="storiesPrice"><Input className="wizard-input" error={touched.storiesPrice ? errors.storiesPrice : undefined} inputMode="numeric" label={t("card.stories")} onBlur={blur("storiesPrice")} onChange={numeric("storiesPrice")} placeholder="200 000" required suffix={t("currency.uzs")} value={form.storiesPrice} /></div>
        <div data-wizard-field="reelsPrice"><Input className="wizard-input" error={touched.reelsPrice ? errors.reelsPrice : undefined} inputMode="numeric" label={t("card.reels")} onBlur={blur("reelsPrice")} onChange={numeric("reelsPrice")} placeholder="500 000" required suffix={t("currency.uzs")} value={form.reelsPrice} /></div>
        <div data-wizard-field="postPrice"><Input className="wizard-input" inputMode="numeric" label={t("form.optionalField", { label: t("card.post") })} onBlur={blur("postPrice")} onChange={numeric("postPrice")} placeholder="350 000" suffix={t("currency.uzs")} value={form.postPrice} /></div>
        <div data-wizard-field="integrationPrice"><Input className="wizard-input" inputMode="numeric" label={t("form.optionalField", { label: t("card.integration") })} onBlur={blur("integrationPrice")} onChange={numeric("integrationPrice")} placeholder="900 000" suffix={t("currency.uzs")} value={form.integrationPrice} /></div>
        <button aria-checked={barterEnabled} aria-label={t("form.barterTitle")} className="wizard-toggle" onClick={() => { setDirty(true); setBarterEnabled((value) => !value); }} role="switch" type="button"><span><strong>{t("form.barterTitle")}</strong><small>{t("form.barterSubtitle")}</small></span><span aria-hidden="true" className="wizard-toggle__control"><span /></span></button>
      </div></WizardStep>}
      {step === 3 && <WizardStep stepKey={stepTitles[3]}><div className="wizard-fields">
        <ProfileMediaPicker className="wizard-media-picker" currentUrl={avatarUrl} disabled={saving} name={form.name || t("profile.telegramUser")} onChange={(image) => { setDirty(true); setPendingImage(image); }} pending={pendingImage} />
        <div data-wizard-field="bio"><Textarea className="wizard-input" error={touched.bio ? errors.bio : undefined} label={t("form.aboutMe")} maxLength={500} onBlur={blur("bio")} onChange={update("bio")} placeholder={t("form.aboutMePlaceholder")} value={form.bio} /></div>
        <div data-wizard-field="portfolioUrl"><Input className="wizard-input" error={touched.portfolioUrl ? errors.portfolioUrl : undefined} label={t("form.portfolioUrl")} onBlur={blur("portfolioUrl")} onChange={update("portfolioUrl")} placeholder="https://..." type="url" value={form.portfolioUrl} /></div>
      </div></WizardStep>}
      {step === 4 && <WizardStep stepKey={stepTitles[4]}><div className="wizard-review">
        <ReviewSection editAriaLabel={t("wizard.changeSection", { section: stepTitles[0] })} editLabel={t("wizard.change")} onEdit={() => setStep(0)} title={stepTitles[0]}>
          <ReviewItem label={t("form.name")} value={[form.name.trim(), form.lastName.trim()].filter(Boolean).join(" ")} /><ReviewItem label={t("form.telegramUsername")} value={telegramUsername ?? t("form.telegramNoUsername")} /><ReviewItem label={t("common.city")} value={cityLabel(form.city, language)} /><ReviewItem label={t("common.phone")} value={verifiedPhone ?? t("phone.missing")} /><ReviewItem label={t("form.emailOptional")} value={form.email.trim()} />
        </ReviewSection>
        <ReviewSection editAriaLabel={t("wizard.changeSection", { section: stepTitles[1] })} editLabel={t("wizard.change")} onEdit={() => setStep(1)} title={stepTitles[1]}>
          <ReviewItem label={t("common.categories")} value={selectedCategories.map((category) => reviewCategory(category, language)).join(", ")} />{filledPlatforms(form).map((kind) => <ReviewItem key={kind} label={platformLabel(kind, t)} value={t("form.platformSummary", { followers: formatNumber(normalizeNumericInput(form[followersField(kind)])), reach: formatNumber(normalizeNumericInput(form[reachField(kind)])), er: formatPercentage(form[erField(kind)]) })} />)}<ReviewItem label={t("form.totalFollowersLabel")} value={formatNumber(totals.totalFollowers)} />
        </ReviewSection>
        <ReviewSection editAriaLabel={t("wizard.changeSection", { section: stepTitles[2] })} editLabel={t("wizard.change")} onEdit={() => setStep(2)} title={stepTitles[2]}>
          <ReviewItem label={t("card.stories")} value={formatCurrency(normalizeNumericInput(form.storiesPrice))} /><ReviewItem label={t("card.reels")} value={formatCurrency(normalizeNumericInput(form.reelsPrice))} /><ReviewItem label={t("card.post")} value={form.postPrice ? formatCurrency(normalizeNumericInput(form.postPrice)) : undefined} /><ReviewItem label={t("card.integration")} value={form.integrationPrice ? formatCurrency(normalizeNumericInput(form.integrationPrice)) : undefined} /><ReviewItem label={t("form.barterReviewTitle")} value={barterEnabled ? t("common.yes") : t("common.no")} />
        </ReviewSection>
        <ReviewSection editAriaLabel={t("wizard.changeSection", { section: stepTitles[3] })} editLabel={t("wizard.change")} emptyLabel={t("common.notSpecified")} isEmpty={!hasPortfolioDetails} onEdit={() => setStep(3)} title={stepTitles[3]}>
          {reviewAvatarUrl && <img alt={t("profileMedia.title")} className="wizard-review__logo" src={reviewAvatarUrl} />}<ReviewItem label={t("form.aboutMe")} value={form.bio.trim()} /><ReviewItem label={t("form.portfolioUrl")} value={form.portfolioUrl.trim()} />
        </ReviewSection>
      </div></WizardStep>}
    </WizardLayout>
    <Toast message={toast} tone="error" />
    <Modal onClose={() => setSuccess(false)} open={success} title={t("form.successTitle")}><p className="text-sm leading-6 text-brand-muted">{t("form.successDescription")}</p><Button className="mt-5 w-full" onClick={() => { window.location.hash = "/profile"; }} type="button"><Icon name="check" />{t("form.understood")}</Button></Modal>
    <UnsavedChangesDialog guard={unsavedChanges} />
  </form>;
}
