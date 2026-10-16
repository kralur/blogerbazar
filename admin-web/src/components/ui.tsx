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

export function Badge({ children, tone = "neutral" }: { children: ReactNode; tone?: "neutral" | "warning" }) {
  const look = tone === "warning"
    ? "border-transparent bg-[color:var(--bb-warning-subtle)] text-[color:var(--bb-warning-on-subtle)]"
    : "border-brand-line text-brand-muted";
  return <span className={`inline-block shrink-0 rounded-full border px-2 py-0.5 text-xs font-semibold ${look}`}>{children}</span>;
}

export function BrandMark() {
  return (
    <span aria-hidden="true" className="grid h-9 w-9 shrink-0 place-items-center rounded-[var(--bb-radius-control)] bg-brand-accent text-sm font-black text-[color:var(--bb-accent-text)]">
      BB
    </span>
  );
}

// A row of choices where one is on (period, role, chart or table). Wraps on a narrow screen instead of scrolling.
export function Choice<T extends string>({ label, options, value, onChange }: { label: string; options: { value: T; label: string }[]; value: T; onChange: (value: T) => void }) {
  return (
    <div role="group" aria-label={label} className="flex flex-wrap gap-2">
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          aria-pressed={option.value === value}
          onClick={() => onChange(option.value)}
          className={`min-h-[40px] rounded-full border px-4 text-sm font-semibold transition-colors ${option.value === value ? "border-transparent bg-brand-accent text-[color:var(--bb-accent-text)]" : "border-brand-line bg-brand-surface text-brand-ink hover:bg-brand-soft"}`}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}

// One headline number with its name and an optional second line.
export function Tile({ label, value, note }: { label: string; value: string; note?: string }) {
  return (
    <div className="min-w-0 rounded-[var(--bb-radius-card)] border border-brand-line bg-brand-surface p-4 shadow-card">
      <p className="text-sm text-brand-muted">{label}</p>
      <p className="mt-1 break-words text-2xl font-extrabold tabular-nums">{value}</p>
      {note && <p className="mt-1 text-xs text-brand-muted">{note}</p>}
    </div>
  );
}

// A wide table scrolls inside its own box, so the page itself never scrolls sideways.
export function Table({ caption, head, rows, empty }: { caption: string; head: string[]; rows: ReactNode[][]; empty?: string }) {
  if (rows.length === 0 && empty) return <p className="text-sm text-brand-muted">{empty}</p>;
  return (
    <div className="max-w-full overflow-x-auto">
      <table className="w-full border-collapse text-sm">
        <caption className="sr-only">{caption}</caption>
        <thead>
          <tr className="border-b border-brand-line text-left text-xs text-brand-muted">
            {head.map((name, index) => <th key={name} scope="col" className={`px-3 py-2 font-semibold ${index > 0 ? "whitespace-nowrap text-right" : ""}`}>{name}</th>)}
          </tr>
        </thead>
        <tbody>
          {rows.map((cells, rowIndex) => (
            <tr key={rowIndex} className="border-b border-brand-line last:border-0">
              {cells.map((cell, index) => index === 0
                ? <th key={index} scope="row" className="min-w-[7rem] max-w-[16rem] break-words px-3 py-2 text-left font-medium">{cell}</th>
                : <td key={index} className="whitespace-nowrap px-3 py-2 text-right tabular-nums">{cell}</td>)}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// What a report shows while it loads or when it failed.
export function LoadFailed({ message, retry, onRetry }: { message: string; retry: string; onRetry: () => void }) {
  return (
    <div className="space-y-3">
      <Alert>{message}</Alert>
      <Button onClick={onRetry}>{retry}</Button>
    </div>
  );
}
