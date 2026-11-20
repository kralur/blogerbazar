import { ApiError } from "../api/client";
// D64 stage B: true once this page signed in through a browser session (outside Telegram). The screens then hide
// what only Telegram can do: switching roles, deleting the account, the admin panel, sharing the phone.
let signedInInBrowser = false;
// The account's Telegram @username as the server keeps it: a browser has no Telegram launch data to read it from.
let browserUsername: string | null = null;

export const isBrowserSession = () => signedInInBrowser;
export const browserSessionUsername = () => (signedInInBrowser ? browserUsername : null);

export function setBrowserSession(value: boolean, username?: string | null) {
  signedInInBrowser = value;
  browserUsername = value ? username ?? null : null;
}

// The website is only for a company (D64 B): a blogger or brand-face profile is never looked up there. Treated as
// "no such profile" (404) without asking the server, which answered 404 on almost every page before.
export function creatorProfileOutsideBrowser<T>(load: () => Promise<T>): Promise<T> {
  return signedInInBrowser ? Promise.reject(new ApiError(404)) : load();
}
