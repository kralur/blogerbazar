import { useCallback, useEffect, useMemo, useRef, useState, type ChangeEvent } from "react";
import { ApiError, getApiErrorMessage } from "../api/client";
import { createCampaign, getCampaignCatalog, getCategories, getCurrentPlatformUser, getMyBusinessProfile, normalizeMarketplaceRole, type CampaignCatalogItem, type CampaignCatalogQuery, type CampaignCatalogSort } from "../api/marketplace";
import { CampaignCard, type CampaignCardData } from "../components/CampaignCard";
import { ActiveFilterChips, CatalogHeader, CatalogState, FilterSelect, SearchSkeleton } from "../components/catalog/CatalogShared";
import { usePaginatedCatalog } from "../components/catalog/usePaginatedCatalog";
import { CategoryMultiSelect } from "../components/CategoryMultiSelect";
import { LanguageSwitcher } from "../components/LanguageSwitcher";
import { RegionSelect } from "../components/RegionSelect";
import { BottomNav, BottomSheet, Button, FloatingActionButton, Icon, Input, Modal, SearchBar, Textarea, Toast } from "../components/ui";
import { useDebouncedValue } from "../hooks/useDebouncedValue";
import { useProfileDataRefresh } from "../hooks/useProfileDataRefresh";
import { useScrollRestoration } from "../hooks/useScrollRestoration";
import { categoryLabel, cityLabel, useI18n } from "../i18n";
import { formatNumericInput, normalizeNumericInput } from "../lib/currency";
import { isOtherCategory, uzbekistanRegions } from "../lib/taxonomy";
import { useRootScreenVisibility } from "../navigation/RootScreenVisibility";
import { useTelegram } from "../telegram/TelegramProvider";

const pageSize = 20;
const filterSheetId = "campaign-catalog-filters";
const initialForm = { title: "", description: "", city: "tashkent-city", categories: ["lifestyle"], requirements: "", deadline: "", budgetFrom: "500 000", budgetTo: "1 500 000" };
const defaultFilters: CampaignCatalogQuery = { sort: "promoted", pageSize };

type CampaignFilterKey = "category" | "city" | "minBudget" | "maxBudget" | "deadlineFrom" | "deadlineTo";
type CampaignActiveFilterKey = CampaignFilterKey | "budget" | "deadline";
type CreateCapability = "none" | "profileMissing" | "ready";
type Translate = (key: string, values?: Record<string, string | number>) => string;

function normalizeFilters(filters: CampaignCatalogQuery): CampaignCatalogQuery {
  return {
    ...defaultFilters,
    ...Object.fromEntries(Object.entries(filters).filter(([, value]) => value !== "" && value !== undefined)),
    pageSize
  };
}

function asCampaignCard(campaign: CampaignCatalogItem): CampaignCardData {
  return {
    id: campaign.id,
    title: campaign.title,
    businessName: campaign.businessName,
    businessAvatarUrl: campaign.businessAvatarUrl,
    city: campaign.city,
    categories: campaign.categories,
    requirements: campaign.requirements,
    budgetFrom: campaign.minBudget,
    budgetTo: campaign.maxBudget,
    deadline: campaign.deadline,
    isPromoted: campaign.isPromoted,
    status: campaign.status,
    createdAtUtc: campaign.createdAtUtc
  };
}

export function Campaigns() {
  const { language, t } = useI18n();
  const { haptic } = useTelegram();
  const active = useRootScreenVisibility();
  useScrollRestoration("campaigns");
  const [categories, setCategories] = useState<string[]>([]);
  const [form, setForm] = useState(initialForm);
  const [createOpen, setCreateOpen] = useState(false);
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [createCapability, setCreateCapability] = useState<CreateCapability>("none");
  const [query, setQuery] = useState("");
  const [appliedFilters, setAppliedFilters] = useState<CampaignCatalogQuery>(() => normalizeFilters({}));
  const [draftFilters, setDraftFilters] = useState<CampaignCatalogQuery>(() => normalizeFilters({}));
  const [toast, setToast] = useState("");
  const [toastTone, setToastTone] = useState<"success" | "error" | "warning">("success");
  const draftFiltersRef = useRef<CampaignCatalogQuery>(normalizeFilters({}));
  const lastCatalogKeyRef = useRef("");
  const sentinelRef = useRef<HTMLDivElement | null>(null);
  const debouncedQuery = useDebouncedValue(query, 350);

  const fetchPage = useCallback(async (requestedPage: number, signal: AbortSignal) => {
    const response = await getCampaignCatalog({ ...appliedFilters, query: debouncedQuery.trim() || undefined, page: requestedPage, pageSize }, signal);
    return { ...response, items: response.items.map(asCampaignCard) };
  }, [appliedFilters, debouncedQuery]);
  const { items, total, loading, loadingMore, loadMoreFailed, failure, page, hasMore, loadedInitialResult, load, cancel } = usePaginatedCatalog<CampaignCardData>({ active, fetchPage });

  const catalogKey = useMemo(() => JSON.stringify({ ...appliedFilters, query: debouncedQuery.trim() }), [appliedFilters, debouncedQuery]);
  const activeChips = useMemo(() => buildActiveChips(appliedFilters, language, t), [appliedFilters, language, t]);
  const filterError = getFilterError(draftFilters, t);
  const hasFilterOrQuery = activeChips.length > 0 || Boolean(debouncedQuery.trim());
  const canCreate = createCapability === "ready";
  const needsBusinessProfile = createCapability === "profileMissing";
  const isEmptyResult = !loading && !failure && items.length === 0;
  const isDefaultEmpty = isEmptyResult && !hasFilterOrQuery;

  const refreshCatalog = useCallback(() => {
    if (!active) return;
    lastCatalogKeyRef.current = catalogKey;
    void load(1, false);
  }, [active, catalogKey, load]);

  const refreshCreateCapability = useCallback(async () => {
    try {
      const user = await getCurrentPlatformUser();
      if (normalizeMarketplaceRole(user.selectedMarketplaceRole) !== "Business") {
        setCreateCapability("none");
        return;
      }
      await getMyBusinessProfile();
      setCreateCapability("ready");
    } catch (error) {
      setCreateCapability(error instanceof ApiError && error.status === 404 ? "profileMissing" : "none");
    }
  }, []);

  useEffect(() => {
    void getCategories().then(setCategories).catch(() => undefined);
    void refreshCreateCapability();
  }, [refreshCreateCapability]);

  useEffect(() => {
    if (!active || loading || (lastCatalogKeyRef.current === catalogKey && (loadedInitialResult || failure))) return;
    lastCatalogKeyRef.current = catalogKey;
    void load(1, false);
  }, [active, catalogKey, failure, load, loadedInitialResult, loading]);

  useEffect(() => {
    if (!active) {
      setFiltersOpen(false);
      cancel();
      return;
    }
    const sentinel = sentinelRef.current;
    if (!sentinel || loading || loadingMore || loadMoreFailed || !hasMore || failure) return;
    const observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) void load(page + 1, true);
    }, { rootMargin: "240px" });
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [active, cancel, failure, hasMore, load, loadMoreFailed, loading, loadingMore, page]);

  useProfileDataRefresh(() => {
    void refreshCreateCapability();
    refreshCatalog();
  });

  const openFilters = () => {
    draftFiltersRef.current = appliedFilters;
    setDraftFilters(appliedFilters);
    setFiltersOpen(true);
  };

  const closeFilters = () => setFiltersOpen(false);

  const setDraft = (key: CampaignFilterKey, value: string | number | undefined) => {
    const next = normalizeFilters({ ...draftFiltersRef.current, [key]: value === "" ? undefined : value });
    draftFiltersRef.current = next;
    setDraftFilters(next);
  };

  const applyFilters = () => {
    if (filterError) return;
    haptic.selection();
    setAppliedFilters(normalizeFilters(draftFiltersRef.current));
    setFiltersOpen(false);
  };

  const resetCatalog = () => {
    haptic.selection();
    cancel();
    const reset = normalizeFilters({});
    draftFiltersRef.current = reset;
    lastCatalogKeyRef.current = "";
    setDraftFilters(reset);
    setAppliedFilters(reset);
    setQuery("");
    setFiltersOpen(false);
  };

  const removeFilter = (key: CampaignActiveFilterKey) => {
    haptic.selection();
    cancel();
    lastCatalogKeyRef.current = "";
    const values = key === "budget" ? { minBudget: undefined, maxBudget: undefined } : key === "deadline" ? { deadlineFrom: undefined, deadlineTo: undefined } : { [key]: undefined };
    setAppliedFilters((current) => normalizeFilters({ ...current, ...values }));
    const nextDraft = normalizeFilters({ ...draftFiltersRef.current, ...values });
    draftFiltersRef.current = nextDraft;
    setDraftFilters(nextDraft);
  };

  const changeSort = (sort: CampaignCatalogSort) => {
    haptic.selection();
    cancel();
    lastCatalogKeyRef.current = "";
    setAppliedFilters((current) => normalizeFilters({ ...current, sort }));
    const nextDraft = normalizeFilters({ ...draftFiltersRef.current, sort });
    draftFiltersRef.current = nextDraft;
    setDraftFilters(nextDraft);
  };

  const updateForm = (key: keyof typeof initialForm) => (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => setForm((previous) => ({ ...previous, [key]: event.target.value }));
  const create = async () => {
    if (saving || !form.title.trim() || !form.description.trim() || !form.categories.length) return;
    try {
      setSaving(true);
      await createCampaign({ title: form.title.trim(), description: form.description.trim(), city: form.city || undefined, categories: form.categories, requirements: form.requirements.split(",").map((item) => item.trim()).filter(Boolean), deadline: form.deadline ? new Date(`${form.deadline}T23:59:59`).toISOString() : undefined, budgetFrom: normalizeNumericInput(form.budgetFrom) || undefined, budgetTo: normalizeNumericInput(form.budgetTo) || undefined, publishImmediately: true });
      haptic.success();
      setCreateOpen(false);
      setForm(initialForm);
      setToastTone("success");
      setToast(t("campaigns.created"));
      refreshCatalog();
    } catch (error) {
      haptic.error();
      setToastTone("error");
      setToast(getApiErrorMessage(error, t("campaigns.createFailed")));
    } finally {
      setSaving(false);
    }
  };

  return <div aria-hidden={!active} className="campaign-catalog catalog-search screen screen--with-nav" hidden={!active}>
    <CatalogHeader>
      <div className="catalog-search__heading"><p className="catalog-search__eyebrow">{t("campaigns.eyebrow")}</p><h1>{t("campaigns.title")}</h1></div>
      <LanguageSwitcher />
    </CatalogHeader>
    <div className="catalog-search__searchbar"><SearchBar clearAriaLabel={t("campaigns.clearSearchAria")} className="catalog-search__search-control" onChange={(event) => setQuery(event.target.value)} onClear={() => setQuery("")} placeholder={t("campaigns.search")} value={query} /></div>
    <div className="catalog-search__controls">
      <button aria-controls={filterSheetId} aria-expanded={filtersOpen} aria-label={t("campaigns.filtersAria")} className="catalog-search__filter-button" onClick={openFilters} type="button"><Icon name="filter" /><span>{t("search.filters")}</span></button>
      <label className="catalog-search__sort"><span>{t("search.sort")}</span><select aria-label={t("search.sort")} onChange={(event) => changeSort(event.target.value as CampaignCatalogSort)} value={appliedFilters.sort ?? "promoted"}>{sortOptions(t).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
    </div>
    <ActiveFilterChips chips={activeChips} onRemove={removeFilter} onReset={resetCatalog} showReset={activeChips.length >= 2} />
    <p aria-live="polite" className="catalog-search__results-count">{loading ? t("campaigns.loading") : t("campaigns.found", { count: total })}</p>
    <section aria-busy={loading || loadingMore} aria-live="polite" className="catalog-search__results">
      {loading ? <SearchSkeleton count={3} /> : failure === "offline" ? <CatalogState icon="refresh" onRetry={refreshCatalog} subtitle={t("ui.offlineSubtitle")} title={t("ui.offlineTitle")} /> : failure === "server" ? <CatalogState icon="refresh" onRetry={refreshCatalog} subtitle={t("campaigns.loadFailedSubtitle")} title={t("campaigns.loadFailedTitle")} /> : items.length ? items.map((campaign) => <CampaignCard campaign={campaign} key={campaign.id} />) : <CatalogState actionLabel={hasFilterOrQuery ? t("search.resetAll") : canCreate ? t("campaigns.create") : needsBusinessProfile ? t("campaigns.createBusinessProfile") : undefined} icon="search" onRetry={hasFilterOrQuery ? resetCatalog : canCreate ? () => setCreateOpen(true) : needsBusinessProfile ? () => { window.location.hash = "/business"; } : undefined} subtitle={hasFilterOrQuery ? t("campaigns.emptySearchSubtitle") : canCreate ? t("campaigns.emptyBusinessSubtitle") : needsBusinessProfile ? t("campaigns.emptyBusinessProfileSubtitle") : t("campaigns.emptySubtitle")} title={hasFilterOrQuery ? t("campaigns.emptySearchTitle") : t("campaigns.emptyTitle")} />}
      {!loading && !failure && items.length > 0 && <>
        <div aria-hidden="true" ref={sentinelRef} />
        {loadingMore && <SearchSkeleton compact count={2} />}
        {loadMoreFailed && <CatalogState compact icon="refresh" onRetry={() => void load(page + 1, true)} subtitle={t("campaigns.loadFailedSubtitle")} title={t("campaigns.loadFailedTitle")} />}
        {loadedInitialResult && !hasMore && !loadingMore && !loadMoreFailed && <p className="catalog-search__end">{t("campaigns.endOfList")}</p>}
      </>}
    </section>
    {canCreate && !isDefaultEmpty && !(isEmptyResult && hasFilterOrQuery) && <FloatingActionButton ariaLabel={t("campaigns.createAria")} onClick={() => setCreateOpen(true)}><Icon name="plus" /></FloatingActionButton>}
    <CampaignFiltersSheet categories={categories} error={filterError} filters={draftFilters} onApply={applyFilters} onClose={closeFilters} onReset={resetCatalog} onSetFilter={setDraft} open={filtersOpen} />
    <Modal id="campaign-create-sheet" onClose={() => setCreateOpen(false)} open={createOpen} title={t("campaigns.newTitle")}><div className="campaign-create-form grid gap-3"><Input className="campaign-create-form__input" label={t("campaigns.name")} maxLength={150} onChange={updateForm("title")} placeholder={t("campaigns.namePlaceholder")} required value={form.title} /><Textarea className="campaign-create-form__textarea" label={t("campaigns.description")} maxLength={1000} onChange={updateForm("description")} placeholder={t("campaigns.descriptionPlaceholder")} required value={form.description} /><CategoryMultiSelect onChange={(categories) => setForm((current) => ({ ...current, categories }))} required value={form.categories} /><Textarea className="campaign-create-form__textarea" label={t("campaigns.requirements")} maxLength={1000} onChange={updateForm("requirements")} placeholder={t("campaigns.requirementsPlaceholder")} value={form.requirements} /><RegionSelect onChange={(event) => setForm((current) => ({ ...current, city: event.target.value }))} value={form.city} /><Input className="campaign-create-form__input" label={t("campaigns.deadline")} onChange={updateForm("deadline")} type="date" value={form.deadline} /><div className="campaign-create-form__budget-grid"><Input className="campaign-create-form__input" inputMode="numeric" label={t("campaigns.budgetFrom")} onChange={(event) => setForm((current) => ({ ...current, budgetFrom: formatNumericInput(event.target.value) }))} placeholder="200 000" value={form.budgetFrom} /><Input className="campaign-create-form__input" inputMode="numeric" label={t("campaigns.budgetTo")} onChange={(event) => setForm((current) => ({ ...current, budgetTo: formatNumericInput(event.target.value) }))} placeholder="1 000 000" value={form.budgetTo} /></div><Button aria-busy={saving} className="campaign-create-form__submit" disabled={saving || !form.title.trim() || !form.description.trim() || !form.categories.length} onClick={create} type="button">{saving ? t("campaigns.publishing") : t("campaigns.publish")}</Button></div></Modal>
    <Toast message={toast} tone={toastTone} />
    <BottomNav />
  </div>;
}

function CampaignFiltersSheet({ open, onClose, filters, categories, error, onSetFilter, onApply, onReset }: { open: boolean; onClose: () => void; filters: CampaignCatalogQuery; categories: string[]; error: string | null; onSetFilter: (key: CampaignFilterKey, value: string | number | undefined) => void; onApply: () => void; onReset: () => void }) {
  const { t } = useI18n();
  const [budgetInputs, setBudgetInputs] = useState({ minBudget: "", maxBudget: "" });
  const wasOpenRef = useRef(false);
  useEffect(() => {
    if (open && !wasOpenRef.current) setBudgetInputs({ minBudget: budgetValue(filters.minBudget), maxBudget: budgetValue(filters.maxBudget) });
    wasOpenRef.current = open;
  }, [filters.maxBudget, filters.minBudget, open]);

  const budgetInputError = Object.values(budgetInputs).some((value) => value !== "" && !/^\d[\d\s]*$/.test(value)) ? t("campaigns.budgetInvalid") : null;
  const updateBudget = (key: "minBudget" | "maxBudget") => (event: ChangeEvent<HTMLInputElement>) => {
    const raw = event.target.value;
    const digits = raw.replace(/\s/g, "");
    const isValid = digits === "" || /^\d+$/.test(digits);
    setBudgetInputs((current) => ({ ...current, [key]: isValid ? formatNumericInput(digits) : raw }));
    onSetFilter(key, digits === "" || !isValid ? undefined : Number(digits));
  };
  return <BottomSheet id={filterSheetId} onClose={onClose} open={open} title={t("campaigns.filtersTitle")} variant="neutral"><div className="catalog-search__sheet-content campaign-catalog__sheet-content">
    <FilterSelect label={t("common.categories")} onChange={(value) => onSetFilter("category", value)} options={[["", t("common.all")], ...categories.map((category) => [category, safeCategoryLabel(category, t)])]} value={filters.category ?? ""} />
    <FilterSelect label={t("common.city")} onChange={(value) => onSetFilter("city", value)} options={[["", t("common.any")], ...uzbekistanRegions.map((city) => [city, cityLabel(city)])]} value={filters.city ?? ""} />
    <div className="campaign-catalog__budget-grid">
      <CurrencyFilterInput label={t("campaigns.minBudget")} onChange={updateBudget("minBudget")} value={budgetInputs.minBudget} currency={t("currency.uzs")} />
      <CurrencyFilterInput label={t("campaigns.maxBudget")} onChange={updateBudget("maxBudget")} value={budgetInputs.maxBudget} currency={t("currency.uzs")} />
    </div>
    <div className="campaign-catalog__date-grid">
      <DateFilterInput label={t("campaigns.deadlineFrom")} onChange={(event) => onSetFilter("deadlineFrom", event.target.value || undefined)} onClear={() => onSetFilter("deadlineFrom", undefined)} value={filters.deadlineFrom ?? ""} />
      <DateFilterInput label={t("campaigns.deadlineTo")} onChange={(event) => onSetFilter("deadlineTo", event.target.value || undefined)} onClear={() => onSetFilter("deadlineTo", undefined)} value={filters.deadlineTo ?? ""} />
    </div>
    {(error || budgetInputError) && <p className="campaign-catalog__filter-error" role="alert">{budgetInputError ?? error}</p>}
    <div className="catalog-search__sheet-actions"><button className="catalog-search__secondary-button" onClick={onReset} type="button">{t("common.reset")}</button><button className="catalog-search__primary-button" disabled={Boolean(error || budgetInputError)} onClick={onApply} type="button">{t("common.apply")}</button></div>
  </div></BottomSheet>;
}

function budgetValue(value: number | undefined) {
  return value == null ? "" : formatNumericInput(String(value));
}

function CurrencyFilterInput({ label, value, currency, onChange }: { label: string; value: string; currency: string; onChange: (event: ChangeEvent<HTMLInputElement>) => void }) {
  const { t } = useI18n();
  return <label className="catalog-search__filter-select campaign-catalog__currency-field"><span>{label}</span><span><input aria-label={label} inputMode="numeric" onChange={onChange} placeholder={t("common.any")} value={value} /><b aria-hidden="true">{currency}</b></span></label>;
}

function DateFilterInput({ label, value, onChange, onClear }: { label: string; value: string; onChange: (event: ChangeEvent<HTMLInputElement>) => void; onClear: () => void }) {
  const { t } = useI18n();
  return <label className="catalog-search__filter-select campaign-catalog__date-field"><span>{label}</span><span><input aria-label={label} onChange={onChange} type="date" value={value} />{value && <button aria-label={t("campaigns.clearDateAria", { label })} onClick={onClear} type="button"><Icon className="h-4 w-4" name="close" /></button>}</span></label>;
}

function buildActiveChips(filters: CampaignCatalogQuery, language: "ru" | "uz", t: Translate) {
  const chips: Array<{ key: CampaignActiveFilterKey; label: string }> = [];
  if (filters.category) chips.push({ key: "category", label: safeCategoryLabel(filters.category, t) });
  if (filters.city) chips.push({ key: "city", label: cityLabel(filters.city) });
  if (filters.minBudget != null || filters.maxBudget != null) chips.push({ key: "budget", label: formatBudgetChip(filters.minBudget, filters.maxBudget, t) });
  if (filters.deadlineFrom || filters.deadlineTo) chips.push({ key: "deadline", label: formatDeadlineChip(filters.deadlineFrom, filters.deadlineTo, language, t) });
  return chips;
}

function formatBudgetChip(minBudget: number | undefined, maxBudget: number | undefined, t: Translate) {
  if (minBudget != null && maxBudget != null) return t("campaigns.budgetRange", { min: formatNumericInput(String(minBudget)), max: formatNumericInput(String(maxBudget)) });
  if (minBudget != null) return t("campaigns.budgetFromValue", { min: formatNumericInput(String(minBudget)) });
  return t("campaigns.budgetToValue", { max: formatNumericInput(String(maxBudget)) });
}

function formatDeadlineChip(deadlineFrom: string | undefined, deadlineTo: string | undefined, language: "ru" | "uz", t: Translate) {
  const format = (value: string) => new Intl.DateTimeFormat(language === "uz" ? "uz-UZ" : "ru-UZ", { day: "numeric", month: "short" }).format(new Date(`${value}T00:00:00`));
  if (deadlineFrom && deadlineTo) return t("campaigns.deadlineRange", { from: format(deadlineFrom), to: format(deadlineTo) });
  if (deadlineFrom) return t("campaigns.deadlineFromValue", { from: format(deadlineFrom) });
  return t("campaigns.deadlineToValue", { to: deadlineTo ? format(deadlineTo) : "" });
}

function getFilterError(filters: CampaignCatalogQuery, t: Translate) {
  if (filters.minBudget != null && filters.maxBudget != null && filters.minBudget > filters.maxBudget) return t("campaigns.budgetRangeInvalid");
  if (filters.deadlineFrom && filters.deadlineTo && filters.deadlineFrom > filters.deadlineTo) return t("campaigns.deadlineRangeInvalid");
  return null;
}

function safeCategoryLabel(value: string, t: Translate) {
  if (isOtherCategory(value)) return value.slice("other:".length).trim() || t("common.notSpecified");
  const label = categoryLabel(value);
  return label.startsWith("taxonomy.category.") ? t("common.notSpecified") : label;
}

function sortOptions(t: Translate): string[][] {
  return [["promoted", t("campaigns.sortPromoted")], ["newest", t("campaigns.sortNewest")], ["deadline_asc", t("campaigns.sortDeadline")], ["budget_asc", t("campaigns.sortBudgetAsc")], ["budget_desc", t("campaigns.sortBudgetDesc")]];
}
