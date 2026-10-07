import { Avatar, Icon } from "./ui";

// The other side of an offer, deal or application; a link to their profile when it is known.
export function CounterpartyRow({ name, imageUrl, label, href }: { name: string; imageUrl?: string | null; label: string; href?: string | null }) {
  const content = <><Avatar name={name} size="sm" src={imageUrl} variant="catalog" /><span className="request-row__body"><span className="request-row__meta">{label}</span><strong className="truncate">{name}</strong></span>{href && <Icon className="home-activity__chevron" name="back" />}</>;
  return href
    ? <a className="request-row mt-4" href={href}>{content}</a>
    : <div className="request-row request-row--static mt-4">{content}</div>;
}
