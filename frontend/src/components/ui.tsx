import { useCallback, useEffect, useId, useRef, useState, type ButtonHTMLAttributes, type ComponentPropsWithoutRef, type InputHTMLAttributes, type ReactNode, type TextareaHTMLAttributes } from "react";
import { createPortal } from "react-dom";
import { useI18n } from "../i18n";
import { formatRating } from "../lib/currency";
import { useTelegram } from "../telegram/TelegramProvider";
import { useRootScreenVisibility } from "../navigation/RootScreenVisibility";
import { requestGuardedNavigation } from "../navigation/guardedNavigation";
import { useVirtualKeyboard } from "../layout/VirtualKeyboardProvider";
import { ActionBadge, useActionCounts } from "../features/actionCounts/ActionCountsProvider";

export function cn(...classes: Array<string | false | null | undefined>) {
  return classes.filter(Boolean).join(" ");
}

export function Icon({ name, className, filled = false }: { name: string; className?: string; filled?: boolean }) {
  const common = "h-5 w-5";
  const icons: Record<string, ReactNode> = {
    search: (
      <path d="m21 21-4.35-4.35m1.35-5.65a7 7 0 1 1-14 0 7 7 0 0 1 14 0Z" />
    ),
    bell: <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9Zm-8.3 13a2.5 2.5 0 0 0 4.6 0" />,
    users: <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2m14-10a4 4 0 1 0 0-8m6 18v-2a4 4 0 0 0-3-3.87M10 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z" />,
    building: <path d="M3 21h18M5 21V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v16M9 7h1m4 0h1M9 11h1m4 0h1M9 15h1m4 0h1" />,
    star: <path d="m12 2 3.1 6.3 6.9 1-5 4.87L18.18 21 12 17.75 5.82 21 7 14.17l-5-4.87 6.9-1L12 2Z" />,
    lock: <path d="M7 11V7a5 5 0 0 1 10 0v4M6 11h12a1 1 0 0 1 1 1v8a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1v-8a1 1 0 0 1 1-1Z" />,
    check: <path d="m20 6-11 11-5-5" />,
    send: <path d="m22 2-7 20-4-9-9-4 20-7Z" />,
    refresh: <path d="M20 11a8 8 0 1 0 2.2 5.5M20 4v7h-7" />,
    filter: <path d="M4 21v-7m0-4V3m8 18v-9m0-4V3m8 18v-5m0-4V3M2 14h4m4-6h4m4 8h4" />,
    phone: <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.8 19.8 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.12 4.18 2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.12.9.33 1.78.62 2.64a2 2 0 0 1-.45 2.11L8.01 9.74a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.86.29 1.74.5 2.64.62A2 2 0 0 1 22 16.92Z" />,
    mail: <path d="M4 4h16a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Zm18 3-10 6L2 7" />,
    link: <path d="M10 13a5 5 0 0 0 7.07.07l2-2a5 5 0 0 0-7.07-7.07l-1.15 1.15M14 11a5 5 0 0 0-7.07-.07l-2 2A5 5 0 0 0 12 20l1.15-1.15" />,
    copy: <path d="M8 8h11a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2V10a2 2 0 0 1 2-2Zm-4 8H3a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v1" />,
    bookmark: <path d="M19 21 12 17 5 21V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v16Z" />,
    heart: <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 1 0-7.78 7.78L12 21.23l8.84-8.84a5.5 5.5 0 0 0 0-7.78Z" />,
    chart: <path d="M3 3v18h18M7 15l4-4 3 3 5-7" />,
    home: <path d="m3 10 9-7 9 7v10a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V10Zm6 11v-7h6v7" />,
    user: <path d="M20 21a8 8 0 1 0-16 0m12-13a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z" />,
    briefcase: <path d="M10 6V5a2 2 0 0 1 2-2h0a2 2 0 0 1 2 2v1m7 4v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-9m18 0H3m18 0-2-4H5l-2 4" />,
    plus: <path d="M12 5v14m-7-7h14" />,
    back: <path d="m15 18-6-6 6-6" />,
    dots: <path d="M12 12h.01M19 12h.01M5 12h.01" />,
    close: <path d="m6 6 12 12M18 6 6 18" />,
    calendar: <path d="M7 3v3m10-3v3M4 9h16M5 5h14a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1Z" />
  };

  return (
    <svg
      className={cn(common, className)}
      fill={filled ? "currentColor" : "none"}
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="2"
      viewBox="0 0 24 24"
    >
      {icons[name] ?? icons.search}
    </svg>
  );
}

export function Button({
  children,
  variant = "primary",
  className,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "secondary" | "ghost" | "danger";
}) {
  return (
    <button
      className={cn(
        "ds-button tap-target inline-flex items-center justify-center gap-2 px-5 transition active:scale-[0.98]",
        "disabled:pointer-events-none disabled:cursor-not-allowed disabled:shadow-none",
        variant === "primary" && "ds-button--primary",
        variant === "secondary" && "ds-button--secondary",
        variant === "ghost" && "ds-button--ghost",
        variant === "danger" && "ds-button--danger",
        className
      )}
      {...props}
    >
      {children}
    </button>
  );
}

export function Card({ children, className, ...props }: ComponentPropsWithoutRef<"section">) {
  return <section className={cn("glass-card p-4", className)} {...props}>{children}</section>;
}

export function Input({ label, error, suffix, className, onInvalid, ...props }: InputHTMLAttributes<HTMLInputElement> & { label?: string; error?: string; suffix?: ReactNode }) {
  const { haptic } = useTelegram();
  const errorId = `${props.id ?? props.name ?? label}-error`;
  const describedBy = [props["aria-describedby"], error ? errorId : undefined].filter(Boolean).join(" ") || undefined;
  return (
    <label className="grid gap-2">
      {label && <span className="ds-field-label">{label}{props.required && <span aria-hidden="true" className="ml-1 text-brand-danger">*</span>}</span>}
      <span className="input-control">
        <input
          {...props}
          className={cn(
            "ds-field w-full px-4 outline-none",
            Boolean(suffix) && "input-control__input--with-suffix",
            error && "border-brand-danger",
            className
          )}
          aria-describedby={describedBy}
          aria-invalid={error ? true : undefined}
          onInvalid={(event) => {
            haptic.error();
            onInvalid?.(event);
          }}
        />
        {suffix && <span aria-hidden="true" className="input-control__suffix">{suffix}</span>}
      </span>
      {error && <span className="ds-field-error" id={errorId}>{error}</span>}
    </label>
  );
}

export function Textarea({ label, error, className, onInvalid, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement> & { label?: string; error?: string }) {
  const { haptic } = useTelegram();
  const length = typeof props.value === "string" ? props.value.length : 0;
  return (
    <label className="grid gap-2">
      {label && <span className="ds-field-label flex items-center justify-between gap-3"><span>{label}{props.required && <span aria-hidden="true" className="ml-1 text-brand-danger">*</span>}</span>{props.maxLength && <span className="font-medium text-brand-muted">{length} / {props.maxLength}</span>}</span>}
      <textarea
        className={cn(
          "ds-field min-h-28 w-full resize-none px-4 py-3 outline-none",
          error && "border-brand-danger",
          className
        )}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${props.id ?? props.name ?? label}-error` : undefined}
        onInvalid={(event) => {
          haptic.error();
          onInvalid?.(event);
        }}
        {...props}
      />
      {error && <span className="ds-field-error" id={`${props.id ?? props.name ?? label}-error`}>{error}</span>}
    </label>
  );
}

export function SearchBar({ placeholder, value, onChange, onClear, clearAriaLabel, className }: { placeholder?: string; value?: string; onChange?: React.ChangeEventHandler<HTMLInputElement>; onClear?: () => void; clearAriaLabel?: string; className?: string }) {
  const { t } = useI18n();
  return (
    <div className={cn("ds-field flex items-center gap-3 px-4", className)}>
      <Icon className="text-brand-muted" name="search" />
      <input aria-label={placeholder ?? t("search.placeholder")} className="min-w-0 w-full bg-transparent text-[15px] text-brand-ink outline-none placeholder:text-brand-muted" onChange={onChange} placeholder={placeholder ?? t("search.placeholder")} value={value} />
      {value && onClear && <button aria-label={clearAriaLabel ?? t("ui.close")} className="search-bar__clear" onClick={onClear} type="button"><Icon className="h-4 w-4" name="close" /></button>}
    </div>
  );
}

export function Chip({ children, active = false }: { children: ReactNode; active?: boolean }) {
  return (
    <span
      className={cn(
        "marketplace-chip",
        active && "marketplace-chip--active"
      )}
    >
      {children}
    </span>
  );
}

export function Badge({ children, tone = "blue" }: { children: ReactNode; tone?: "blue" | "purple" | "gold" | "green" | "gray" | "orange" | "red" }) {
  return (
    <span
      className={cn(
        "ds-badge inline-flex items-center gap-1 whitespace-nowrap px-2.5 py-1",
        tone === "blue" && "ds-badge--info",
        tone === "purple" && "ds-badge--premium",
        (tone === "gold" || tone === "orange") && "ds-badge--warning",
        tone === "green" && "ds-badge--success",
        tone === "gray" && "ds-badge--neutral",
        tone === "red" && "ds-badge--danger"
      )}
    >
      {children}
    </span>
  );
}

export function Avatar({ src, name, size = "md", verified = false, variant = "default" }: { src?: string | null; name: string; size?: "sm" | "md" | "lg" | "xl"; verified?: boolean; variant?: "default" | "catalog" | "neutral" }) {
  const [imageFailed, setImageFailed] = useState(false);
  useEffect(() => setImageFailed(false), [src]);
  const sizes = { sm: "h-12 w-12", md: "h-16 w-16", lg: "h-24 w-24", xl: "h-32 w-32" };
  return (
    <div className={cn("relative shrink-0", variant === "catalog" && "catalog-avatar")}>
      <div className={cn("overflow-hidden rounded-full", variant === "neutral" ? "border border-[color:var(--bb-border)] bg-[color:var(--bb-surface-secondary)] text-[color:var(--bb-text)]" : "avatar-surface", variant === "catalog" && "catalog-avatar__image", sizes[size])}>
        {src && !imageFailed ? <img alt={name} className="image-fade h-full w-full object-cover" decoding="async" loading="lazy" onError={() => setImageFailed(true)} src={src} /> : <div aria-label={name} className="grid h-full place-items-center font-bold">{name.slice(0, 1)}</div>}
      </div>
      {verified && (
        <span className={cn("avatar-verified absolute -bottom-1 -right-1 grid h-8 w-8 place-items-center rounded-full", variant === "catalog" && "catalog-avatar__verified")}>
          <Icon className="h-4 w-4" name="check" />
        </span>
      )}
    </div>
  );
}

export function Rating({ value, count }: { value?: number | null; count?: number }) {
  const { t } = useI18n();
  return (
    <div className="inline-flex items-center gap-1 text-[13px] font-semibold">
      <span className="text-brand-warning">★</span>
      <span>{value == null ? "-" : formatRating(value)}</span>
      {count !== undefined && <span className="font-normal text-brand-muted">({t("common.reviews", { count })})</span>}
    </div>
  );
}

export function StatsCard({ icon, value, label }: { icon?: string; value: string; label: string }) {
  return (
    <div className="glass-card p-4 text-center">
      {icon && <Icon className="mx-auto mb-2 text-brand-muted" name={icon} />}
      <div className="text-xl font-extrabold tracking-tight">{value}</div>
      <div className="mt-1 text-xs text-brand-muted">{label}</div>
    </div>
  );
}

export function Skeleton({ className }: { className?: string }) {
  return <div className={cn("shimmer rounded-[var(--bb-radius-card)]", className)} />;
}

export function EmptyState({ title, subtitle, icon = "search" }: { title: string; subtitle: string; icon?: string }) {
  return (
    <Card className="ds-state" role="status">
      <div aria-hidden="true" className="ds-state__icon mx-auto grid h-14 w-14 place-items-center">
        <Icon name={icon} />
      </div>
      <h3 className="ds-state__title mt-4">{title}</h3>
      <p className="ds-state__subtitle mt-2">{subtitle}</p>
    </Card>
  );
}

export function LoadingState({ title }: { title?: string }) {
  const { t } = useI18n();
  return <section aria-busy="true" aria-live="polite" className="grid gap-3"><Skeleton className="h-24" /><Skeleton className="h-32" /><p className="text-center text-sm font-semibold text-brand-muted">{title ?? t("common.loading")}</p></section>;
}

export function ErrorState({ title, subtitle, onRetry }: { title?: string; subtitle?: string; onRetry?: () => void }) {
  const { t } = useI18n();
  return <div className="space-y-3"><EmptyState icon="filter" subtitle={subtitle ?? t("ui.errorSubtitle")} title={title ?? t("ui.errorTitle")} />{onRetry && <Button className="w-full" onClick={onRetry} type="button">{t("common.retry")}</Button>}</div>;
}

export type ToastTone = "success" | "saved" | "deleted" | "copied" | "error" | "warning" | "info";

export function Toast({ message, tone = "success" }: { message: string; tone?: ToastTone }) {
  const { haptic } = useTelegram();
  const [visible, setVisible] = useState(Boolean(message));
  useEffect(() => {
    if (!message) {
      setVisible(false);
      return;
    }
    setVisible(true);
    if (tone === "error") haptic.error();
    else if (tone === "warning") haptic.warning();
    else haptic.success();
    const timer = window.setTimeout(() => setVisible(false), 4000);
    return () => window.clearTimeout(timer);
  }, [haptic, message, tone]);
  if (!message || !visible) return null;
  const isError = tone === "error";
  const isWarning = tone === "warning";
  if (typeof document === "undefined") return null;
  return createPortal(
    <div
      aria-live={isError ? "assertive" : "polite"}
      role={isError ? "alert" : "status"}
      className={cn(
        "toast-enter fixed inset-x-4 z-50 mx-auto max-w-[390px] rounded-[var(--bb-radius-control)] px-4 py-3 text-sm font-bold text-white shadow-soft",
        isError ? "bg-brand-danger" : isWarning ? "bg-brand-warning" : tone === "info" ? "bg-brand-blue" : "bg-brand-success"
      )}
      style={{ bottom: "max(6rem, calc(5rem + var(--tg-content-safe-bottom, env(safe-area-inset-bottom))))" }}
    >
      {message}
    </div>,
    document.body
  );
}

export function Modal({ open, title, children, onClose, id, variant = "default" }: { open: boolean; title: string; children: ReactNode; onClose: () => void; id?: string; variant?: "default" | "neutral" }) {
  const { t } = useI18n();
  const { registerBackButtonHandler } = useTelegram();
  const generatedTitleId = useId();
  const titleId = id ? `${id}-title` : generatedTitleId;
  const dialogRef = useRef<HTMLDivElement>(null);
  const restoreFocusRef = useRef<HTMLElement | null>(null);
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;
  const close = useCallback(() => onCloseRef.current(), []);
  useEffect(() => {
    if (!open) return;
    restoreFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const previousOverflow = document.body.style.overflow;
    const focusableSelector = "button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex='-1'])";
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        close();
        return;
      }
      if (event.key !== "Tab") return;
      const focusable = Array.from(dialogRef.current?.querySelectorAll<HTMLElement>(focusableSelector) ?? []);
      if (!focusable.length) {
        event.preventDefault();
        dialogRef.current?.focus();
        return;
      }
      const currentIndex = focusable.indexOf(document.activeElement as HTMLElement);
      const nextIndex = event.shiftKey ? currentIndex <= 0 ? focusable.length - 1 : currentIndex - 1 : currentIndex === focusable.length - 1 ? 0 : currentIndex + 1;
      event.preventDefault();
      focusable[nextIndex].focus();
    };
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", onKeyDown);
    const frame = window.requestAnimationFrame(() => dialogRef.current?.focus());
    const unregisterBackButton = registerBackButtonHandler(close);
    return () => {
      window.cancelAnimationFrame(frame);
      window.removeEventListener("keydown", onKeyDown);
      document.body.style.overflow = previousOverflow;
      unregisterBackButton();
      restoreFocusRef.current?.focus();
    };
  }, [close, open, registerBackButtonHandler]);
  if (!open) return null;
  if (typeof document === "undefined") return null;
  return createPortal(
    <div aria-modal="true" className={cn("bottom-sheet-backdrop fixed inset-0 z-[60] grid place-items-end px-3 backdrop-blur-sm", variant === "neutral" && "bottom-sheet-backdrop--neutral")} onMouseDown={(event) => { if (event.target === event.currentTarget) close(); }} role="dialog">
      <div aria-labelledby={titleId} className={cn("bottom-sheet w-full max-w-[430px] rounded-t-[var(--bb-radius-overlay)] p-5", variant === "neutral" && "bottom-sheet--neutral")} data-keyboard-scroll-container id={id} ref={dialogRef} tabIndex={-1}>
        <div className="ds-dialog__handle mx-auto mb-4 h-1 w-10 rounded-full" />
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-xl font-extrabold" id={titleId}>{title}</h3>
          <button aria-label={t("ui.close")} className="ds-icon-button grid h-10 w-10 place-items-center rounded-full" onClick={close} type="button">
            <Icon className="h-4 w-4" name="close" />
          </button>
        </div>
        {children}
      </div>
    </div>,
    document.body
  );
}

export function BottomSheet({ open, title, children, onClose, id, variant }: { open: boolean; title: string; children: ReactNode; onClose: () => void; id?: string; variant?: "default" | "neutral" }) {
  return <Modal id={id} onClose={onClose} open={open} title={title} variant={variant}>{children}</Modal>;
}

let fixedActionBars = 0;

export function FixedActionBar({ children }: { children: ReactNode }) {
  // The bar floats over the page; tell the page to reserve room so the last section stays readable.
  useEffect(() => {
    fixedActionBars += 1;
    document.body.dataset.fixedActionBar = "true";
    return () => {
      fixedActionBars -= 1;
      if (fixedActionBars === 0) delete document.body.dataset.fixedActionBar;
    };
  }, []);
  if (typeof document === "undefined") return null;
  return createPortal(<div className="fixed-action-bar fixed inset-x-0 z-40 mx-auto max-w-[430px] px-5">{children}</div>, document.body);
}

export function BottomNav() {
  const { t } = useI18n();
  const { haptic } = useTelegram();
  const rootScreenVisible = useRootScreenVisibility();
  const { isOpen: keyboardOpen } = useVirtualKeyboard();
  const [hash, setHash] = useState(window.location.hash || "#/");
  const actionCounts = useActionCounts();

  useEffect(() => {
    const handler = () => setHash(window.location.hash || "#/");
    window.addEventListener("hashchange", handler);
    return () => window.removeEventListener("hashchange", handler);
  }, []);

  const items = [
    { href: "#/search", label: t("nav.search"), icon: "search" },
    { href: "#/campaigns", label: t("nav.campaigns"), icon: "send" },
    { href: "#/requests", label: t("nav.requests"), icon: "briefcase" },
    { href: "#/profile", label: t("nav.profile"), icon: "user" }
  ];
  const isActive = (href: string) => {
    if (href === "#/search") return hash.startsWith("#/search") || hash.startsWith("#/blogger/") || hash.startsWith("#/brand-face-detail/");
    if (href === "#/campaigns") return hash.startsWith("#/campaigns") || hash.startsWith("#/campaign/") || hash.startsWith("#/company/") || hash.startsWith("#/my-campaigns") || hash.startsWith("#/my-campaign/") || hash.startsWith("#/my-campaign-applications/");
    if (href === "#/requests") return hash.startsWith("#/requests") || hash.startsWith("#/my-application/") || hash.startsWith("#/deal/") || hash.startsWith("#/offer/");
    return ["#/profile", "#/settings", "#/favorites", "#/blogger-form", "#/business"].some((route) => hash.startsWith(route))
      || (hash.startsWith("#/brand-face") && !hash.startsWith("#/brand-face-detail/"));
  };
  const renderItem = (item: typeof items[number]) => {
    const active = isActive(item.href);
    return <a aria-current={active ? "page" : undefined} className={cn("bottom-nav__item", active && "bottom-nav__item--active")} href={item.href} key={item.href} onClick={() => haptic.selection()}><span aria-hidden="true" className="bottom-nav__icon"><Icon name={item.icon} /></span>{item.href === "#/requests" && <ActionBadge count={actionCounts.total} label={t("requests.actionBadge", { count: actionCounts.total })} />}<span className="bottom-nav__label">{item.label}</span></a>;
  };

  if (!rootScreenVisible || keyboardOpen || typeof document === "undefined") return null;

  return createPortal(
    <nav aria-label={t("nav.aria")} className="bottom-nav fixed inset-x-0 bottom-0 z-40 mx-auto max-w-[430px]">
      <div className="bottom-nav__items">
        {items.slice(0, 2).map(renderItem)}
        <button aria-current={hash === "#/" ? "page" : undefined} aria-label={t("nav.home")} className={cn("bottom-nav__item", "bottom-nav__home", hash === "#/" && "bottom-nav__item--active")} onClick={() => { haptic.impact(); if (!requestGuardedNavigation("/")) window.location.hash = "/"; }} type="button"><span aria-hidden="true" className="bottom-nav__icon"><Icon name="home" /></span><span className="bottom-nav__label">{t("nav.home")}</span></button>
        {items.slice(2).map(renderItem)}
      </div>
    </nav>
    ,
    document.body
  );
}
