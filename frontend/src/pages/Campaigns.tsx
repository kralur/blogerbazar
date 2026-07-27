import { useEffect, useState } from "react";
import { createCampaign, getCampaigns } from "../api/marketplace";
import { CampaignCard, type CampaignCardData } from "../components/CampaignCard";
import { BottomNav, Button, Chip, Icon, Input, Modal, SearchBar, Textarea, Toast } from "../components/ui";
const initial = { title: "", description: "", city: "Ташкент", categories: "Lifestyle", budgetFrom: "500000", budgetTo: "1500000" };

export function Campaigns() {
  const [campaigns, setCampaigns] = useState<CampaignCardData[]>([]);
  const [form, setForm] = useState(initial);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [toast, setToast] = useState("");

  useEffect(() => { getCampaigns().then(setCampaigns).catch(() => undefined); }, []);
  const update = (key: keyof typeof initial) => (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => setForm((previous) => ({ ...previous, [key]: event.target.value }));

  const create = async () => {
    try {
      setSaving(true);
      await createCampaign({ title: form.title, description: form.description, city: form.city || undefined, categories: form.categories.split(",").map((item) => item.trim()).filter(Boolean), budgetFrom: Number(form.budgetFrom) || undefined, budgetTo: Number(form.budgetTo) || undefined, publishImmediately: true });
      setOpen(false);
      setForm(initial);
      setToast("Кампания опубликована и видна блогерам");
      const updated = await getCampaigns();
      setCampaigns(updated);
    } catch (error) {
      setToast(error instanceof Error ? error.message : "Не удалось создать кампанию");
    } finally {
      setSaving(false);
    }
  };

  return <div className="screen pb-28">
    <header className="flex items-center justify-between pt-2"><div><p className="text-sm font-semibold text-brand-muted">Для блогеров</p><h1 className="text-3xl font-extrabold tracking-tight">Реклама</h1></div><span className="grid h-11 w-11 place-items-center rounded-2xl bg-gradient-to-br from-amber-100 to-orange-100 text-amber-600">✦</span></header>
    <div className="mt-4"><SearchBar placeholder="Поиск рекламных кампаний…" /></div><div className="no-scrollbar -mx-5 mt-3 flex gap-2 overflow-x-auto px-5"><Chip active>Все</Chip><Chip>Красота</Chip><Chip>Еда</Chip><Chip>Технологии</Chip></div>
    <div className="mt-5 grid gap-3">{campaigns.map((campaign) => <CampaignCard campaign={campaign} key={campaign.id} />)}</div>
    <button aria-label="Создать кампанию" className="fixed bottom-24 right-5 z-30 grid h-14 w-14 place-items-center rounded-full bg-brand-gradient text-white shadow-glow" onClick={() => setOpen(true)} type="button"><Icon name="plus" /></button>
    <Modal onClose={() => setOpen(false)} open={open} title="Новая кампания"><div className="grid gap-3"><Input label="Название" onChange={update("title")} placeholder="Запуск новой линейки" required value={form.title} /><Textarea label="Описание" onChange={update("description")} placeholder="Что нужно рассказать блогерам?" value={form.description} /><Input label="Категории через запятую" onChange={update("categories")} value={form.categories} /><Input label="Город" onChange={update("city")} value={form.city} /><div className="grid grid-cols-2 gap-3"><Input label="Бюджет от, сум" min="0" onChange={update("budgetFrom")} type="number" value={form.budgetFrom} /><Input label="Бюджет до, сум" min="0" onChange={update("budgetTo")} type="number" value={form.budgetTo} /></div><Button disabled={saving || !form.title.trim() || !form.description.trim()} onClick={create}>{saving ? "Публикуем…" : "Опубликовать кампанию"}</Button><a className="text-center text-sm font-bold text-brand-blue" href="#/business">Сначала создайте профиль бизнеса</a></div></Modal>
    <Toast message={toast} /><BottomNav />
  </div>;
}
