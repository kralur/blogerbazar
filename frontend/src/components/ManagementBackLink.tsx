import { useTelegram } from "../telegram/TelegramProvider";
import { Icon } from "./ui";

export function ManagementBackLink({ ariaLabel, href }: { ariaLabel: string; href: string }) {
  const { isEmbedded, isEnvironmentReady } = useTelegram();
  if (!isEnvironmentReady || isEmbedded) return null;

  return <a aria-label={ariaLabel} className="management-back-link" href={href} onClick={(event) => {
    event.preventDefault();
    if (window.location.hash !== href) window.location.hash = href.slice(1);
  }}><Icon name="back" /></a>;
}
