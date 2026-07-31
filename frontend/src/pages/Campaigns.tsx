import { useEffect, useMemo, useState } from "react";
import { getApiErrorMessage } from "../api/client";
import { createCampaign, getCampaigns, getMyBusinessProfile } from "../api/marketplace";
import { CampaignCard, type CampaignCardData } from "../components/CampaignCard";
import { CategoryMultiSelect } from "../components/CategoryMultiSelect";
import { RegionSelect } from "../components/RegionSelect";
import { BottomNav, BottomSheet, Button, Chip, ErrorState, Icon, Input, LoadingState, Modal, NoDataState, SearchBar, Textarea, Toast } from "../components/ui";
import { categoryLabel, useI18n } from "../i18n";
import { formatNumericInput, normalizeNumericInput } from "../lib/currency";

const initial = { title: "", description: "", city: "tashkent-city", categories: ["lifestyle"], requirements: "", deadline: "", budgetFrom: "500 000", budgetTo: "1 500 000" };
type CampaignFilters = { city: string; category: string; budget: string; deadline: string; format: string };
const defaultFilters: CampaignFilters = { city: "", category: "", budget: "", deadline: "", format: "" };

export function Campaigns() {
  const { t } = useI18n();
  const [campaigns, setCampaigns] = useState<CampaignCardData[]>([]);
  const [form, setForm] = useState(initial);
  const [filters, setFilters] = useState(defaultFilters);
  const [createOpen, setCreateOpen] = useState(false);
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [query, setQuery] = useState("");
  const [toast, setToast] = useState("");

  const load = () => { setLoading(true); setFailed(false); getCampaigns().then(setCampaigns).catch(() => setFailed(true)).finally(() => setLoading(false)); };
  useEffect(load, []);
  const visible = useMemo(() => campaigns.filter((campaign) => {
    const text = `${campaign.title} ${campaign.description} ${campaign.requirements?.join(" ") ?? ""} ${campaign.business?.name ?? ""}`.toLocaleLowerCase();
    const deadlineDays = campaign.deadline ? Math.ceil((new Date(campaign.deadline).getTime() - Date.now()) / 86_400_000) : null;
    const maxBudget = filters.budget ? Number(filters.budget) : null;
    return (!filters.city || campaign.city === filters.city)
      && (!filters.category || campaign.categories.includes(filters.category))
      && (!maxBudget || (campaign.budgetFrom ?? 0) <= maxBudget)
      && (!filters.format || text.includes(filters.format))
      && (!filters.deadline || deadlineDays === null || deadlineDays <= Number(filters.deadline))
      && text.includes(query.toLocaleLowerCase());
  }), [campaigns, filters, query]);

  const update = (key: keyof typeof initial) => (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => setForm((previous) => ({ ...previous, [key]: event.target.value }));
  const setFilter = (key: keyof CampaignFilters) => (value: string) => setFilters((current) => ({ ...current, [key]: value }));
  const openCreate = async () => { try { await getMyBusinessProfile(); setCreateOpen(true); } catch { setToast(t("campaigns.businessRequired")); window.setTimeout(() => { window.location.hash = "/business"; }, 800); } };
  const create = async () => {
    if (saving || !form.title.trim() || !form.description.trim() || !form.categories.length) return;
    try {
      setSaving(true);
      await createCampaign({ title: form.title.trim(), description: form.description.trim(), city: form.city || undefined, categories: form.categories, requirements: form.requirements.split(",").map((item) => item.trim()).filter(Boolean), deadline: form.deadline ? new Date(`${form.deadline}T23:59:59`).toISOString() : undefined, budgetFrom: normalizeNumericInput(form.budgetFrom) || undefined, budgetTo: normalizeNumericInput(form.budgetTo) || undefined, publishImmediately: true });
      setCreateOpen(false); setForm(initial); setToast(t("campaigns.created")); load();
    } catch (error) { setToast(getApiErrorMessage(error, t("campaigns.createFailed"))); } finally { setSaving(false); }
  };

  return <div className="screen space-y-4 px-4 pt-5"><header className="flex items-center justify-between"><div><p className="text-sm font-semibold text-brand-muted">{t("campaigns.eyebrow")}</p><h1 className="mt-1 text-3xl font-extrabold tracking-tight">{t("campaigns.title")}</h1></div><button aria-expanded={filtersOpen} aria-label={t("search.filters")} className="grid h-11 w-11 place-items-center rounded-2xl bg-white text-brand-blue shadow-card" onClick={() => setFiltersOpen(true)} type="button"><Icon name="filter" /></button></header><SearchBar onChange={(event) => setQuery(event.target.value)} placeholder={t("campaigns.search")} value={query} /><div className="no-scrollbar -mx-4 flex gap-2 overflow-x-auto px-4 pb-1"><button onClick={() => setFilter("category")("")} type="button"><Chip active={!filters.category}>{t("common.all")}</Chip></button>{["lifestyle", "beauty", "food", "technology", "sport", "travel", "finance", "gaming", "fashion"].map((item) => <button key={item} onClick={() => setFilter("category")(filters.category === item ? "" : item)} type="button"><Chip active={filters.category === item}>{categoryLabel(item)}</Chip></button>)}</div><section className="grid gap-3">{loading ? <LoadingState title={t("campaigns.loading")} /> : failed ? <ErrorState onRetry={load} subtitle={t("campaigns.loadFailedSubtitle")} title={t("campaigns.loadFailedTitle")} /> : visible.length ? visible.map((campaign) => <CampaignCard campaign={campaign} key={campaign.id} />) : <NoDataState icon="briefcase" subtitle={campaigns.length ? t("campaigns.emptySearchSubtitle") : t("campaigns.emptySubtitle")} title={campaigns.length ? t("campaigns.emptySearchTitle") : t("campaigns.emptyTitle")} />}</section><button aria-label={t("campaigns.createAria")} className="fixed bottom-24 right-5 z-30 grid h-14 w-14 place-items-center rounded-full bg-brand-gradient text-white shadow-glow transition active:scale-95" onClick={openCreate} type="button"><Icon name="plus" /></button><BottomSheet onClose={() => setFiltersOpen(false)} open={filtersOpen} title={t("campaigns.filtersTitle")}><div className="grid gap-3"><RegionSelect includeAny onChange={(event) => setFilter("city")(event.target.value)} value={filters.city} /><FilterSelect label={t("common.categories")} onChange={setFilter("category")} options={[["", t("common.any")], ...["lifestyle", "beauty", "food", "technology", "sport", "travel", "finance", "gaming", "fashion"].map((item) => [item, categoryLabel(item)])]} value={filters.category} /><FilterSelect label={t("campaigns.filterBudget")} onChange={setFilter("budget")} options={[["", t("common.any")], ["300000", "300 000"], ["500000", "500 000"], ["1000000", "1 000 000"]]} value={filters.budget} /><FilterSelect label={t("campaigns.filterDeadline")} onChange={setFilter("deadline")} options={[["", t("common.any")], ["7", t("campaigns.deadlineWeek")], ["30", t("campaigns.deadlineMonth")]]} value={filters.deadline} /><FilterSelect label={t("campaigns.filterFormat")} onChange={setFilter("format")} options={[["", t("common.any")], ["stories", "Stories"], ["reels", "Reels"], ["post", t("card.post")], ["integration", t("card.integration")]]} value={filters.format} /><Button className="w-full" onClick={() => setFiltersOpen(false)} type="button">{t("common.apply")}</Button></div></BottomSheet><Modal onClose={() => setCreateOpen(false)} open={createOpen} title={t("campaigns.newTitle")}><div className="grid gap-3"><Input label={t("campaigns.name")} maxLength={150} onChange={update("title")} placeholder={t("campaigns.namePlaceholder")} required value={form.title} /><Textarea label={t("campaigns.description")} maxLength={1000} onChange={update("description")} placeholder={t("campaigns.descriptionPlaceholder")} required value={form.description} /><CategoryMultiSelect onChange={(categories) => setForm((current) => ({ ...current, categories }))} required value={form.categories} /><Textarea label={t("campaigns.requirements")} maxLength={1000} onChange={update("requirements")} placeholder={t("campaigns.requirementsPlaceholder")} value={form.requirements} /><RegionSelect onChange={(event) => setForm((current) => ({ ...current, city: event.target.value }))} value={form.city} /><Input label={t("campaigns.deadline")} onChange={update("deadline")} type="date" value={form.deadline} /><div className="grid grid-cols-2 gap-3"><Input inputMode="numeric" label={t("campaigns.budgetFrom")} onChange={(event) => setForm((current) => ({ ...current, budgetFrom: formatNumericInput(event.target.value) }))} placeholder="200 000" value={form.budgetFrom} /><Input inputMode="numeric" label={t("campaigns.budgetTo")} onChange={(event) => setForm((current) => ({ ...current, budgetTo: formatNumericInput(event.target.value) }))} placeholder="1 000 000" value={form.budgetTo} /></div><Button aria-busy={saving} disabled={saving || !form.title.trim() || !form.description.trim() || !form.categories.length} onClick={create} type="button">{saving ? t("campaigns.publishing") : t("campaigns.publish")}</Button></div></Modal><Toast message={toast} /><BottomNav /></div>;
}

function FilterSelect({ label, value, options, onChange }: { label: string; value: string; options: string[][]; onChange: (value: string) => void }) {
  return <label className="grid gap-2"><span className="text-[13px] font-bold text-brand-muted">{label}</span><select className="h-[52px] rounded-2xl border border-brand-line bg-white px-3 text-sm font-semibold outline-none focus:border-brand-blue focus:ring-4 focus:ring-blue-100" onChange={(event) => onChange(event.target.value)} value={value}>{options.map(([optionValue, text]) => <option key={optionValue} value={optionValue}>{text}</option>)}</select></label>;
}
