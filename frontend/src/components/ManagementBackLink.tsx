import { useTelegram } from "../telegram/TelegramProvider";
import { Icon } from "./ui";

export function ManagementBackLink({ ariaLabel, href }: { ariaLabel: string; href: string }) {
  // Inside Telegram its own Back button does this. The Telegram script also loads in an ordinary browser (the website,
  // D64 B2), so "inside Telegram" means a launch that brought initData, not just the script's presence.
  const { isTelegram, isEnvironmentReady } = useTelegram();
  if (!isEnvironmentReady || isTelegram) return null;

  return <a aria-label={ariaLabel} className="management-back-link" href={href} onClick={(event) => {
    event.preventDefault();
    if (window.location.hash !== href) window.location.hash = href.slice(1);
  }}><Icon name="back" /></a>;
}
