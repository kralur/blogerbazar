import { useEffect, useState } from "react";
import { getPendingBloggerProfiles, moderateBloggerProfile, type PendingBloggerProfile } from "../api/marketplace";
import { Avatar, Badge, BottomNav, Button, Card, EmptyState, StatsCard, Toast } from "../components/ui";

export function Admin() {
  const [profiles, setProfiles] = useState<PendingBloggerProfile[]>([]);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState("");

  const load = () => {
    setLoading(true);
    getPendingBloggerProfiles().then(setProfiles).catch((error) => setToast(error instanceof Error ? error.message : "Нет доступа к модерации")).finally(() => setLoading(false));
  };

  useEffect(load, []);
  const moderate = async (id: string, decision: "approve" | "reject") => {
    try {
      await moderateBloggerProfile(id, decision);
      setProfiles((current) => current.filter((profile) => profile.id !== id));
      setToast(decision === "approve" ? "Профиль одобрен" : "Профиль отклонён");
    } catch (error) {
      setToast(error instanceof Error ? error.message : "Не удалось выполнить модерацию");
    }
  };

  return <div className="screen pb-28"><header><p className="text-sm font-semibold text-brand-muted">Внутренняя панель</p><h1 className="text-3xl font-extrabold tracking-tight">Модерация</h1></header><div className="mt-5 grid grid-cols-2 gap-2"><StatsCard label="Ожидают проверки" value={loading ? "—" : String(profiles.length)} /><StatsCard label="Безопасная роль" value="TMA" /></div><Card className="mt-5"><p className="text-sm leading-6 text-brand-muted">Доступ проверяется сервером по Telegram ID из allow-list. Пароли не передаются в Mini App.</p></Card><section className="mt-5"><h2 className="mb-3 font-extrabold">Новые профили</h2>{!loading && !profiles.length ? <EmptyState icon="check" subtitle="Новые анкеты появятся здесь автоматически." title="Очередь пуста" /> : <div className="grid gap-3">{profiles.map((profile) => <Card key={profile.id}><div className="flex gap-3"><Avatar name={profile.name} size="sm" src={profile.avatarUrl} /><div className="min-w-0 flex-1"><h3 className="font-extrabold">{profile.name}</h3><p className="mt-1 text-sm text-brand-muted">{profile.city} · {profile.categories[0] ?? "Без категории"}</p><div className="mt-2"><Badge tone="gold">На проверке</Badge></div></div></div><div className="mt-4 grid grid-cols-2 gap-2"><Button onClick={() => moderate(profile.id, "approve")}>Одобрить</Button><Button onClick={() => moderate(profile.id, "reject")} variant="secondary">Отклонить</Button></div></Card>)}</div>}</section><Toast message={toast} /><BottomNav /></div>;
}
