import { useEffect, useRef, useState, type ReactNode } from "react";
import type { AdminWebSession } from "../api/session";
import { useActions } from "../components/Actions";
import { BrandMark, Badge, Button } from "../components/ui";
import { Preferences } from "../components/Preferences";
import { useI18n } from "../i18n";
import { formatTashkentTime } from "../lib/time";
import { navigation, type NavItem } from "./navigation";

type ShellProps = {
  session: AdminWebSession;
  activeId: string;
  title: string;
  onNavigate: (path: string) => void;
  onSignOut: () => void;
  onSignOutAll: () => Promise<void>;
  signingOut: boolean;
  children: ReactNode;
};

// Desktop first: a fixed side menu from 1024px; below that the menu opens over the page, so a phone never
// scrolls sideways.
export function Shell({ session, activeId, title, onNavigate, onSignOut, onSignOutAll, signingOut, children }: ShellProps) {
  const { t } = useI18n();
  const signOutAll = useActions(() => undefined);
  const [menuOpen, setMenuOpen] = useState(false);
  const menuButton = useRef<HTMLButtonElement>(null);
  const menu = useRef<HTMLElement>(null);

  useEffect(() => {
    if (!menuOpen) return;
    menu.current?.querySelector<HTMLElement>("a[href]")?.focus();
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setMenuOpen(false);
        menuButton.current?.focus();
      }
    };
    document.addEventListener("keydown", closeOnEscape);
    return () => document.removeEventListener("keydown", closeOnEscape);
  }, [menuOpen]);

  const navigate = (path: string) => {
    setMenuOpen(false);
    onNavigate(path);
  };
  const role = session.role === "owner" ? t("role.owner") : t("role.admin");

  return (
    <div className="min-h-screen bg-brand-bg text-brand-ink lg:flex">
      {menuOpen && <div className="fixed inset-0 z-30 bg-[color:var(--bb-overlay)] lg:hidden" aria-hidden="true" onClick={() => setMenuOpen(false)} />}
      <aside
        ref={menu}
        id="admin-menu"
        // Closed on a narrow screen the menu is also invisible, so keyboard focus and screen readers skip it.
        className={`fixed inset-y-0 left-0 z-40 flex w-72 max-w-[85vw] flex-col border-r border-brand-line bg-brand-surface transition-[transform,visibility] motion-reduce:transition-none lg:visible lg:sticky lg:top-0 lg:h-screen lg:w-64 lg:translate-x-0 ${menuOpen ? "visible translate-x-0" : "invisible -translate-x-full"}`}
      >
        {/* The logo goes back to the overview, like on most sites. */}
        <a
          href="/"
          aria-label={t("nav.home")}
          className="mx-2 mt-2 flex items-center gap-3 rounded-[var(--bb-radius-control)] px-3 py-3 hover:bg-brand-soft"
          onClick={(event) => {
            if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            navigate("/");
          }}
        >
          <BrandMark />
          <div className="min-w-0">
            <p className="truncate text-base font-extrabold">{t("app.name")}</p>
            <p className="truncate text-xs text-brand-muted">{t("app.admin")}</p>
          </div>
        </a>
        <nav aria-label={t("nav.label")} className="flex-1 overflow-y-auto px-3 pb-6">
          <ul className="space-y-1">
            {navigation.map((item) => <NavEntry key={item.id} item={item} activeId={activeId} onNavigate={navigate} />)}
          </ul>
        </nav>
        <div className="space-y-3 border-t border-brand-line px-5 py-4">
          <div className="min-w-0">
            <p className="break-words text-sm font-semibold">{session.firstName}</p>
            <p className="text-xs text-brand-muted">{role}</p>
            <p className="text-xs text-brand-muted">{t("header.sessionUntil", { time: formatTashkentTime(session.expiresAtUtc) })}</p>
          </div>
          <Preferences />
          <Button className="w-full" onClick={onSignOut} disabled={signingOut}>{signingOut ? t("header.signingOut") : t("header.signOut")}</Button>
          <Button
            variant="dangerText"
            className="w-full"
            disabled={signingOut}
            onClick={() => signOutAll.ask({
              title: t("header.signOutAllTitle"),
              text: t("header.signOutAllText"),
              confirmLabel: t("header.signOutAll"),
              danger: true,
              run: onSignOutAll,
              done: t("header.signingOut")
            })}
          >
            {t("header.signOutAll")}
          </Button>
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 border-b border-brand-line bg-brand-bg">
          <div className="mx-auto flex w-full max-w-6xl items-center gap-3 px-4 py-3 sm:px-6 lg:py-5">
            <button
              ref={menuButton}
              type="button"
              className="inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-[var(--bb-radius-control)] border border-brand-line bg-brand-surface text-lg lg:hidden"
              aria-label={menuOpen ? t("nav.closeMenu") : t("nav.openMenu")}
              aria-expanded={menuOpen}
              aria-controls="admin-menu"
              onClick={() => setMenuOpen((open) => !open)}
            >
              <span aria-hidden="true">☰</span>
            </button>
            <h1 className="min-w-0 flex-1 truncate text-xl font-extrabold sm:text-2xl">{title}</h1>
          </div>
        </header>
        <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 sm:px-6 sm:py-8">{children}</main>
      </div>
      {signOutAll.dialog}
    </div>
  );
}

function NavEntry({ item, activeId, onNavigate, nested = false }: { item: NavItem; activeId: string; onNavigate: (path: string) => void; nested?: boolean }) {
  const { t } = useI18n();
  const base = `flex w-full min-h-[44px] items-center justify-between gap-2 rounded-[var(--bb-radius-control)] px-3 text-left text-sm ${nested ? "pl-6" : ""}`;
  if (item.children) {
    return (
      <li>
        <p className="px-3 pb-1 pt-4 text-xs font-bold uppercase text-brand-muted">{t(item.labelKey)}</p>
        <ul className="space-y-1">
          {item.children.map((child) => <NavEntry key={child.id} item={child} activeId={activeId} onNavigate={onNavigate} nested />)}
        </ul>
      </li>
    );
  }

  if (!item.path) {
    return (
      <li>
        <span aria-disabled="true" className={`${base} cursor-default text-brand-muted`}>
          <span className="truncate">{t(item.labelKey)}</span>
          <Badge>{t("nav.soon")}</Badge>
        </span>
      </li>
    );
  }

  const active = item.id === activeId;
  const path = item.path;
  return (
    <li>
      <a
        href={path}
        aria-current={active ? "page" : undefined}
        className={`${base} font-semibold ${active ? "bg-brand-accent text-[color:var(--bb-accent-text)]" : "text-brand-ink hover:bg-brand-soft"}`}
        onClick={(event) => {
          event.preventDefault();
          onNavigate(path);
        }}
      >
        <span className="truncate">{t(item.labelKey)}</span>
      </a>
    </li>
  );
}
