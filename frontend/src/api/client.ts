import { telegramBridge } from "../telegram/TelegramProvider";
import { translate } from "../i18n";

const API_URL = import.meta.env.VITE_API_URL ?? "";

type ApiOptions = RequestInit;

function friendlyError(status: number, code?: string) {
  const normalizedCode = code?.toLowerCase();
  if (status === 401 || status === 403) return translate("error.access_denied");
  if (status === 404) return translate("error.not_found");
  if (status === 409) return translate("error.conflict");
  if (status >= 500) return translate("error.server");
  return translate("error.default");
}

function authHeaders(options?: ApiOptions) {
  const headers = new Headers(options?.headers);
  const initData = telegramBridge.initData;

  if (initData) {
    headers.set("authorization", `tma ${initData}`);
  }

  if (!headers.has("content-type") && options?.body) {
    headers.set("content-type", "application/json");
  }

  return headers;
}

export async function api<T>(path: string, options?: ApiOptions): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: authHeaders(options)
  });

  const data = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(friendlyError(response.status, data?.code));
  }

  return data as T;
}
