import { telegramBridge } from "../telegram/TelegramProvider";
import { translate } from "../i18n";

const API_URL = import.meta.env.VITE_API_URL ?? "";

type ApiOptions = RequestInit;
type ProblemDetailsPayload = {
  code?: string;
  errors?: Record<string, string[]>;
};

export class ApiError extends Error {
  readonly status: number;
  readonly code?: string;
  readonly validationFields: string[];

  constructor(status: number, code?: string, validationFields: string[] = []) {
    super(friendlyError(status, code));
    this.name = "ApiError";
    this.status = status;
    this.code = code?.toLowerCase();
    this.validationFields = validationFields;
  }
}

export function getApiErrorMessage(error: unknown, fallback: string, options?: { validationMessages?: Record<string, string>; conflictMessage?: string }) {
  if (!(error instanceof ApiError)) return fallback;

  if (error.code === "validation_failed") {
    const validationMessage = error.validationFields
      .map((field) => options?.validationMessages?.[field.toLowerCase()])
      .find((message): message is string => Boolean(message));
    return validationMessage ?? translate("error.validation_failed");
  }

  if (error.status === 409 && options?.conflictMessage) return options.conflictMessage;
  return error.message || fallback;
}

function friendlyError(status: number, code?: string) {
  const normalizedCode = code?.toLowerCase();
  if (normalizedCode === "validation_failed") return translate("error.validation_failed");
  if (normalizedCode === "payment_provider_unavailable") return translate("error.payment_provider_unavailable");
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

  const data = await response.json().catch(() => null) as ProblemDetailsPayload | null;
  if (!response.ok) {
    throw new ApiError(response.status, data?.code, Object.keys(data?.errors ?? {}));
  }

  return data as T;
}
