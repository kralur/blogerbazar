import { useEffect, useState } from "react";
import { createBusinessProfile, getMyBusinessProfile, updateBusinessProfile } from "../api/marketplace";
import { BottomNav, Button, Card, Icon, Input, Textarea, Toast } from "../components/ui";

const initial = { name: "", username: "", city: "Ташкент", description: "", phone: "", email: "", logoUrl: "" };

export function BusinessProfileForm() {
  const [form, setForm] = useState(initial);
  const [existing, setExisting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState(false);
  const [toast, setToast] = useState("");
  const update = (key: keyof typeof initial) => (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => setForm((previous) => ({ ...previous, [key]: event.target.value }));

  useEffect(() => {
    getMyBusinessProfile().then((profile) => {
      setForm({
        name: profile.name,
        username: profile.username ?? "",
        city: profile.city ?? initial.city,
        description: profile.description ?? "",
        phone: profile.phone ?? "",
        email: profile.email ?? "",
        logoUrl: profile.logoUrl ?? ""
      });
      setExisting(true);
    }).catch(() => undefined);
  }, []);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    try {
      setSaving(true);
      const input = { ...form, username: form.username || undefined, description: form.description || undefined, phone: form.phone || undefined, email: form.email || undefined, logoUrl: form.logoUrl || undefined };
      if (existing) await updateBusinessProfile(input);
      else await createBusinessProfile(input);
      setSuccess(true);
    } catch (error) {
      setToast(error instanceof Error ? error.message : "Не удалось сохранить профиль");
    } finally {
      setSaving(false);
    }
  };

  if (success) return <div className="screen pb-28"><div className="mt-24 text-center"><div className="mx-auto grid h-20 w-20 place-items-center rounded-[28px] bg-brand-gradient text-white shadow-glow"><Icon name="check" /></div><h1 className="mt-6 text-3xl font-extrabold">{existing ? "Профиль обновлён" : "Профиль создан"}</h1><p className="mx-auto mt-3 max-w-xs text-sm leading-6 text-brand-muted">Теперь можно публиковать рекламные кампании и получать отклики блогеров.</p><a className="mt-7 inline-flex" href="#/campaigns"><Button>Создать кампанию</Button></a></div><BottomNav /></div>;

  return <form className="screen pb-28" onSubmit={submit}>
    <header><p className="text-sm font-semibold text-brand-muted">Для бизнеса</p><h1 className="mt-1 text-3xl font-extrabold tracking-tight">{existing ? "Профиль компании" : "Создайте профиль компании"}</h1><p className="mt-2 text-sm leading-5 text-brand-muted">Блогеры увидят бренд, задачу и смогут откликнуться на кампанию.</p></header>
    <Card className="mt-5"><div className="flex gap-3"><span className="grid h-14 w-14 place-items-center rounded-2xl bg-blue-50 text-brand-blue"><Icon name="building" /></span><div><h2 className="font-extrabold">Профиль бренда</h2><p className="mt-1 text-sm text-brand-muted">Контакты будут скрыты до оплаченной разблокировки.</p></div></div></Card>
    <section className="mt-5 grid gap-3"><Input label="Название компании" onChange={update("name")} placeholder="Lumi Beauty" required value={form.name} /><Input label="Username" onChange={update("username")} placeholder="@lumibeauty" value={form.username} /><Input label="Город" onChange={update("city")} required value={form.city} /><Input label="Ссылка на логотип" onChange={update("logoUrl")} placeholder="https://…" type="url" value={form.logoUrl} /><Textarea label="О компании" onChange={update("description")} placeholder="Чем занимается ваш бренд?" value={form.description} /></section>
    <section className="mt-6 grid gap-3"><h2 className="font-extrabold">Контакты</h2><Input label="Телефон" onChange={update("phone")} placeholder="+998901234567" value={form.phone} /><Input label="Email" onChange={update("email")} placeholder="brand@example.uz" type="email" value={form.email} /></section>
    <Button className="mt-6 w-full" disabled={saving} type="submit"><Icon name="check" />{saving ? "Сохраняем…" : existing ? "Сохранить изменения" : "Создать профиль"}</Button><Toast message={toast} /><BottomNav />
  </form>;
}
