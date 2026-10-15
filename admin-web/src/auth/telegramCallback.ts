import { ApiError } from "../api/client";
import { startAdminWebSession } from "../api/session";
import { browser } from "../lib/browser";

export const callbackPath = "/auth/telegram";
export type SignInError = "denied" | "expired" | "rate" | "config" | "failed";

// The Telegram widget returns here with its signed data in the query (data-auth-url). Every field goes to the
// server as it came: the signature covers all of them, so nothing is dropped or added here.
export function readWidgetFields(search: string): Record<string, string> | null {
  const params = new URLSearchParams(search);
  if (!params.has("hash") || !params.has("id")) return null;
  const fields: Record<string, string> = {};
  params.forEach((value, key) => {
    fields[key] = value;
  });
  return fields;
}

export function signInErrorFor(error: unknown): SignInError {
  if (!(error instanceof ApiError)) return "failed";
  if (error.status === 401) return "expired";
  if (error.status === 403) return "denied";
  if (error.status === 429) return "rate";
  if (error.status === 404) return "config";
  return "failed";
}

export async function completeTelegramSignIn(search: string) {
  const fields = readWidgetFields(search);
  browser.cleanAddress(callbackPath);
  if (!fields) {
    browser.replace("/login");
    return;
  }

  try {
    await startAdminWebSession(fields);
    browser.replace("/");
  } catch (error) {
    browser.replace(`/login?error=${signInErrorFor(error)}`);
  }
}
