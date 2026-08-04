export type ContactKind = "phone" | "telegram" | "instagram" | "tiktok" | "youtube" | "website" | "email";

export type ContactItem = { kind: ContactKind; value: string };

export function formatPhone(value: string) {
  const digits = value.replace(/\D/g, "");
  const local = digits.startsWith("998") ? digits.slice(3) : digits;
  if (local.length !== 9) return value;
  return `+998 ${local.slice(0, 2)} ${local.slice(2, 5)} ${local.slice(5, 7)} ${local.slice(7)}`;
}

export function normalizeWebsite(value: string) {
  const trimmed = value.trim();
  if (!trimmed) return "";
  if (/^https:\/\//i.test(trimmed)) return trimmed;
  if (/^[a-z][a-z\d+.-]*:/i.test(trimmed)) return trimmed;
  return `https://${trimmed}`;
}

export function safeExternalUrl(value: string) {
  const normalized = normalizeWebsite(value);
  try {
    const url = new URL(normalized);
    return url.protocol === "https:" ? url.toString() : null;
  } catch {
    return null;
  }
}

export function socialHandle(value: string) {
  const trimmed = value.trim();
  if (!trimmed) return "";
  try {
    const url = new URL(normalizeWebsite(trimmed));
    const segments = url.pathname.split("/").filter(Boolean);
    return (segments[segments.length - 1] ?? "").replace(/^@/, "");
  } catch {
    return trimmed.replace(/^@/, "");
  }
}

export function socialUrl(kind: "instagram" | "tiktok" | "telegram", value: string) {
  const handle = socialHandle(value);
  if (!handle) return "";
  const host = kind === "instagram" ? "instagram.com" : kind === "tiktok" ? "tiktok.com" : "t.me";
  return `https://${host}/${kind === "tiktok" ? "@" : ""}${handle}`;
}

export function contactUrl(item: ContactItem) {
  if (item.kind === "phone") return `tel:${item.value.replace(/[^+\d]/g, "")}`;
  if (item.kind === "email") return `mailto:${item.value}`;
  if (item.kind === "telegram") return safeExternalUrl(socialUrl("telegram", item.value));
  if (item.kind === "instagram") return safeExternalUrl(socialUrl("instagram", item.value));
  if (item.kind === "tiktok") return safeExternalUrl(socialUrl("tiktok", item.value));
  return safeExternalUrl(item.value);
}

export function displayContact(item: ContactItem) {
  if (item.kind === "phone") return formatPhone(item.value);
  if (item.kind === "telegram" || item.kind === "instagram" || item.kind === "tiktok") return `@${socialHandle(item.value)}`;
  return item.value;
}

export async function copyText(value: string) {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(value);
    return;
  }

  const input = document.createElement("textarea");
  input.value = value;
  input.style.position = "fixed";
  input.style.opacity = "0";
  document.body.append(input);
  input.select();
  document.execCommand("copy");
  input.remove();
}
