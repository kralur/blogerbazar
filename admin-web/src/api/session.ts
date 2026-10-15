import { api } from "./client";

// Role values are D26 strings from the API; the screen translates them.
export type AdminRole = "owner" | "admin";

export type AdminWebSession = {
  telegramUserId: number;
  firstName: string;
  username: string | null;
  role: AdminRole;
  expiresAtUtc: string;
};

export type AdminWebConfig = { botUsername: string };

export const getAdminWebConfig = () => api<AdminWebConfig>("/api/admin-web/config");
export const getAdminWebSession = () => api<AdminWebSession>("/api/admin-web/session");
// Fields are the Telegram widget data exactly as Telegram returned it; the server checks the signature.
export const startAdminWebSession = (fields: Record<string, string>) => api<AdminWebSession>("/api/admin-web/session", { method: "POST", body: fields });
export const endAdminWebSession = () => api<void>("/api/admin-web/logout", { method: "POST" });
