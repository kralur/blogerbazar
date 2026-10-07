import { useEffect, useId, useMemo, useRef, useState, type ChangeEvent } from "react";
import { useI18n } from "../i18n";
import { Avatar, BottomSheet, Button, Icon } from "./ui";

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
  className,
  onChange
}: {
  name: string;
  currentUrl?: string | null;
  fallbackUrl?: string | null;
  pending: PendingProfileImage;
  disabled?: boolean;
  compact?: boolean;
  canRemove?: boolean;
  className?: string;
  onChange: (image: PendingProfileImage) => void;
}) {
  const { t } = useI18n();
  const inputId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState("");
  const [actionsOpen, setActionsOpen] = useState(false);
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

  // One "+" on the avatar always opens the same small menu, for every role: choose a new photo, and delete
  // only when the profile has its own photo. A Telegram photo shown as a fallback is labelled as such.
  const openPhotoActions = () => {
    setError("");
    setActionsOpen(true);
  };
  const showsTelegramPhoto = !canDelete && Boolean(fallbackUrl) && displayedUrl === fallbackUrl;

  if (compact) return <div aria-label={t("profileMedia.sectionAria")} className="relative shrink-0">
    {fileInput}
    <Avatar name={name} size="md" src={displayedUrl} />
    <button aria-haspopup="dialog" aria-label={canDelete ? t("profileMedia.manage") : t("profileMedia.upload")} className="absolute -bottom-1 -right-1 grid h-8 w-8 place-items-center rounded-full border-2 border-white avatar-verified shadow-card disabled:opacity-50" disabled={disabled} onClick={openPhotoActions} type="button"><Icon className="h-4 w-4" name="plus" /></button>
    <BottomSheet onClose={() => setActionsOpen(false)} open={actionsOpen} title={t("profileMedia.title")}>
      <div className="grid gap-2">
        {showsTelegramPhoto && <p className="text-sm leading-5 text-brand-muted">{t("profileMedia.telegramPhoto")}</p>}
        <Button onClick={() => { setActionsOpen(false); inputRef.current?.click(); }} type="button" variant="secondary">{t("profileMedia.replacePhoto")}</Button>
        {canDelete && <Button onClick={() => { setActionsOpen(false); onChange(null); }} type="button" variant="danger">{t("profileMedia.deletePhoto")}</Button>}
        <p className="text-xs leading-5 text-brand-muted">{t("profileMedia.helperShort")}</p>
      </div>
    </BottomSheet>
    {error && <p className="absolute left-0 top-full z-10 mt-2 w-56 rounded-xl bg-red-50 px-3 py-2 text-xs font-semibold text-brand-danger shadow-card" role="alert">{error}</p>}
  </div>;

  return <section aria-label={t("profileMedia.sectionAria")} className={`rounded-3xl border border-brand-line bg-brand-surface p-4 shadow-card ${className ?? ""}`}>
    <div className="flex items-center gap-4">
      <Avatar name={name} size="lg" src={displayedUrl} />
      <div className="min-w-0 flex-1"><h2 className="font-extrabold">{t("profileMedia.title")}</h2><p className="mt-1 text-sm leading-5 text-brand-muted">{t("profileMedia.helper")}</p></div>
    </div>
    {fileInput}
    <div className="mt-4 grid grid-cols-2 gap-3">
      <Button disabled={disabled} onClick={() => inputRef.current?.click()} type="button" variant="secondary">{hasImage ? t("profileMedia.replace") : t("profileMedia.upload")}</Button>
      {canDelete ? <Button disabled={disabled} onClick={() => { setError(""); onChange(null); }} type="button" variant="ghost">{t("profileMedia.delete")}</Button> : <div />}
    </div>
    {pending instanceof File && <p className="mt-3 text-xs font-semibold text-brand-ink">{t("profileMedia.readyToSave")}</p>}
    {error && <p className="mt-3 text-xs font-semibold text-brand-danger" role="alert">{error}</p>}
  </section>;
}
