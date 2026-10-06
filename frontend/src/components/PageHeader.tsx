import type { ReactNode } from "react";
import { ManagementBackLink } from "./ManagementBackLink";

// One header for every screen: optional back link (hidden inside Telegram, which shows its own BackButton),
// eyebrow, title and trailing actions. The language switcher lives only in Profile and first-run screens.
export function PageHeader({ title, eyebrow, back, actions, className }: {
  title?: ReactNode;
  eyebrow?: ReactNode;
  back?: { href: string; label: string };
  actions?: ReactNode;
  className?: string;
}) {
  return <header className={`page-header ${className ?? ""}`.trim()}>
    {back && <ManagementBackLink ariaLabel={back.label} href={back.href} />}
    {(title || eyebrow) && <div className="page-header__text">
      {eyebrow && <p className="page-header__eyebrow">{eyebrow}</p>}
      {title && <h1 className="page-header__title">{title}</h1>}
    </div>}
    {actions && <div className="page-header__actions">{actions}</div>}
  </header>;
}
