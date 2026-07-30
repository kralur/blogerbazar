import { useEffect, useMemo, useState } from "react";
import { createCampaign, getCampaigns, getCategories, getMyBusinessProfile } from "../api/marketplace";
import { CampaignCard, type CampaignCardData } from "../components/CampaignCard";
import { BottomNav, Button, Chip, ErrorState, Icon, Input, LoadingState, Modal, NoDataState, SearchBar, Textarea, Toast } from "../components/ui";
import { categoryLabel, useI18n } from "../i18n";

const initial = { title: "", description: "", city: "Tashkent", categories: ["Lifestyle"], requirements: "", deadline: "", budgetFrom: "500000", budgetTo: "1500000" };

export function Campaigns() {
  const { t } = useI18n();
  const [campaigns, setCampaigns] = useState<CampaignCardData[]>([]);
  const [categories, setCategories] = useState<string[]>([]);
  const [form, setForm] = useState(initial);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [query, setQuery] = useState("");
  const [category, setCategory] = useState("");
  const [toast, setToast] = useState("");

  const load = () => {
    setLoading(true);
    setFailed(false);
    getCampaigns().then(setCampaigns).catch(() => setFailed(true)).finally(() => setLoading(false));
  };

  useEffect(load, []);
  useEffect(() => { getCategories().then(setCategories).catch(() => undefined); }, []);

  const visible = useMemo(() => campaigns.filter((campaign) =>
    (!category || campaign.categories.includes(category))
    && `${campaign.title} ${campaign.description} ${campaign.business?.name ?? ""}`.toLowerCase().includes(query.toLowerCase())), [campaigns, category, query]);

  const update = (key: keyof typeof initial) => (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
    setForm((previous) => ({ ...previous, [key]: event.target.value }));

  const toggleCategory = (value: string) => setForm((previous) => ({
    ...previous,
    categories: previous.categories.includes(value)
      ? previous.categories.length === 1 ? previous.categories : previous.categories.filter((item) => item !== value)
      : [...previous.categories, value]
  }));

  const openCreate = async () => {
    try {
      await getMyBusinessProfile();
      setOpen(true);
    } catch {
      setToast(t("campaigns.businessRequired"));
      window.setTimeout(() => { window.location.hash = "/business"; }, 800);
    }
  };

  const create = async () => {
    if (saving) return;
    try {
      setSaving(true);
      await createCampaign({
        title: form.title.trim(),
        description: form.description.trim(),
        city: form.city.trim() || undefined,
        categories: form.categories,
        requirements: form.requirements.split(",").map((item) => item.trim()).filter(Boolean),
        deadline: form.deadline ? new Date(`${form.deadline}T23:59:59`).toISOString() : undefined,
        budgetFrom: Number(form.budgetFrom) || undefined,
        budgetTo: Number(form.budgetTo) || undefined,
        publishImmediately: true
      });
      setOpen(false);
      setForm(initial);
      setToast(t("campaigns.created"));
      load();
    } catch (error) {
      setToast(error instanceof Error ? error.message : t("campaigns.createFailed"));
    } finally {
      setSaving(false);
    }
  };

  return <div className="screen space-y-4 px-4 pt-5">
    <header className="flex items-center justify-between"><div><p className="text-sm font-semibold text-brand-muted">{t("campaigns.eyebrow")}</p><h1 className="mt-1 text-3xl font-extrabold tracking-tight">{t("campaigns.title")}</h1></div><span className="grid h-11 w-11 place-items-center rounded-2xl bg-gradient-to-br from-amber-100 to-orange-100 text-amber-600">✦</span></header>
    <SearchBar onChange={(event) => setQuery(event.target.value)} placeholder={t("campaigns.search")} value={query} />
    <div className="no-scrollbar -mx-4 flex gap-2 overflow-x-auto px-4 pb-1"><button onClick={() => setCategory("")} type="button"><Chip active={!category}>{t("common.all")}</Chip></button>{categories.map((item) => <button key={item} onClick={() => setCategory(item === category ? "" : item)} type="button"><Chip active={category === item}>{categoryLabel(item)}</Chip></button>)}</div>
    <section className="grid gap-3">{loading ? <LoadingState title={t("campaigns.loading")} /> : failed ? <ErrorState onRetry={load} subtitle={t("campaigns.loadFailedSubtitle")} title={t("campaigns.loadFailedTitle")} /> : visible.length ? visible.map((campaign) => <CampaignCard campaign={campaign} key={campaign.id} />) : <NoDataState action={<Button className="w-full" onClick={openCreate} type="button">{t("campaigns.create")}</Button>} icon="briefcase" subtitle={campaigns.length ? t("campaigns.emptySearchSubtitle") : t("campaigns.emptySubtitle")} title={campaigns.length ? t("campaigns.emptySearchTitle") : t("campaigns.emptyTitle")} />}</section>
    <button aria-label={t("campaigns.createAria")} className="fixed bottom-24 right-5 z-30 grid h-14 w-14 place-items-center rounded-full bg-brand-gradient text-white shadow-glow transition active:scale-95" onClick={openCreate} type="button"><Icon name="plus" /></button>
    <Modal onClose={() => setOpen(false)} open={open} title={t("campaigns.newTitle")}><div className="grid gap-3"><Input label={t("campaigns.name")} onChange={update("title")} placeholder={t("campaigns.namePlaceholder")} required value={form.title} /><Textarea label={t("campaigns.description")} onChange={update("description")} placeholder={t("campaigns.descriptionPlaceholder")} value={form.description} /><div><p className="text-sm font-bold">{t("common.categories")}</p><div className="mt-2 flex flex-wrap gap-2">{categories.map((item) => <button key={item} onClick={() => toggleCategory(item)} type="button"><Chip active={form.categories.includes(item)}>{categoryLabel(item)}</Chip></button>)}</div></div><Textarea label={t("campaigns.requirements")} onChange={update("requirements")} placeholder={t("campaigns.requirementsPlaceholder")} value={form.requirements} /><Input label={t("common.city")} onChange={update("city")} value={form.city} /><Input label={t("campaigns.deadline")} onChange={update("deadline")} type="date" value={form.deadline} /><div className="grid grid-cols-2 gap-3"><Input label={t("campaigns.budgetFrom")} min="0" onChange={update("budgetFrom")} type="number" value={form.budgetFrom} /><Input label={t("campaigns.budgetTo")} min="0" onChange={update("budgetTo")} type="number" value={form.budgetTo} /></div><Button disabled={saving || !form.title.trim() || !form.description.trim() || !form.categories.length} onClick={create} type="button">{saving ? t("campaigns.publishing") : t("campaigns.publish")}</Button></div></Modal>
    <Toast message={toast} /><BottomNav />
  </div>;
}
