import { api } from "./client";

// D64 stage B: company work in an ordinary browser. The browser asks to sign in and shows a Telegram link and a
// number; the Mini App, opened from that link, confirms it with the number. The session lives in an HttpOnly cookie
// the page never sees; the API accepts it on business actions only.

export type BrowserSignInStart = { code: string; number: number; link: string; expiresAtUtc: string };
export type BrowserSignInStatus = "pending" | "approved" | "declined" | "expired";
export type BrowserEligibility = "ok" | "switchToBusiness" | "noCompany";
export type BrowserSignInPreview = {
  browser: string;
  os: string;
  createdAtUtc: string;
  expiresAtUtc: string;
  options: number[];
  eligibility: BrowserEligibility;
  companyName?: string | null;
};
export type BrowserSession = {
  id: string;
  browser: string;
  os: string;
  createdAtUtc: string;
  lastSeenAtUtc: string;
  expiresAtUtc: string;
  isCurrent: boolean;
};

// The link that opens the Mini App with a browser sign-in: t.me/<bot>?startapp=web_<code>.
export const browserSignInPrefix = "web_";

export function browserSignInCodeFrom(startParam: string | undefined | null) {
  if (!startParam?.startsWith(browserSignInPrefix)) return null;
  const code = startParam.slice(browserSignInPrefix.length);
  return /^[A-Za-z0-9_-]{16,64}$/.test(code) ? code : null;
}

export const startBrowserSignIn = () => api<BrowserSignInStart>("/api/web/sign-in", { method: "POST" });
export const checkBrowserSignIn = () => api<{ status: BrowserSignInStatus; firstName?: string | null }>("/api/web/sign-in/check", { method: "POST" });
export const previewBrowserSignIn = (code: string) => api<BrowserSignInPreview>("/api/web/sign-in/preview", { method: "POST", body: JSON.stringify({ code }) });
export const confirmBrowserSignIn = (code: string, number: number) => api<void>("/api/web/sign-in/confirm", { method: "POST", body: JSON.stringify({ code, number }) });
export const declineBrowserSignIn = (code: string) => api<void>("/api/web/sign-in/decline", { method: "POST", body: JSON.stringify({ code }) });
export const getBrowserSessions = () => api<BrowserSession[]>("/api/web/sessions");
export const endBrowserSession = (id: string) => api<void>(`/api/web/sessions/${encodeURIComponent(id)}`, { method: "DELETE" });
export const endAllBrowserSessions = () => api<void>("/api/web/sessions/end-all", { method: "POST" });
export const signOutBrowser = () => api<void>("/api/web/sign-out", { method: "POST" });

// Browser and system names are the same in both languages, except for the catch-all ones.
const browserNames: Record<string, string> = { chrome: "Chrome", edge: "Edge", firefox: "Firefox", safari: "Safari", opera: "Opera" };
const systemNames: Record<string, string> = { windows: "Windows", macos: "macOS", linux: "Linux", android: "Android", ios: "iOS", chromeos: "ChromeOS" };

export function deviceLabel(session: { browser: string; os: string }, t: (key: string) => string) {
  const browser = browserNames[session.browser] ?? (session.browser === "yandex" ? t("browser.yandex") : t("browser.otherBrowser"));
  const system = systemNames[session.os];
  return system ? `${browser}, ${system}` : browser;
}
