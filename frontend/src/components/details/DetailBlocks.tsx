import type { BloggerReview } from "../../api/marketplace";
import { useI18n } from "../../i18n";
import { Card, Rating } from "../ui";

export type Fact = { label: string; value?: string | null; wide?: boolean };

// Key facts as tiles; facts without a value are skipped instead of showing "0" or "—".
export function FactGrid({ facts, className }: { facts: Fact[]; className?: string }) {
  const visible = facts.filter((fact) => fact.value != null && fact.value !== "");
  if (visible.length === 0) return null;
  return <dl className={`fact-grid ${className ?? ""}`.trim()}>{visible.map((fact) => <div className={fact.wide ? "fact-grid__item fact-grid__item--wide" : "fact-grid__item"} key={fact.label}><dt>{fact.label}</dt><dd>{fact.value}</dd></div>)}</dl>;
}

export function ChipList({ items, label }: { items: string[]; label?: string }) {
  if (items.length === 0) return null;
  return <ul aria-label={label} className="chip-list">{items.map((item) => <li key={item}>{item}</li>)}</ul>;
}

export function DetailSection({ title, aside, children }: { title: string; aside?: React.ReactNode; children: React.ReactNode }) {
  return <section className="detail-section"><div className="detail-section__heading"><h2>{title}</h2>{aside}</div>{children}</section>;
}

export function ReviewList({ reviews, emptyText }: { reviews: BloggerReview[]; emptyText: string }) {
  const { language } = useI18n();
  if (reviews.length === 0) return <Card><p className="text-sm text-brand-muted">{emptyText}</p></Card>;
  const formatDate = (value: string) => new Intl.DateTimeFormat(language === "uz" ? "uz-UZ" : "ru-RU", { day: "numeric", month: "short", year: "numeric" }).format(new Date(value));
  return <div className="grid gap-2">{reviews.map((review) => <Card className="p-3" key={review.id}>
    <div className="flex items-center justify-between"><Rating value={review.rating} /><span className="text-xs text-brand-muted">{formatDate(review.createdAtUtc)}</span></div>
    {review.reviewerName && <p className="mt-2 text-sm font-bold">{review.reviewerName}</p>}
    {review.comment && <p className="mt-1 text-sm leading-5 text-brand-muted">{review.comment}</p>}
  </Card>)}</div>;
}
