import { useCallback, useEffect, useMemo, useState } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { getMyCampaign, updateMyCampaign, type MyCampaignDetails, type UpdateMyCampaignInput } from "../api/marketplace";
import { CategoryMultiSelect } from "../components/CategoryMultiSelect";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { ManagementBackLink } from "../components/ManagementBackLink";
import { RegionSelect } from "../components/RegionSelect";
import { BottomNav, Button, ErrorState, Input, LoadingState, Textarea, Toast } from "../components/ui";
import { useCampaignDataRefresh, notifyCampaignDataChanged } from "../hooks/useCampaignDataRefresh";
import { UnsavedChangesDialog, useUnsavedChanges } from "../hooks/useUnsavedChanges";
import { useI18n } from "../i18n";
import { formatNumericInput, normalizeNumericInput } from "../lib/currency";

type DetailState = "not-found" | "denied" | "failed" | null;
type FormValues = {
  title: string;
  description: string;
  city: string;
  categories: string[];
  requirements: string;
  budgetFrom: string;
  budgetTo: string;
  deadline: string;
};
type FieldErrors = Partial<Record<keyof FormValues, string>>;

const emptyValues: FormValues = { title: "", description: "", city: "", categories: [], requirements: "", budgetFrom: "", budgetTo: "", deadline: "" };

function toValues(campaign: MyCampaignDetails): FormValues {
  return {
    title: campaign.title,
    description: campaign.description,
    city: campaign.city ?? "",
    categories: campaign.categories,
    requirements: campaign.requirements.join(", "),
    budgetFrom: campaign.minBudget == null ? "" : formatNumericInput(String(campaign.minBudget)),
    budgetTo: campaign.maxBudget == null ? "" : formatNumericInput(String(campaign.maxBudget)),
    deadline: campaign.deadline?.slice(0, 10) ?? ""
  };
}

function toBudget(value: string) {
  return value.trim() === "" ? null : normalizeNumericInput(value);
}

function requirementsFrom(value: string) {
  return value.split(",").map((item) => item.trim()).filter(Boolean);
}

function validate(values: FormValues, t: (key: string) => string): FieldErrors {
  const errors: FieldErrors = {};
  const requirements = requirementsFrom(values.requirements);
  const budgetFrom = toBudget(values.budgetFrom);
  const budgetTo = toBudget(values.budgetTo);
  if (!values.title.trim() || values.title.trim().length > 160) errors.title = t("myCampaignEdit.validationTitle");
  if (!values.description.trim() || values.description.trim().length > 3000) errors.description = t("myCampaignEdit.validationDescription");
  if (!values.categories.length || values.categories.length > 5 || values.categories.some((item) => !item.trim() || item.length > 50)) errors.categories = t("myCampaignEdit.validationCategories");
  if (values.city.length > 80) errors.city = t("myCampaignEdit.validationCity");
  if (requirements.length > 10 || requirements.some((item) => item.length > 300)) errors.requirements = t("myCampaignEdit.validationRequirements");
  if ((budgetFrom != null && budgetFrom < 0) || (budgetTo != null && budgetTo < 0) || (budgetFrom != null && budgetTo != null && budgetFrom > budgetTo)) errors.budgetTo = t("myCampaignEdit.validationBudget");
  if (values.deadline && Number.isNaN(Date.parse(`${values.deadline}T00:00:00.000Z`))) errors.deadline = t("myCampaignEdit.validationDeadline");
  return errors;
}

function errorState(error: unknown): DetailState {
  if (!(error instanceof ApiError)) return "failed";
  if (error.status === 404) return "not-found";
  if (error.status === 401 || error.status === 403) return "denied";
  return "failed";
}

export function MyCampaignEdit({ id }: { id: string }) {
  const { t } = useI18n();
  const [values, setValues] = useState<FormValues>(emptyValues);
  const [snapshot, setSnapshot] = useState("");
  const [loading, setLoading] = useState(true);
  const [failure, setFailure] = useState<DetailState>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [submitting, setSubmitting] = useState(false);
  const [toast, setToast] = useState("");
  const dirty = Boolean(snapshot) && snapshot !== JSON.stringify(values);
  const unsavedChanges = useUnsavedChanges(dirty);

  const load = useCallback(() => {
    const controller = new AbortController();
    setLoading(true);
    setFailure(null);
    getMyCampaign(id, controller.signal).then((campaign) => {
      if (campaign.status === 2) {
        setFailure("not-found");
        return;
      }
      const nextValues = toValues(campaign);
      setValues(nextValues);
      setSnapshot(JSON.stringify(nextValues));
    }).catch((error: unknown) => {
      if (!controller.signal.aborted) setFailure(errorState(error));
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, [id]);

  useEffect(() => load(), [load]);
  useCampaignDataRefresh(() => { if (!submitting) void load(); }, !loading);

  const setValue = <Key extends keyof FormValues>(key: Key, value: FormValues[Key]) => {
    setValues((current) => ({ ...current, [key]: value }));
    setFieldErrors((current) => ({ ...current, [key]: undefined }));
  };
  const validationMessages = useMemo(() => ({
    title: t("myCampaignEdit.validationTitle"),
    description: t("myCampaignEdit.validationDescription"),
    city: t("myCampaignEdit.validationCity"),
    categories: t("myCampaignEdit.validationCategories"),
    requirements: t("myCampaignEdit.validationRequirements"),
    budgetfrom: t("myCampaignEdit.validationBudget"),
    budgetto: t("myCampaignEdit.validationBudget"),
    deadline: t("myCampaignEdit.validationDeadline")
  }), [t]);

  const applyServerFieldErrors = (error: ApiError) => {
    if (error.code !== "validation_failed") return;
    const fieldMap: Record<string, keyof FormValues> = {
      title: "title",
      description: "description",
      city: "city",
      categories: "categories",
      requirements: "requirements",
      budgetfrom: "budgetFrom",
      budgetto: "budgetTo",
      deadline: "deadline"
    };
    const nextErrors = error.validationFields.reduce<FieldErrors>((errors, field) => {
      const normalized = field.toLowerCase();
      const target = fieldMap[normalized];
      if (target) errors[target] = validationMessages[normalized as keyof typeof validationMessages] ?? t("myCampaignEdit.validationSummary");
      return errors;
    }, {});
    if (Object.keys(nextErrors).length) setFieldErrors(nextErrors);
  };

  const onSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (submitting) return;
    const nextErrors = validate(values, t);
    if (Object.keys(nextErrors).length) {
      setFieldErrors(nextErrors);
      setToast(t("myCampaignEdit.validationSummary"));
      return;
    }
    const payload: UpdateMyCampaignInput = {
      title: values.title.trim(),
      description: values.description.trim(),
      city: values.city.trim() || null,
      categories: values.categories,
      requirements: requirementsFrom(values.requirements),
      budgetFrom: toBudget(values.budgetFrom),
      budgetTo: toBudget(values.budgetTo),
      deadline: values.deadline ? new Date(`${values.deadline}T00:00:00.000Z`).toISOString() : null
    };
    setSubmitting(true);
    try {
      await updateMyCampaign(id, payload);
      notifyCampaignDataChanged();
      sessionStorage.setItem(`bloggerbazar.my-campaign-feedback:${id}`, "saved");
      window.location.hash = `/my-campaign/${id}`;
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        sessionStorage.setItem(`bloggerbazar.my-campaign-feedback:${id}`, "conflict");
        window.location.hash = `/my-campaign/${id}`;
        return;
      }
      if (error instanceof ApiError) applyServerFieldErrors(error);
      setToast(getApiErrorMessage(error, t("myCampaignEdit.saveFailed"), { validationMessages }));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <div className="campaign-management-screen screen screen--with-nav"><LoadingState title={t("myCampaignEdit.loading")} /><BottomNav /></div>;
  if (failure) return <div className="campaign-management-screen screen screen--with-nav"><ErrorState onRetry={failure === "failed" ? load : undefined} subtitle={t(failure === "not-found" ? "myCampaignEdit.notFoundSubtitle" : failure === "denied" ? "myCampaignEdit.deniedSubtitle" : "myCampaignEdit.errorSubtitle")} title={t(failure === "not-found" ? "myCampaignEdit.notFoundTitle" : failure === "denied" ? "myCampaignEdit.deniedTitle" : "myCampaignEdit.errorTitle")} /><BottomNav /></div>;

  return <div className="campaign-management-screen my-campaign-edit screen screen--with-nav">
    <header className="my-campaign-edit__header"><ManagementBackLink ariaLabel={t("myCampaignEdit.backAria")} href={`#/my-campaign/${id}`} /><LanguageSwitcher /></header>
    <div className="my-campaign-edit__heading"><p>{t("myCampaignEdit.eyebrow")}</p><h1>{t("myCampaignEdit.title")}</h1></div>
    <form className="my-campaign-edit__form" onSubmit={onSubmit} noValidate>
      <Input error={fieldErrors.title} label={t("campaigns.title")} maxLength={160} onChange={(event) => setValue("title", event.target.value)} required value={values.title} />
      <Textarea error={fieldErrors.description} label={t("myCampaignDetails.description")} maxLength={3000} onChange={(event) => setValue("description", event.target.value)} required value={values.description} />
      <RegionSelect error={fieldErrors.city} onChange={(event) => setValue("city", event.target.value)} value={values.city} />
      <CategoryMultiSelect error={fieldErrors.categories} onChange={(categories) => setValue("categories", categories)} required value={values.categories} />
      <Input error={fieldErrors.requirements} label={t("common.requirements")} maxLength={3000} onChange={(event) => setValue("requirements", event.target.value)} placeholder={t("myCampaignEdit.requirementsPlaceholder")} value={values.requirements} />
      <div className="my-campaign-edit__budget-grid">
        <Input error={fieldErrors.budgetFrom} inputMode="numeric" label={t("campaigns.budgetFrom")} onChange={(event) => setValue("budgetFrom", formatNumericInput(event.target.value))} suffix={t("currency.uzs")} value={values.budgetFrom} />
        <Input error={fieldErrors.budgetTo} inputMode="numeric" label={t("campaigns.budgetTo")} onChange={(event) => setValue("budgetTo", formatNumericInput(event.target.value))} suffix={t("currency.uzs")} value={values.budgetTo} />
      </div>
      <Input error={fieldErrors.deadline} label={t("campaigns.deadline")} onChange={(event) => setValue("deadline", event.target.value)} type="date" value={values.deadline} />
      <Button aria-busy={submitting} className="my-campaign-edit__submit" disabled={submitting || !dirty} type="submit">{submitting ? t("myCampaignEdit.saving") : t("myCampaignEdit.save")}</Button>
    </form>
    <UnsavedChangesDialog guard={unsavedChanges} />
    <Toast message={toast} tone="error" />
    <BottomNav />
  </div>;
}
