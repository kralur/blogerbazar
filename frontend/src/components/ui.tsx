import { useEffect, useState, type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode, type TextareaHTMLAttributes } from "react";
import { useI18n } from "../i18n";

export function cn(...classes: Array<string | false | null | undefined>) {
  return classes.filter(Boolean).join(" ");
}

export function Icon({ name, className }: { name: string; className?: string }) {
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
    filter: <path d="M4 21v-7m0-4V3m8 18v-9m0-4V3m8 18v-5m0-4V3M2 14h4m4-6h4m4 8h4" />,
    bookmark: <path d="M19 21 12 17 5 21V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v16Z" />,
    heart: <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 1 0-7.78 7.78L12 21.23l8.84-8.84a5.5 5.5 0 0 0 0-7.78Z" />,
    chart: <path d="M3 3v18h18M7 15l4-4 3 3 5-7" />,
    user: <path d="M20 21a8 8 0 1 0-16 0m12-13a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z" />,
    briefcase: <path d="M10 6V5a2 2 0 0 1 2-2h0a2 2 0 0 1 2 2v1m7 4v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-9m18 0H3m18 0-2-4H5l-2 4" />,
    plus: <path d="M12 5v14m-7-7h14" />,
    back: <path d="m15 18-6-6 6-6" />,
    dots: <path d="M12 12h.01M19 12h.01M5 12h.01" />
  };

  return (
    <svg
      className={cn(common, className)}
      fill="none"
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

export function Card({ children, className }: { children: ReactNode; className?: string }) {
  return <section className={cn("glass-card p-4", className)}>{children}</section>;
}

export function Input({ label, className, ...props }: InputHTMLAttributes<HTMLInputElement> & { label?: string }) {
  return (
    <label className="grid gap-2">
      {label && <span className="text-[13px] font-bold text-brand-muted">{label}</span>}
      <input
        className={cn(
          "h-[52px] rounded-2xl border border-brand-line bg-white px-4 text-[15px] outline-none transition placeholder:text-slate-400 focus:border-brand-blue focus:ring-4 focus:ring-blue-100",
          className
        )}
        {...props}
      />
    </label>
  );
}

export function Textarea({ label, className, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement> & { label?: string }) {
  return (
    <label className="grid gap-2">
      {label && <span className="text-[13px] font-bold text-brand-muted">{label}</span>}
      <textarea
        className={cn(
          "min-h-28 rounded-2xl border border-brand-line bg-white px-4 py-3 text-[15px] outline-none transition placeholder:text-slate-400 focus:border-brand-blue focus:ring-4 focus:ring-blue-100",
          className
        )}
        {...props}
      />
    </label>
  );
}

export function SearchBar({ placeholder = "Поиск блогеров..." }: { placeholder?: string }) {
  return (
    <div className="flex h-[52px] items-center gap-3 rounded-2xl bg-white px-4 shadow-card ring-1 ring-brand-line/80">
      <Icon className="text-brand-muted" name="search" />
      <input className="w-full bg-transparent text-[15px] outline-none placeholder:text-slate-400" placeholder={placeholder} />
    </div>
  );
}

export function Chip({ children, active = false }: { children: ReactNode; active?: boolean }) {
  return (
    <span
      className={cn(
        "inline-flex h-9 items-center whitespace-nowrap rounded-full border px-4 text-[13px] font-bold",
        active ? "border-brand-blue bg-blue-50 text-brand-blue" : "border-brand-line bg-white text-brand-ink"
      )}
    >
      {children}
    </span>
  );
}

export function Badge({ children, tone = "blue" }: { children: ReactNode; tone?: "blue" | "purple" | "gold" | "green" | "gray" }) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-[12px] font-bold",
        tone === "blue" && "bg-blue-50 text-brand-blue",
        tone === "purple" && "bg-purple-50 text-brand-premium",
        tone === "gold" && "bg-amber-50 text-amber-600",
        tone === "green" && "bg-green-50 text-brand-success",
        tone === "gray" && "bg-slate-100 text-brand-muted"
      )}
    >
      {children}
    </span>
  );
}

export function Avatar({ src, name, size = "md", verified = false }: { src?: string | null; name: string; size?: "sm" | "md" | "lg" | "xl"; verified?: boolean }) {
  const sizes = { sm: "h-12 w-12", md: "h-16 w-16", lg: "h-24 w-24", xl: "h-32 w-32" };
  return (
    <div className="relative shrink-0">
      <div className={cn("overflow-hidden rounded-full bg-gradient-to-br from-blue-100 to-cyan-100 ring-4 ring-white", sizes[size])}>
        {src ? <img alt={name} className="h-full w-full object-cover" src={src} /> : <div className="grid h-full place-items-center font-bold">{name.slice(0, 1)}</div>}
      </div>
      {verified && (
        <span className="absolute -bottom-1 -right-1 grid h-8 w-8 place-items-center rounded-full bg-brand-gradient text-white shadow-glow ring-4 ring-white">
          <Icon className="h-4 w-4" name="check" />
        </span>
      )}
    </div>
  );
}

export function Rating({ value, count }: { value?: number | null; count?: number }) {
  return (
    <div className="inline-flex items-center gap-1 text-[13px] font-semibold">
      <span className="text-brand-warning">★</span>
      <span>{value ?? "—"}</span>
      {count !== undefined && <span className="font-normal text-brand-muted">({count} отзывов)</span>}
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

export function PaywallCard({ title, subtitle, price = "29 000 сум", cta = "Купить доступ" }: { title: string; subtitle: string; price?: string; cta?: string }) {
  return (
    <Card className="overflow-hidden p-0">
      <div className="relative p-5 text-center">
        <div className="pointer-events-none absolute inset-x-4 top-3 h-16 rounded-full bg-gradient-to-r from-blue-200 via-purple-200 to-cyan-200 blur-2xl" />
        <div className="relative mx-auto grid h-16 w-16 place-items-center rounded-3xl bg-premium-gradient text-white shadow-glow">
          <Icon name="lock" />
        </div>
        <h3 className="relative mt-4 text-xl font-extrabold leading-tight">{title}</h3>
        <p className="relative mt-2 text-sm leading-5 text-brand-muted">{subtitle}</p>
        <div className="relative mt-3 text-lg font-extrabold">{price}</div>
        <Button className="relative mt-4 w-full" type="button">
          {cta}
        </Button>
      </div>
    </Card>
  );
}

export function UnlockBlock({ title = "Контакты скрыты", subtitle = "Разблокируйте, чтобы связаться напрямую", price = "29 000 сум", cta, disabled = false, onUnlock }: { title?: string; subtitle?: string; price?: string; cta?: string; disabled?: boolean; onUnlock?: () => void }) {
  return (
    <div className="rounded-[28px] border border-blue-100 bg-gradient-to-br from-blue-50 via-white to-cyan-50 p-4">
      <div className="flex gap-3">
        <div className="grid h-12 w-12 shrink-0 place-items-center rounded-2xl bg-blue-100 text-brand-blue">
          <Icon name="lock" />
        </div>
        <div>
          <div className="font-extrabold">{title}</div>
          <div className="mt-1 text-sm text-brand-muted">{subtitle}</div>
        </div>
      </div>
      <Button className="mt-4 w-full" disabled={disabled} onClick={onUnlock} type="button">
        {cta ?? `Разблокировать контакты — ${price}`}
      </Button>
    </div>
  );
}

export function Skeleton({ className }: { className?: string }) {
  return <div className={cn("animate-pulse rounded-3xl bg-slate-200/80", className)} />;
}

export function EmptyState({ title, subtitle, icon = "search" }: { title: string; subtitle: string; icon?: string }) {
  return (
    <Card className="py-8 text-center">
      <div className="mx-auto grid h-14 w-14 place-items-center rounded-3xl bg-blue-50 text-brand-blue">
        <Icon name={icon} />
      </div>
      <h3 className="mt-4 text-lg font-extrabold">{title}</h3>
      <p className="mt-2 text-sm text-brand-muted">{subtitle}</p>
    </Card>
  );
}

export function Toast({ message, tone = "success" }: { message: string; tone?: "success" | "error" }) {
  if (!message) return null;
  return (
    <div
      className={cn(
        "fixed inset-x-4 bottom-24 z-50 mx-auto max-w-[390px] rounded-2xl px-4 py-3 text-sm font-bold text-white shadow-glow",
        tone === "success" ? "bg-brand-success" : "bg-brand-danger"
      )}
    >
      {message}
    </div>
  );
}

export function Modal({ open, title, children, onClose }: { open: boolean; title: string; children: ReactNode; onClose: () => void }) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 grid place-items-end bg-slate-950/30 px-3 pb-3 backdrop-blur-sm">
      <div className="w-full max-w-[430px] rounded-[32px] bg-white p-5 shadow-soft">
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-xl font-extrabold">{title}</h3>
          <button className="grid h-10 w-10 place-items-center rounded-full bg-slate-100" onClick={onClose} type="button">
            ×
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}

export function BottomNav() {
  const { t } = useI18n();
  const [hash, setHash] = useState(window.location.hash || "#/");

  useEffect(() => {
    const handler = () => setHash(window.location.hash || "#/");
    window.addEventListener("hashchange", handler);
    return () => window.removeEventListener("hashchange", handler);
  }, []);

  const items = [
    { href: "#/search", label: "Поиск", icon: "search" },
    { href: "#/campaigns", label: "Реклама", icon: "send" },
    { href: "#/requests", label: "Заявки", icon: "briefcase" },
    { href: "#/profile", label: "Профиль", icon: "user" }
  ];
  const localizedLabels = [t("nav.search"), t("nav.campaigns"), t("nav.requests"), t("nav.profile")];

  return (
    <nav className="fixed inset-x-0 bottom-0 z-40 mx-auto grid max-w-[430px] grid-cols-4 border-t border-brand-line/70 bg-white/[0.92] px-3 pb-4 pt-2 shadow-[0_-16px_40px_rgba(15,23,42,0.06)] backdrop-blur-2xl">
      {items.map((item, index) => {
        const active = hash.startsWith(item.href);
        return (
          <a
            className={cn(
              "group grid justify-items-center gap-1 rounded-2xl px-2 py-1.5 text-[11px] font-bold transition",
              active ? "text-brand-blue" : "text-brand-muted"
            )}
            href={item.href}
            key={item.href}
          >
            <span className={cn("grid h-8 w-10 place-items-center rounded-full transition", active && "bg-blue-50 shadow-sm")}>
              <Icon className={cn("h-5 w-5 transition group-active:scale-90", active && "scale-110")} name={item.icon} />
            </span>
            {localizedLabels[index]}
          </a>
        );
      })}
    </nav>
  );
}
