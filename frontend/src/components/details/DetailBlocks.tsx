import type { BloggerReview } from "../../api/marketplace";
import { formatRating, formatShortDate } from "../../lib/currency";
import { useI18n } from "../../i18n";
import { Avatar, Card, Rating } from "../ui";

// onSelect turns the tile into a button (a price that opens an offer for that format).
export type Fact = { label: string; value?: string | null; wide?: boolean; onSelect?: () => void; selectLabel?: string };

// Key facts as tiles; facts without a value are skipped instead of showing "0" or "—".
export function FactGrid({ facts, className }: { facts: Fact[]; className?: string }) {
  const visible = facts.filter((fact) => fact.value != null && fact.value !== "");
  if (visible.length === 0) return null;
  // Pairs first, wide facts last; an unpaired fact takes the full row so the grid never leaves a hole.
  const paired = className?.includes("fact-grid--three") ? visible : [
    ...visible.filter((fact) => !fact.wide).map((fact, index, normal) => normal.length % 2 === 1 && index === normal.length - 1 ? { ...fact, wide: true } : fact),
    ...visible.filter((fact) => fact.wide)
  ];
  return <dl className={`fact-grid ${className ?? ""}`.trim()}>{paired.map((fact) => <div className={`fact-grid__item${fact.wide ? " fact-grid__item--wide" : ""}${fact.onSelect ? " fact-grid__item--action" : ""}`} key={fact.label}><dt>{fact.label}</dt><dd>{fact.value}</dd>{fact.onSelect && <button aria-label={fact.selectLabel} className="fact-grid__action" onClick={fact.onSelect} type="button"><span aria-hidden="true">›</span></button>}</div>)}</dl>;
}

export function ChipList({ items, label }: { items: string[]; label?: string }) {
  if (items.length === 0) return null;
  return <ul aria-label={label} className="chip-list">{items.map((item) => <li key={item}>{item}</li>)}</ul>;
}

export function DetailSection({ title, aside, children }: { title: string; aside?: React.ReactNode; children: React.ReactNode }) {
  return <section className="detail-section"><div className="detail-section__heading"><h2>{title}</h2>{aside}</div>{children}</section>;
}

// reviewerRoute links the reviewer to their profile: a business reviews a blogger and the other way round.
export function ReviewList({ reviews, emptyText, reviewerRoute }: { reviews: BloggerReview[]; emptyText: string; reviewerRoute?: (profileId: string) => string }) {
  if (reviews.length === 0) return <Card><p className="text-sm text-brand-muted">{emptyText}</p></Card>;
  return <div className="grid gap-2">{reviews.map((review) => <ReviewCard key={review.id} review={review} reviewerRoute={reviewerRoute} />)}</div>;
}

function ReviewCard({ review, reviewerRoute, compact = false }: { review: BloggerReview; reviewerRoute?: (profileId: string) => string; compact?: boolean }) {
  const { language, t } = useI18n();
  return <Card className={`p-3${compact ? " review-card--compact" : ""}`}>
    <div className="flex items-center justify-between"><Rating value={review.rating} /><span className="text-xs text-brand-muted">{formatShortDate(review.createdAtUtc, language, { year: true })}</span></div>
    {review.reviewerDeleted && <p className="review-author review-author--deleted">{t("common.deletedAccount")}</p>}
    {!review.reviewerDeleted && review.reviewerName && (review.reviewerProfileId && reviewerRoute
      ? <a className="review-author" href={reviewerRoute(review.reviewerProfileId)}><Avatar name={review.reviewerName} size="sm" src={review.reviewerImageUrl} variant="catalog" /><span>{review.reviewerName}</span></a>
      : <p className="mt-2 text-sm font-bold">{review.reviewerName}</p>)}
    {review.comment && <p className="review-card__comment mt-1 text-sm leading-5 text-brand-muted">{review.comment}</p>}
  </Card>;
}

export const reviewCarouselLimit = 10;

// The latest reviews side by side, so a popular profile does not turn into an endless page;
// "Show all" opens every review with the full text.
export function ReviewsSection({ title, reviews, count, rating, allHref, emptyText, reviewerRoute }: { title: string; reviews: BloggerReview[]; count: number; rating?: number | null; allHref: string; emptyText: string; reviewerRoute?: (profileId: string) => string }) {
  const { t } = useI18n();
  const shown = reviews.slice(0, reviewCarouselLimit);
  const total = Math.max(count, reviews.length);
  return <section className="detail-section">
    <div className="detail-section__heading">
      <h2 className="reviews-heading"><span className="reviews-heading__title">{title}</span>{total > 0 && <span className="reviews-heading__summary">{rating != null && <><span aria-hidden="true" className="text-brand-warning">★</span> {formatRating(rating)} · </>}{total}</span>}</h2>
      {total > 0 && <a className="reviews-heading__all" href={allHref}>{t("reviews.showAll")}</a>}
    </div>
    {shown.length === 0
      ? <Card><p className="text-sm text-brand-muted">{emptyText}</p></Card>
      : <ul aria-label={title} className="review-carousel no-scrollbar">{shown.map((review) => <li className="review-carousel__item" key={review.id}><ReviewCard compact review={review} reviewerRoute={reviewerRoute} /></li>)}</ul>}
  </section>;
}
