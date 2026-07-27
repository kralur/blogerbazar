import { useEffect, useState } from "react";
import { createContactUnlockInvoice, createContactUnlockOrder, getBlogger, getBloggerReviews, getContactUnlockStatus, getUnlockedContact, type BloggerDetails, type BloggerReview } from "../api/marketplace";
import { openTelegramInvoice } from "../api/client";
import { Avatar, Badge, BottomNav, Button, Card, Icon, Rating, StatsCard, Toast } from "../components/ui";
import { formatCurrency } from "../lib/currency";

type Contact = { phone?: string | null; email?: string | null };

export function BloggerDetails({ id }: { id: string }) {
  const [blogger, setBlogger] = useState<BloggerDetails | null>(null);
  const [contact, setContact] = useState<Contact | null>(null);
  const [reviews, setReviews] = useState<BloggerReview[]>([]);
  const [unlocking, setUnlocking] = useState(false);
  const [toast, setToast] = useState("");

  useEffect(() => {
    getBlogger(id).then(setBlogger).catch(() => setBlogger(null));
  }, [id]);

  useEffect(() => {
    getBloggerReviews(id).then(setReviews).catch(() => undefined);
  }, [id]);

  useEffect(() => {
    getContactUnlockStatus("Blogger", id)
      .then((status) => status.isUnlocked ? getUnlockedContact("Blogger", id).then(setContact) : undefined)
      .catch(() => undefined);
  }, [id]);

  const unlockContact = async () => {
    try {
      setUnlocking(true);
      const order = await createContactUnlockOrder("Blogger", id);
      if (order.isUnlocked) {
        setContact(await getUnlockedContact("Blogger", id));
        setToast("Контакты разблокированы");
        return;
      }

      const invoice = await createContactUnlockInvoice(order.reference);
      const status = await openTelegramInvoice(invoice.invoiceLink);
      if (status === "paid") {
        setToast("Платёж принят. Контакты откроются после подтверждения.");
        window.setTimeout(() => {
          getContactUnlockStatus("Blogger", id)
            .then((result) => result.isUnlocked ? getUnlockedContact("Blogger", id).then(setContact) : undefined)
            .catch(() => undefined);
        }, 1200);
      } else if (status === "pending") {
        setToast("Платёж обрабатывается.");
      } else {
        setToast("Оплата не завершена.");
      }
    } catch {
      setToast("Не удалось создать счёт");
    } finally {
      setUnlocking(false);
    }
  };

  if (!blogger) return <div className="screen"><Card>Загружаем профиль…</Card></div>;

  const contactLine = contact?.phone ?? contact?.email;
  const portfolio = blogger.portfolioItems;
  return <div className="screen pb-36">
    <div className="relative -mx-5 h-48 overflow-hidden bg-gradient-to-br from-violet-200 via-blue-100 to-cyan-100">
      {blogger.coverUrl && <img alt="" className="h-full w-full object-cover opacity-35" src={blogger.coverUrl} />}
      <div className="absolute inset-0 bg-gradient-to-b from-white/20 to-white/80" /><a className="absolute left-4 top-4 grid h-11 w-11 place-items-center rounded-full bg-white/85 shadow-card" href="#/search"><Icon name="back" /></a>
    </div>
    <div className="relative -mt-16 text-center"><div className="mx-auto w-fit"><Avatar name={blogger.name} size="xl" src={blogger.avatarUrl} verified={blogger.verified} /></div><h1 className="mt-3 text-2xl font-extrabold tracking-tight">{blogger.name}</h1><p className="mt-1 text-sm text-brand-muted">{blogger.categories.join(" · ")} · {blogger.city}</p><div className="mt-2"><Rating count={blogger.reviewsCount} value={blogger.rating} /> <span className="text-sm text-brand-muted">· {blogger.completedDealsCount} сделок</span></div></div>
    <div className="mt-5 grid grid-cols-3 gap-2"><StatsCard label="Подписчики" value={`${Math.round(blogger.totalFollowers / 1000)}K`} /><StatsCard label="ER" value={`${blogger.engagementRate}%`} /><StatsCard label="Сделок" value={String(blogger.completedDealsCount)} /></div>
    <Card className="mt-5"><h2 className="font-extrabold">О блогере</h2><p className="mt-2 text-sm leading-6 text-brand-muted">{blogger.bio ?? "Профиль проходит наполнение."}</p><div className="mt-3 flex flex-wrap gap-2">{blogger.barterEnabled && <Badge tone="green">Готов(а) к бартеру</Badge>}{blogger.verified && <Badge tone="blue">Проверенный профиль</Badge>}</div></Card>
    {portfolio.length > 0 && <section className="mt-5"><h2 className="mb-3 font-extrabold">Портфолио</h2><div className="no-scrollbar -mx-5 flex gap-3 overflow-x-auto px-5">{portfolio.map((item) => <a className="relative h-28 w-24 shrink-0 overflow-hidden rounded-2xl bg-slate-100" href={item.url} key={item.id} rel="noreferrer" target="_blank"><img alt={item.title} className="h-full w-full object-cover" src={item.url} />{item.type === "VIDEO" && <span className="absolute inset-0 grid place-items-center bg-slate-950/30 text-white">▶</span>}</a>)}</div></section>}
    <section className="mt-5"><h2 className="mb-3 font-extrabold">Отзывы</h2>{reviews.length ? <div className="grid gap-2">{reviews.map((review) => <Card className="p-3" key={review.id}><div className="flex items-center justify-between"><Rating value={review.rating} /><span className="text-xs text-brand-muted">{new Intl.DateTimeFormat("ru-RU", { day: "numeric", month: "short", year: "numeric" }).format(new Date(review.createdAtUtc))}</span></div>{review.reviewerName && <p className="mt-2 text-sm font-bold">{review.reviewerName}</p>}{review.comment && <p className="mt-1 text-sm leading-5 text-brand-muted">{review.comment}</p>}</Card>)}</div> : <Card><p className="text-sm text-brand-muted">Отзывов пока нет. Они появятся после завершённых сделок.</p></Card>}</section>
    <section className="mt-5"><h2 className="mb-3 font-extrabold">Стоимость рекламы</h2><div className="grid grid-cols-2 gap-2">{[["Stories", blogger.storiesPrice], ["Reels", blogger.reelsPrice], ["Пост", blogger.postPrice], ["Интеграция", blogger.integrationPrice]].map(([label, value]) => <Card className="p-3" key={String(label)}><p className="text-xs text-brand-muted">{label}</p><p className="mt-1 text-sm font-extrabold">{formatCurrency(Number(value))}</p></Card>)}</div></section>
    <section className="mt-5"><h2 className="mb-3 font-extrabold">Контакты</h2>{contactLine ? <Card><p className="text-sm font-bold">{contactLine}</p>{contact?.phone && contact.email && <p className="mt-1 text-sm text-brand-muted">{contact.email}</p>}</Card> : <Card className="border-blue-100 bg-gradient-to-br from-blue-50 via-white to-cyan-50"><div className="flex gap-3"><span className="grid h-12 w-12 place-items-center rounded-2xl bg-blue-100 text-brand-blue"><Icon name="lock" /></span><div><p className="font-extrabold">Контакты скрыты</p><p className="mt-1 text-sm text-brand-muted">Разблокируйте, чтобы связаться напрямую</p></div></div><Button className="mt-4 w-full" disabled={unlocking} onClick={unlockContact}>{unlocking ? "Создаём счёт…" : "Разблокировать контакты"}</Button></Card>}</section>
    <div className="fixed inset-x-0 bottom-[70px] z-30 mx-auto max-w-[430px] bg-white/90 px-5 pb-3 pt-2 backdrop-blur"><a href="#/campaigns"><Button className="w-full"><Icon name="send" />Создать рекламную кампанию</Button></a></div>
    <Toast message={toast} /><BottomNav />
  </div>;
}
