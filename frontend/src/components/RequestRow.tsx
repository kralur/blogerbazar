import type { ReactNode } from "react";
import { PersonCard } from "./PersonCard";

// One row for applications, offers and deals: who, what, status and when. It is the same card as a person in Search
// (owner, 2026-11-21): the status opens the tag row, the price sits on the right when the row has one.
// A row that waits for the viewer shows that as a marked line instead of the date, so the tab badge has a visible match.
export function RequestRow({ href, onClick, ariaLabel, name, imageUrl, title, meta, status, action, tags, priceLabel, price, priceUnit }: {
  href?: string;
  onClick?: () => void;
  ariaLabel?: string;
  name: string;
  imageUrl?: string | null;
  title?: string | null;
  meta?: string | null;
  status: ReactNode;
  action?: string | null;
  tags?: string[];
  priceLabel?: string;
  price?: string | null;
  priceUnit?: string;
}) {
  return <PersonCard
    action={action}
    ariaLabel={ariaLabel}
    avatarUrl={imageUrl}
    facts={[title, action ? null : meta].filter(Boolean).join(" · ")}
    href={href}
    name={name}
    onClick={onClick}
    price={price}
    priceLabel={priceLabel}
    priceUnit={priceUnit}
    status={status}
    tags={tags}
  />;
}
