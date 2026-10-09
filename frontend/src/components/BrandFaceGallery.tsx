import { useEffect, useId, useMemo, useState, type ChangeEvent } from "react";
import { useI18n } from "../i18n";
import { Icon } from "./ui";

const maxFileSizeBytes = 5 * 1024 * 1024;
const allowedTypes = new Set(["image/jpeg", "image/jpg", "image/png", "image/webp"]);
export const maxBrandFacePhotos = 4;

// Extra photos besides the avatar (QA Q20). Saved photos are URLs; new ones wait as files until the profile is saved.
export function BrandFaceGallery({ saved, pending, disabled = false, onRemoveSaved, onAdd, onRemovePending }: {
  saved: string[];
  pending: File[];
  disabled?: boolean;
  onRemoveSaved: (url: string) => void;
  onAdd: (file: File) => void;
  onRemovePending: (index: number) => void;
}) {
  const { t } = useI18n();
  const inputId = useId();
  const [error, setError] = useState("");
  const previews = useMemo(() => pending.map((file) => URL.createObjectURL(file)), [pending]);
  useEffect(() => () => previews.forEach((url) => URL.revokeObjectURL(url)), [previews]);
  const total = saved.length + pending.length;

  const select = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    if (!allowedTypes.has(file.type) || file.size > maxFileSizeBytes) {
      setError(t("profileMedia.invalidFile"));
      return;
    }
    setError("");
    onAdd(file);
  };

  const tile = (src: string, label: string, remove: () => void, key: string) => <li className="brand-face-gallery__item" key={key}>
    <img alt="" className="brand-face-gallery__image" src={src} />
    <button aria-label={label} className="brand-face-gallery__remove" disabled={disabled} onClick={remove} type="button"><Icon className="h-4 w-4" name="close" /></button>
  </li>;

  return <section aria-label={t("brandFace.galleryTitle")} className="brand-face-gallery">
    <p className="ds-field-label">{t("brandFace.galleryTitle")}</p>
    <p className="wizard-field-helper">{t("brandFace.galleryHelper", { count: maxBrandFacePhotos })}</p>
    <ul className="brand-face-gallery__grid">
      {saved.map((url, index) => tile(url, t("brandFace.galleryRemove", { index: index + 1 }), () => onRemoveSaved(url), url))}
      {previews.map((url, index) => tile(url, t("brandFace.galleryRemove", { index: saved.length + index + 1 }), () => onRemovePending(index), url))}
      {total < maxBrandFacePhotos && <li className="brand-face-gallery__item">
        <label className={`brand-face-gallery__add${disabled ? " brand-face-gallery__add--disabled" : ""}`} htmlFor={inputId}><Icon name="plus" /><span>{t("brandFace.galleryAdd")}</span></label>
        <input accept="image/jpeg,image/png,image/webp" aria-label={t("brandFace.galleryAdd")} className="sr-only" disabled={disabled} id={inputId} onChange={select} type="file" />
      </li>}
    </ul>
    {error && <p className="mt-2 text-xs font-semibold text-brand-danger" role="alert">{error}</p>}
  </section>;
}
