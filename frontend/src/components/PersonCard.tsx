import type { ReactNode } from "react";
import { Avatar } from "./ui";

// One card for a person in every list of the Mini App: bloggers and brand faces, on Home, in Search and in Saved.
// Photo, name, the facts, the price on the right and the save button; the tags go under them across the card, so
// three of them fit in a line on a phone. What the API did not send is left out, never shown as a dash or a zero.
export function PersonCard({ href, ariaLabel, name, avatarUrl, facts, tags, promotedLabel, priceLabel, price, priceUnit, save, priority = false }: {
  href: string;
  ariaLabel: string;
  name: string;
  avatarUrl?: string | null;
  facts: string;
  tags: string[];
  promotedLabel?: string | null;
  priceLabel: string;
  price?: string | null;
  priceUnit: string;
  save: ReactNode;
  priority?: boolean;
}) {
  return <article className="person-card card-enter">
    <a aria-label={ariaLabel} className="person-card__link" href={href}>
      <Avatar name={name} priority={priority} size="sm" src={avatarUrl} variant="catalog" />
      <div className="person-card__body">
        <div className="person-card__name-row"><strong>{name}</strong></div>
        {facts && <p className="person-card__facts">{facts}</p>}
      </div>
      {price && <div className="person-card__price"><span>{priceLabel}</span><strong>{price}</strong><span>{priceUnit}</span></div>}
      {(promotedLabel || tags.length > 0) && <div className="person-card__tags">
        {promotedLabel && <span className="catalog-card__promoted">{promotedLabel}</span>}
        {tags.map((tag) => <span key={tag}>{tag}</span>)}
      </div>}
    </a>
    {save}
  </article>;
}
