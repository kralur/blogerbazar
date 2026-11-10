import { Avatar, Icon } from "./ui";

// The other side of an offer, deal or application; a link to their profile when it is known.
// icon: a thing rather than a person (the deal's campaign) shows an icon in place of the photo.
export function CounterpartyRow({ name, imageUrl, label, href, icon }: { name: string; imageUrl?: string | null; label: string; href?: string | null; icon?: string }) {
  const content = <>{icon ? <span aria-hidden="true" className="counterparty-row__icon grid h-11 w-11 shrink-0 place-items-center rounded-full bg-brand-soft text-brand-ink"><Icon name={icon} /></span> : <Avatar name={name} size="sm" src={imageUrl} variant="catalog" />}<span className="request-row__body"><span className="request-row__meta">{label}</span><strong className="truncate">{name}</strong></span>{href && <Icon className="home-activity__chevron" name="back" />}</>;
  return href
    ? <a className="request-row mt-4" href={href}>{content}</a>
    : <div className="request-row request-row--static mt-4">{content}</div>;
}
