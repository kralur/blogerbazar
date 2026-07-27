import WebApp from "@twa-dev/sdk";

const API_URL = import.meta.env.VITE_API_URL ?? "";

type ApiOptions = RequestInit;

function authHeaders(options?: ApiOptions) {
  const headers = new Headers(options?.headers);
  const initData = WebApp.initData;

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
    throw new Error(data?.title ?? data?.error ?? "API error");
  }

  return data as T;
}

export type TelegramInvoiceStatus = "paid" | "cancelled" | "failed" | "pending";

export function openTelegramInvoice(invoiceLink: string): Promise<TelegramInvoiceStatus> {
  return new Promise((resolve, reject) => {
    try {
      WebApp.openInvoice(invoiceLink, (status) => resolve(status as TelegramInvoiceStatus));
    } catch (error) {
      reject(error);
    }
  });
}
