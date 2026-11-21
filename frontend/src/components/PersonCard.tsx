import type { ReactNode } from "react";
import { Avatar } from "./ui";

// One card for a person in every list of the Mini App: bloggers and brand faces on Home, in Search and in Saved, and
// the other side of an application, an offer or a deal in Requests.
// Photo, name, the facts, the price on the right and the save button; the tags go under them across the card, so
// three of them fit in a line on a phone. What the API did not send is left out, never shown as a dash or a zero.
// In Requests the tag row starts with the status, and a row that waits for the viewer carries a marked line.
export function PersonCard({ href, onClick, ariaLabel, name, avatarUrl, facts, tags = [], promotedLabel, status, action, priceLabel, price, priceUnit, save, priority = false }: {
  href?: string;
  onClick?: () => void;
  ariaLabel?: string;
  name: string;
  avatarUrl?: string | null;
  facts?: string | null;
  tags?: string[];
  promotedLabel?: string | null;
  status?: ReactNode;
  action?: string | null;
  priceLabel?: string;
  price?: string | null;
  priceUnit?: string;
  save?: ReactNode;
  priority?: boolean;
}) {
  const content = <>
    <Avatar name={name} priority={priority} size="sm" src={avatarUrl} variant="catalog" />
    <span className="person-card__body">
      <span className="person-card__name-row"><strong>{name}</strong></span>
      {facts && <span className="person-card__facts">{facts}</span>}
    </span>
    {price && <span className="person-card__price">{priceLabel && <span>{priceLabel}</span>}<strong>{price}</strong>{priceUnit && <span>{priceUnit}</span>}</span>}
    {(status || action || promotedLabel || tags.length > 0) && <span className="person-card__tags">
      {status}
      {action && <span className="person-card__action"><span aria-hidden="true" className="person-card__action-dot" />{action}</span>}
      {promotedLabel && <span className="person-card__tag catalog-card__promoted">{promotedLabel}</span>}
      {tags.map((tag) => <span className="person-card__tag" key={tag}>{tag}</span>)}
    </span>}
  </>;
  const className = `person-card card-enter${save ? "" : " person-card--plain"}${action ? " person-card--action" : ""}`;
  return <article className={className}>
    {href
      ? <a aria-label={ariaLabel} className="person-card__link" href={href}>{content}</a>
      : <button aria-label={ariaLabel} className="person-card__link" onClick={onClick} type="button">{content}</button>}
    {save}
  </article>;
}
