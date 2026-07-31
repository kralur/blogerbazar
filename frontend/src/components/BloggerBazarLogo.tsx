import { useI18n } from "../i18n";

export function BloggerBazarLogo({ size = 136 }: { size?: number }) {
  const { t } = useI18n();
  return <svg aria-label={t("common.appName")} height={size} role="img" viewBox="0 0 120 120" width={size}><defs><linearGradient id="bloggerbazar-logo-gradient" x1="16" x2="102" y1="12" y2="106" gradientUnits="userSpaceOnUse"><stop stopColor="#2563EB" /><stop offset="1" stopColor="#06B6D4" /></linearGradient></defs><rect fill="white" height="112" rx="36" width="112" x="4" y="4" /><path d="M39 25h24c16 0 27 9 27 22 0 8-4 14-11 17 9 3 14 10 14 19 0 15-12 25-30 25H39V25Zm21 33c8 0 13-4 13-10s-5-10-13-10h-5v20h5Zm3 37c9 0 15-4 15-11 0-7-6-11-15-11h-8v22h8Z" fill="url(#bloggerbazar-logo-gradient)" /><circle cx="92" cy="29" fill="#06B6D4" r="6" /></svg>;
}
