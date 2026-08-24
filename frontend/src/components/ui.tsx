import { useCallback, useEffect, useRef, useState, type ButtonHTMLAttributes, type ComponentPropsWithoutRef, type InputHTMLAttributes, type ReactNode, type TextareaHTMLAttributes } from "react";
import { createPortal } from "react-dom";
import { useI18n } from "../i18n";
import { formatCurrency } from "../lib/currency";
import { useTelegram } from "../telegram/TelegramProvider";
import { useRootScreenVisibility } from "../navigation/RootScreenVisibility";

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
        "tap-target inline-flex h-[52px] items-center justify-center gap-2 rounded-2xl px-5 text-[15px] font-bold transition active:scale-[0.98]",
        "disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 disabled:shadow-none",
        variant === "primary" && "bg-brand-gradient text-white shadow-glow",
        variant === "secondary" && "border border-brand-line bg-white text-brand-ink shadow-card",
        variant === "ghost" && "bg-transparent text-brand-blue",
        variant === "danger" && "bg-brand-danger text-white shadow-card",
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

export function Container({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn("mx-auto w-full max-w-[430px]", className)}>{children}</div>;
}

export function SectionHeader({ title, action, href }: { title: string; action?: string; href?: string }) {
  return <div className="section-header"><h2 className="section-header__title">{title}</h2>{action && href && <a className="section-header__action" href={href}>{action}</a>}</div>;
}

export function Divider({ className }: { className?: string }) {
  return <div className={cn("h-px bg-brand-line", className)} />;
}

export function Input({ label, error, suffix, className, onInvalid, ...props }: InputHTMLAttributes<HTMLInputElement> & { label?: string; error?: string; suffix?: ReactNode }) {
  const { haptic } = useTelegram();
  const errorId = `${props.id ?? props.name ?? label}-error`;
  const describedBy = [props["aria-describedby"], error ? errorId : undefined].filter(Boolean).join(" ") || undefined;
  return (
    <label className="grid gap-2">
      {label && <span className="text-[13px] font-bold text-brand-muted">{label}{props.required && <span aria-hidden="true" className="ml-1 text-brand-danger">*</span>}</span>}
      <span className="input-control">
        <input
          {...props}
          className={cn(
            "h-[52px] rounded-2xl border border-brand-line bg-white px-4 text-[15px] outline-none transition placeholder:text-slate-400 focus:border-brand-blue focus:ring-4 focus:ring-blue-100",
            Boolean(suffix) && "input-control__input--with-suffix",
            error && "border-brand-danger focus:border-brand-danger focus:ring-red-100",
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
      {error && <span className="text-xs font-semibold text-brand-danger" id={errorId}>{error}</span>}
    </label>
  );
}

export function Textarea({ label, error, className, onInvalid, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement> & { label?: string; error?: string }) {
  const { haptic } = useTelegram();
  const length = typeof props.value === "string" ? props.value.length : 0;
  return (
    <label className="grid gap-2">
      {label && <span className="flex items-center justify-between gap-3 text-[13px] font-bold text-brand-muted"><span>{label}{props.required && <span aria-hidden="true" className="ml-1 text-brand-danger">*</span>}</span>{props.maxLength && <span className="font-medium text-slate-400">{length} / {props.maxLength}</span>}</span>}
      <textarea
        className={cn(
          "min-h-28 resize-none rounded-2xl border border-brand-line bg-white px-4 py-3 text-[15px] outline-none transition placeholder:text-slate-400 focus:border-brand-blue focus:ring-4 focus:ring-blue-100",
          error && "border-brand-danger focus:border-brand-danger focus:ring-red-100",
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
      {error && <span className="text-xs font-semibold text-brand-danger" id={`${props.id ?? props.name ?? label}-error`}>{error}</span>}
    </label>
  );
}

export function SearchBar({ placeholder, value, onChange, className }: { placeholder?: string; value?: string; onChange?: React.ChangeEventHandler<HTMLInputElement>; className?: string }) {
  const { t } = useI18n();
  return (
    <div className={cn("flex h-[52px] items-center gap-3 rounded-2xl bg-white px-4 shadow-card ring-1 ring-brand-line/80", className)}>
      <Icon className="text-brand-muted" name="search" />
      <input aria-label={placeholder ?? t("search.placeholder")} className="w-full bg-transparent text-[15px] outline-none placeholder:text-slate-400" onChange={onChange} placeholder={placeholder ?? t("search.placeholder")} value={value} />
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

export function Badge({ children, tone = "blue" }: { children: ReactNode; tone?: "blue" | "purple" | "gold" | "green" | "gray" | "orange" }) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-[12px] font-bold",
        tone === "blue" && "bg-blue-50 text-brand-blue",
        tone === "purple" && "bg-purple-50 text-brand-premium",
        tone === "gold" && "bg-amber-50 text-amber-600",
        tone === "green" && "bg-green-50 text-brand-success",
        tone === "gray" && "bg-slate-100 text-brand-muted",
        tone === "orange" && "bg-orange-50 text-orange-600"
      )}
    >
      {children}
    </span>
  );
}

export function StatusBadge({ children, status = "neutral" }: { children: ReactNode; status?: "success" | "warning" | "neutral" | "info" }) {
  const tone = status === "success" ? "green" : status === "warning" ? "gold" : status === "info" ? "blue" : "gray";
  return <Badge tone={tone}>{children}</Badge>;
}

export function Price({ value, className }: { value?: number | null; className?: string }) {
  return <span className={cn("font-extrabold tracking-tight", className)}>{formatCurrency(value)}</span>;
}

export function Avatar({ src, name, size = "md", verified = false, variant = "default" }: { src?: string | null; name: string; size?: "sm" | "md" | "lg" | "xl"; verified?: boolean; variant?: "default" | "home" | "catalog" }) {
  const [imageFailed, setImageFailed] = useState(false);
  useEffect(() => setImageFailed(false), [src]);
  const sizes = { sm: "h-12 w-12", md: "h-16 w-16", lg: "h-24 w-24", xl: "h-32 w-32" };
  return (
    <div className={cn("relative shrink-0", variant === "home" && "home-avatar", variant === "catalog" && "catalog-avatar")}>
      <div className={cn("overflow-hidden rounded-full bg-gradient-to-br from-blue-100 to-cyan-100 ring-4 ring-white", variant === "home" && "home-avatar__image", variant === "catalog" && "catalog-avatar__image", sizes[size])}>
        {src && !imageFailed ? <img alt={name} className="image-fade h-full w-full object-cover" decoding="async" loading="lazy" onError={() => setImageFailed(true)} src={src} /> : <div aria-label={name} className="grid h-full place-items-center font-bold">{name.slice(0, 1)}</div>}
      </div>
      {verified && (
        <span className={cn("absolute -bottom-1 -right-1 grid h-8 w-8 place-items-center rounded-full bg-brand-gradient text-white shadow-glow ring-4 ring-white", variant === "home" && "home-avatar__verified", variant === "catalog" && "catalog-avatar__verified")}>
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
      <span>{value ?? "—"}</span>
      {count !== undefined && <span className="font-normal text-brand-muted">({t("common.reviews", { count })})</span>}
    </div>
  );
}

export function StatsCard({ icon, value, label }: { icon?: string; value: string; label: string }) {
  return (
    <div className="rounded-3xl border border-brand-line bg-white/90 p-4 text-center shadow-card">
      {icon && <Icon className="mx-auto mb-2 text-brand-blue" name={icon} />}
      <div className="text-xl font-extrabold tracking-tight">{value}</div>
      <div className="mt-1 text-xs text-brand-muted">{label}</div>
    </div>
  );
}

export function PaywallCard({ title, subtitle, price, cta, onClick }: { title: string; subtitle: string; price?: string; cta?: string; onClick?: () => void }) {
  const { t } = useI18n();
  return (
    <Card className="overflow-hidden p-0">
      <div className="relative p-5 text-center">
        <div className="pointer-events-none absolute inset-x-4 top-3 h-16 rounded-full bg-gradient-to-r from-blue-200 via-purple-200 to-cyan-200 blur-2xl" />
        <div className="relative mx-auto grid h-16 w-16 place-items-center rounded-3xl bg-premium-gradient text-white shadow-glow">
          <Icon name="lock" />
        </div>
        <h3 className="relative mt-4 text-xl font-extrabold leading-tight">{title}</h3>
        <p className="relative mt-2 text-sm leading-5 text-brand-muted">{subtitle}</p>
        <div className="relative mt-3 text-lg font-extrabold">{price ?? formatCurrency(29000)}</div>
        <Button aria-describedby={onClick ? undefined : "paywall-coming-soon"} className="relative mt-4 w-full" disabled={!onClick} onClick={onClick} type="button">
          {cta ?? t("common.open")}
        </Button>
        {!onClick && <p className="relative mt-2 text-xs text-brand-muted" id="paywall-coming-soon">{t("ui.paywallSoon")}</p>}
      </div>
    </Card>
  );
}

export function PromotionCard({ title, subtitle, audience }: { title: string; subtitle: string; audience: string }) {
  const { t } = useI18n();
  return <Card className="overflow-hidden bg-gradient-to-br from-blue-50 via-white to-cyan-50"><Badge tone="gray">{t("ui.soon")}</Badge><h3 className="mt-3 text-lg font-extrabold">{title}</h3><p className="mt-2 text-sm leading-5 text-brand-muted">{subtitle}</p><p className="mt-3 text-xs font-bold text-brand-blue">{t("ui.forAudience", { audience })}</p><Button className="mt-4 w-full" disabled title={t("ui.promotionUnavailable")} type="button">{t("ui.soon")}</Button></Card>;
}

export function Skeleton({ className }: { className?: string }) {
  return <div className={cn("shimmer rounded-3xl", className)} />;
}

export function EmptyState({ title, subtitle, icon = "search" }: { title: string; subtitle: string; icon?: string }) {
  return (
    <Card className="py-8 text-center" role="status">
      <div aria-hidden="true" className="mx-auto grid h-14 w-14 place-items-center rounded-3xl bg-blue-50 text-brand-blue">
        <Icon name={icon} />
      </div>
      <h3 className="mt-4 text-lg font-extrabold">{title}</h3>
      <p className="mt-2 text-sm text-brand-muted">{subtitle}</p>
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

export function OfflineState({ onRetry }: { onRetry?: () => void }) {
  const { t } = useI18n();
  return <ErrorState onRetry={onRetry} subtitle={t("ui.offlineSubtitle")} title={t("ui.offlineTitle")} />;
}

export function PermissionDeniedState({ subtitle }: { subtitle?: string }) {
  const { t } = useI18n();
  return <EmptyState icon="lock" subtitle={subtitle ?? t("ui.accessDeniedSubtitle")} title={t("ui.accessDenied")} />;
}

export function NoDataState({ title, subtitle, icon = "search", action }: { title: string; subtitle: string; icon?: string; action?: ReactNode }) {
  return <div className="space-y-3"><EmptyState icon={icon} subtitle={subtitle} title={title} />{action}</div>;
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
        "toast-enter fixed inset-x-4 bottom-24 z-50 mx-auto max-w-[390px] rounded-2xl px-4 py-3 text-sm font-bold text-white shadow-glow",
        isError ? "bg-brand-danger" : isWarning ? "bg-amber-500" : tone === "info" ? "bg-brand-blue" : "bg-brand-success"
      )}
    >
      {message}
    </div>,
    document.body
  );
}

export function Modal({ open, title, children, onClose, id }: { open: boolean; title: string; children: ReactNode; onClose: () => void; id?: string }) {
  const { t } = useI18n();
  const { registerBackButtonHandler } = useTelegram();
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
    <div aria-modal="true" className="bottom-sheet-backdrop fixed inset-0 z-[60] grid place-items-end bg-slate-950/30 px-3 backdrop-blur-sm" onMouseDown={(event) => { if (event.target === event.currentTarget) close(); }} role="dialog">
      <div aria-labelledby="bottom-sheet-title" className="bottom-sheet w-full max-w-[430px] rounded-t-[32px] bg-white p-5 shadow-soft" id={id} ref={dialogRef} tabIndex={-1}>
        <div className="mx-auto mb-4 h-1.5 w-10 rounded-full bg-slate-200" />
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-xl font-extrabold" id="bottom-sheet-title">{title}</h3>
          <button aria-label={t("ui.close")} className="grid h-10 w-10 place-items-center rounded-full bg-slate-100" onClick={close} type="button">
            ×
          </button>
        </div>
        {children}
      </div>
    </div>,
    document.body
  );
}

export function BottomSheet({ open, title, children, onClose, id }: { open: boolean; title: string; children: ReactNode; onClose: () => void; id?: string }) {
  return <Modal id={id} onClose={onClose} open={open} title={title}>{children}</Modal>;
}

export function FixedActionBar({ children }: { children: ReactNode }) {
  if (typeof document === "undefined") return null;
  return createPortal(<div className="fixed-action-bar fixed inset-x-0 z-40 mx-auto max-w-[430px] px-5">{children}</div>, document.body);
}

export function FloatingActionButton({ children, ariaLabel, onClick }: { children: ReactNode; ariaLabel: string; onClick: () => void }) {
  const rootScreenVisible = useRootScreenVisibility();
  if (!rootScreenVisible || typeof document === "undefined") return null;
  return createPortal(<button aria-label={ariaLabel} className="floating-action fixed right-5 z-40 grid h-14 w-14 place-items-center rounded-full bg-brand-gradient text-white shadow-glow transition active:scale-95" onClick={onClick} type="button">{children}</button>, document.body);
}

export function BottomNav() {
  const { t } = useI18n();
  const { haptic } = useTelegram();
  const rootScreenVisible = useRootScreenVisibility();
  const [hash, setHash] = useState(window.location.hash || "#/");

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
    if (href === "#/campaigns") return hash.startsWith("#/campaigns") || hash.startsWith("#/campaign/");
    if (href === "#/requests") return hash.startsWith("#/requests");
    return ["#/profile", "#/favorites", "#/blogger-form", "#/business"].some((route) => hash.startsWith(route))
      || (hash.startsWith("#/brand-face") && !hash.startsWith("#/brand-face-detail/"));
  };
  const renderItem = (item: typeof items[number]) => {
    const active = isActive(item.href);
    return <a aria-current={active ? "page" : undefined} className={cn("bottom-nav__item", active && "bottom-nav__item--active")} href={item.href} key={item.href} onClick={() => haptic.selection()}><span aria-hidden="true" className="bottom-nav__icon"><Icon name={item.icon} /></span><span className="bottom-nav__label">{item.label}</span></a>;
  };

  if (!rootScreenVisible || typeof document === "undefined") return null;

  return createPortal(
    <nav aria-label={t("nav.aria")} className="bottom-nav fixed inset-x-0 bottom-0 z-40 mx-auto max-w-[430px]">
      <div className="bottom-nav__items">
        {items.slice(0, 2).map(renderItem)}
        <button aria-current={hash === "#/" ? "page" : undefined} aria-label={t("nav.home")} className={cn("bottom-nav__item", "bottom-nav__home", hash === "#/" && "bottom-nav__item--active")} onClick={() => { haptic.impact(); window.location.hash = "/"; }} type="button"><span aria-hidden="true" className="bottom-nav__icon"><Icon name="home" /></span><span className="bottom-nav__label">{t("nav.home")}</span></button>
        {items.slice(2).map(renderItem)}
      </div>
    </nav>
    ,
    document.body
  );
}
