import { useEffect, useId, useMemo, useRef, useState, type ChangeEvent } from "react";
import { useI18n } from "../i18n";
import { Avatar, Button, Icon } from "./ui";

const maxFileSizeBytes = 5 * 1024 * 1024;
const allowedTypes = new Set(["image/jpeg", "image/jpg", "image/png", "image/webp"]);

export type PendingProfileImage = File | null | undefined;

export function ProfileMediaPicker({
  name,
  currentUrl,
  fallbackUrl,
  pending,
  disabled = false,
  compact = false,
  canRemove,
  onChange
}: {
  name: string;
  currentUrl?: string | null;
  fallbackUrl?: string | null;
  pending: PendingProfileImage;
  disabled?: boolean;
  compact?: boolean;
  canRemove?: boolean;
  onChange: (image: PendingProfileImage) => void;
}) {
  const { t } = useI18n();
  const inputId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState("");
  const previewUrl = useMemo(() => pending instanceof File ? URL.createObjectURL(pending) : undefined, [pending]);
  const displayedUrl = pending === null ? fallbackUrl : previewUrl ?? currentUrl ?? fallbackUrl;
  const hasImage = Boolean(displayedUrl);
  const canDelete = canRemove ?? hasImage;

  useEffect(() => () => {
    if (previewUrl) URL.revokeObjectURL(previewUrl);
  }, [previewUrl]);

  const selectFile = (event: ChangeEvent<HTMLInputElement>) => {
    const image = event.target.files?.[0];
    event.target.value = "";
    if (!image) return;
    if (!allowedTypes.has(image.type) || image.size > maxFileSizeBytes) {
      setError(t("profileMedia.invalidFile"));
      return;
    }

    setError("");
    onChange(image);
  };

  const fileInput = <input accept="image/jpeg,image/png,image/webp" aria-label={t("profileMedia.selectAria")} className="sr-only" disabled={disabled} id={inputId} onChange={selectFile} ref={inputRef} type="file" />;

  if (compact) return <div aria-label={t("profileMedia.sectionAria")} className="relative shrink-0">
    {fileInput}
    <Avatar name={name} size="md" src={displayedUrl} />
    <button aria-label={hasImage ? t("profileMedia.replace") : t("profileMedia.upload")} className="absolute -bottom-1 -right-1 grid h-8 w-8 place-items-center rounded-full border-2 border-white bg-brand-gradient text-white shadow-card disabled:opacity-50" disabled={disabled} onClick={() => inputRef.current?.click()} type="button"><Icon className="h-4 w-4" name="plus" /></button>
    {canDelete && <button aria-label={t("profileMedia.delete")} className="absolute -left-1 -top-1 grid h-7 w-7 place-items-center rounded-full border-2 border-white bg-slate-700 text-white shadow-card disabled:opacity-50" disabled={disabled} onClick={() => { setError(""); onChange(null); }} type="button"><Icon className="h-3.5 w-3.5" name="close" /></button>}
    {error && <p className="absolute left-0 top-full z-10 mt-2 w-56 rounded-xl bg-red-50 px-3 py-2 text-xs font-semibold text-brand-danger shadow-card" role="alert">{error}</p>}
  </div>;

  return <section aria-label={t("profileMedia.sectionAria")} className="rounded-3xl border border-brand-line bg-white p-4 shadow-card">
    <div className="flex items-center gap-4">
      <Avatar name={name} size="lg" src={displayedUrl} />
      <div className="min-w-0 flex-1"><h2 className="font-extrabold">{t("profileMedia.title")}</h2><p className="mt-1 text-sm leading-5 text-brand-muted">{t("profileMedia.helper")}</p></div>
    </div>
    {fileInput}
    <div className="mt-4 grid grid-cols-2 gap-3">
      <Button disabled={disabled} onClick={() => inputRef.current?.click()} type="button" variant="secondary">{hasImage ? t("profileMedia.replace") : t("profileMedia.upload")}</Button>
      {canDelete ? <Button disabled={disabled} onClick={() => { setError(""); onChange(null); }} type="button" variant="ghost">{t("profileMedia.delete")}</Button> : <div />}
    </div>
    {pending instanceof File && <p className="mt-3 text-xs font-semibold text-brand-blue">{t("profileMedia.readyToSave")}</p>}
    {error && <p className="mt-3 text-xs font-semibold text-brand-danger" role="alert">{error}</p>}
  </section>;
}
