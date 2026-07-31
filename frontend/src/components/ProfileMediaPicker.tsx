import { useEffect, useId, useMemo, useRef, useState, type ChangeEvent } from "react";
import { useI18n } from "../i18n";
import { Avatar, Button } from "./ui";

const maxFileSizeBytes = 5 * 1024 * 1024;
const allowedTypes = new Set(["image/jpeg", "image/jpg", "image/png", "image/webp"]);

export type PendingProfileImage = File | null | undefined;

export function ProfileMediaPicker({
  name,
  currentUrl,
  pending,
  disabled = false,
  onChange
}: {
  name: string;
  currentUrl?: string | null;
  pending: PendingProfileImage;
  disabled?: boolean;
  onChange: (image: PendingProfileImage) => void;
}) {
  const { t } = useI18n();
  const inputId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState("");
  const previewUrl = useMemo(() => pending instanceof File ? URL.createObjectURL(pending) : undefined, [pending]);
  const displayedUrl = pending === null ? undefined : previewUrl ?? currentUrl;
  const hasImage = Boolean(displayedUrl);

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

  return <section aria-label={t("profileMedia.sectionAria")} className="rounded-3xl border border-brand-line bg-white p-4 shadow-card">
    <div className="flex items-center gap-4">
      <Avatar name={name} size="lg" src={displayedUrl} />
      <div className="min-w-0 flex-1"><h2 className="font-extrabold">{t("profileMedia.title")}</h2><p className="mt-1 text-sm leading-5 text-brand-muted">{t("profileMedia.helper")}</p></div>
    </div>
    <input accept="image/jpeg,image/png,image/webp" aria-label={t("profileMedia.selectAria")} className="sr-only" disabled={disabled} id={inputId} onChange={selectFile} ref={inputRef} type="file" />
    <div className="mt-4 grid grid-cols-2 gap-3">
      <Button disabled={disabled} onClick={() => inputRef.current?.click()} type="button" variant="secondary">{hasImage ? t("profileMedia.replace") : t("profileMedia.upload")}</Button>
      {hasImage ? <Button disabled={disabled} onClick={() => { setError(""); onChange(null); }} type="button" variant="ghost">{t("profileMedia.delete")}</Button> : <div />}
    </div>
    {pending instanceof File && <p className="mt-3 text-xs font-semibold text-brand-blue">{t("profileMedia.readyToSave")}</p>}
    {error && <p className="mt-3 text-xs font-semibold text-brand-danger" role="alert">{error}</p>}
  </section>;
}
