import { api } from "./client";

// Shapes of /api/admin/dashboard, /api/admin/funnel and /api/admin/analytics/*. Dates are Tashkent days.
export type AdminDashboard = {
  users: number;
  bloggers: number;
  businesses: number;
  publishedCampaigns: number;
  completedDeals: number;
  promotedBloggers: number;
  promotedCampaigns: number;
  openComplaints: number;
  newUsersToday: number;
  newUsersWeek: number;
  newDealsWeek: number;
  completedDealsWeek: number;
  activeToday: number;
  activeWeek: number;
  sources: { source: string; users: number }[] | null;
};

export type FunnelPeriod = "7d" | "30d" | "all";
export type FunnelRole = "blogger" | "brandFace" | "business";
export type AdminFunnel = { period: FunnelPeriod; fromUtc: string | null; roles: { role: FunnelRole; steps: { key: string; count: number }[] }[] };

export type DailyPoint = { date: string; newUsers: number; lastSeen: number; campaigns: number; applications: number; offers: number; deals: number; completedDeals: number };
export type DailyMetric = Exclude<keyof DailyPoint, "date">;
export type AdminDaily = { from: string; to: string; days: DailyPoint[] };
export type MoneyMonth = { month: string; completedDeals: number; pricedDeals: number; total: number; average: number | null };
export type GeographyRow = { key: string; bloggers: number; brandFaces: number; campaigns: number; deals: number };
export type AdminGeography = { cities: GeographyRow[]; categories: GeographyRow[] };
export type AdminRetention = { from: string; to: string; cohort: number; steps: { days: number; eligible: number; returned: number }[] };
export type SourceRow = { source: string | null; users: number; withProfile: number; withDeal: number };

export type Period = { from?: string; to?: string };
const range = ({ from, to }: Period) => {
  const query = new URLSearchParams();
  if (from) query.set("from", from);
  if (to) query.set("to", to);
  const text = query.toString();
  return text ? `?${text}` : "";
};

export const getDashboard = () => api<AdminDashboard>("/api/admin/dashboard");
export const getFunnel = (period: FunnelPeriod) => api<AdminFunnel>(`/api/admin/funnel?period=${period}`);
export const getDaily = (period: Period) => api<AdminDaily>(`/api/admin/analytics/daily${range(period)}`);
export const getMoney = (months = 12) => api<{ months: MoneyMonth[] }>(`/api/admin/analytics/money?months=${months}`);
export const getGeography = () => api<AdminGeography>("/api/admin/analytics/geography");
export const getRetention = (period: Period) => api<AdminRetention>(`/api/admin/analytics/retention${range(period)}`);
export const getSources = () => api<{ sources: SourceRow[] }>("/api/admin/analytics/sources");
