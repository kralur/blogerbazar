import { useEffect, useRef, type ReactNode } from "react";
import { Button, Icon } from "./ui";

export function WizardLayout({ children, actionBar }: { children: ReactNode; actionBar: ReactNode }) {
  useEffect(() => {
    const root = document.documentElement;
    const updateKeyboardOffset = () => {
      const viewport = window.visualViewport;
      const offset = viewport ? Math.max(0, window.innerHeight - viewport.height - viewport.offsetTop) : 0;
      root.style.setProperty("--wizard-keyboard-offset", `${offset}px`);
    };

    updateKeyboardOffset();
    window.visualViewport?.addEventListener("resize", updateKeyboardOffset);
    window.visualViewport?.addEventListener("scroll", updateKeyboardOffset);
    return () => {
      window.visualViewport?.removeEventListener("resize", updateKeyboardOffset);
      window.visualViewport?.removeEventListener("scroll", updateKeyboardOffset);
      root.style.removeProperty("--wizard-keyboard-offset");
    };
  }, []);

  return <section className="wizard-screen"><div className="wizard-screen__content">{children}</div>{actionBar}</section>;
}

export function WizardHeader({ title, stepTitle, step, totalSteps, backLabel, progressLabel, onBack }: {
  title: string;
  stepTitle: string;
  step: number;
  totalSteps: number;
  backLabel: string;
  progressLabel: string;
  onBack: () => void;
}) {
  return <header className="wizard-header">
    <button aria-label={backLabel} className="wizard-header__back" onClick={onBack} type="button"><Icon name="back" /></button>
    <div className="wizard-header__copy"><p>{title}</p><h1>{stepTitle}</h1></div>
    <ProgressIndicator current={step} label={progressLabel} total={totalSteps} />
  </header>;
}

export function ProgressIndicator({ current, total, label }: { current: number; total: number; label: string }) {
  const progress = Math.round(current / total * 100);
  return <div aria-label={label} aria-valuemax={total} aria-valuemin={1} aria-valuenow={current} aria-valuetext={label} className="wizard-progress" role="progressbar">
    <span className="wizard-progress__label">{label}</span>
    <span aria-hidden="true" className="wizard-progress__track"><span className="wizard-progress__value" style={{ width: `${progress}%` }} /></span>
  </div>;
}

export function WizardStep({ stepKey, children }: { stepKey: string; children: ReactNode }) {
  const headingRef = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    headingRef.current?.focus();
  }, [stepKey]);

  return <div className="wizard-step" key={stepKey}>
    <h2 className="sr-only" ref={headingRef} tabIndex={-1}>{stepKey}</h2>
    {children}
  </div>;
}

export function FixedActionBar({ backLabel, continueLabel, disabled = false, loading = false, onBack, onPrimary, submit = false }: {
  backLabel: string;
  continueLabel: string;
  disabled?: boolean;
  loading?: boolean;
  onBack: () => void;
  onPrimary?: () => void;
  submit?: boolean;
}) {
  return <div className="wizard-action-bar">
    <div className="wizard-action-bar__inner">
      <Button className="wizard-action-bar__back" disabled={loading} onClick={onBack} type="button">{backLabel}</Button>
      <Button aria-busy={loading} className="wizard-action-bar__primary" disabled={disabled || loading} onClick={onPrimary} type={submit ? "submit" : "button"}>{continueLabel}</Button>
    </div>
  </div>;
}

export function ReviewSection({ title, editLabel, children, onEdit }: { title: string; editLabel: string; children: ReactNode; onEdit: () => void }) {
  return <section className="wizard-review-section">
    <div className="wizard-review-section__header"><h3>{title}</h3><button className="wizard-review-section__edit" onClick={onEdit} type="button">{editLabel}</button></div>
    <dl className="wizard-review-section__content">{children}</dl>
  </section>;
}

export function ReviewItem({ label, value }: { label: string; value?: string }) {
  if (!value) return null;
  return <div className="wizard-review-item"><dt>{label}</dt><dd>{value}</dd></div>;
}

export function WizardErrorSummary({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="wizard-error-summary" role="alert">{message}</p>;
}
