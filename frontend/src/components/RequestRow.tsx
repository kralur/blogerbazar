import type { ReactNode } from "react";
import { Avatar, Icon } from "./ui";

// One row for applications, offers and deals: who, what, status and when.
export function RequestRow({ href, onClick, ariaLabel, name, imageUrl, title, meta, status }: {
  href?: string;
  onClick?: () => void;
  ariaLabel?: string;
  name: string;
  imageUrl?: string | null;
  title?: string | null;
  meta?: string | null;
  status: ReactNode;
}) {
  const content = <>
    <Avatar name={name} size="sm" src={imageUrl} variant="catalog" />
    <span className="request-row__body">
      <span className="request-row__top"><strong>{name}</strong>{status}</span>
      {title && <span className="request-row__title">{title}</span>}
      {meta && <span className="request-row__meta">{meta}</span>}
    </span>
    <Icon className="request-row__chevron" name="back" />
  </>;
  return href
    ? <a aria-label={ariaLabel} className="request-row" href={href}>{content}</a>
    : <button aria-label={ariaLabel} className="request-row" onClick={onClick} type="button">{content}</button>;
}
