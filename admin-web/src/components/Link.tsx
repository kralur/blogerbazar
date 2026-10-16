import { createContext, useContext, type ReactNode } from "react";

// In-app links: a normal <a href> (opens in a new tab with Ctrl/Cmd), but a plain click changes the page without a reload.
export const NavigateContext = createContext<(path: string) => void>((path) => window.location.assign(path));

export const userPath = (telegramUserId: number) => `/users/${telegramUserId}`;

export function Link({ to, children, className = "font-semibold text-brand-blue underline-offset-4 hover:underline" }: { to: string; children: ReactNode; className?: string }) {
  const navigate = useContext(NavigateContext);
  return (
    <a
      href={to}
      className={className}
      onClick={(event) => {
        if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        navigate(to);
      }}
    >
      {children}
    </a>
  );
}

// A person in a list: their name opens the admin card; without an id the name is plain text.
export function UserLink({ telegramUserId, name }: { telegramUserId?: number | null; name: string }) {
  return telegramUserId ? <Link to={userPath(telegramUserId)}>{name}</Link> : <>{name}</>;
}
