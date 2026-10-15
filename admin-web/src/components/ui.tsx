import type { ButtonHTMLAttributes, ReactNode } from "react";

// Small Design System v2 primitives for the web admin. Colors come only from the shared --bb-* tokens.
type ButtonVariant = "primary" | "secondary" | "ghost";
const buttonVariants: Record<ButtonVariant, string> = {
  primary: "bg-brand-accent text-[color:var(--bb-accent-text)] hover:bg-[color:var(--bb-accent-pressed)]",
  secondary: "border border-brand-line bg-brand-surface text-brand-ink hover:bg-brand-soft",
  ghost: "text-brand-ink hover:bg-brand-soft"
};

export function Button({ variant = "secondary", className = "", type = "button", ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant }) {
  return (
    <button
      type={type}
      className={`inline-flex min-h-[44px] items-center justify-center gap-2 rounded-[var(--bb-radius-control)] px-4 text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-60 ${buttonVariants[variant]} ${className}`}
      {...props}
    />
  );
}

export function Card({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <section className={`rounded-[var(--bb-radius-card)] border border-brand-line bg-brand-surface p-5 shadow-card sm:p-6 ${className}`}>{children}</section>;
}

export function Spinner({ label }: { label: string }) {
  return (
    <div role="status" className="flex items-center gap-3 text-sm text-brand-muted">
      <span aria-hidden="true" className="h-5 w-5 animate-spin rounded-full border-2 border-brand-line border-t-[color:var(--bb-action)] motion-reduce:animate-none" />
      <span>{label}</span>
    </div>
  );
}

export function Alert({ children }: { children: ReactNode }) {
  return (
    <p role="alert" className="rounded-[var(--bb-radius-control)] bg-[color:var(--bb-error-subtle)] px-4 py-3 text-sm font-medium text-[color:var(--bb-error-on-subtle)]">
      {children}
    </p>
  );
}

export function Badge({ children }: { children: ReactNode }) {
  return <span className="shrink-0 rounded-full border border-brand-line px-2 py-0.5 text-xs font-semibold text-brand-muted">{children}</span>;
}

export function BrandMark() {
  return (
    <span aria-hidden="true" className="grid h-9 w-9 shrink-0 place-items-center rounded-[var(--bb-radius-control)] bg-brand-accent text-sm font-black text-[color:var(--bb-accent-text)]">
      BB
    </span>
  );
}
