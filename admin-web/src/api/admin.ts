import { api } from "./client";

// Shapes of the admin lists and actions (/api/admin/*). Existing enums arrive as numbers: role 0 member … 4 owner,
// profile status 0 pending … 4 needs changes, campaign status 0 draft … 3 rejected, deal status 0 active … 2 cancelled.
export const platformRoles = ["member", "support", "moderator", "admin", "owner"] as const;
export const ownerRole = 4;

export type AdminUser = {
  telegramUserId: number;
  firstName: string;
  username: string | null;
  role: number;
  isBlocked: boolean;
  isDeleted: boolean;
  deletedAtUtc: string | null;
  deletedByTelegramUserId: number | null;
  createdAtUtc: string;
  verifiedPhone: string | null;
  hasBloggerProfile: boolean;
  hasBusinessProfile: boolean;
  hasBrandFaceProfile: boolean;
  lastSeenAtUtc?: string | null;
  source?: string | null;
};
export type UserFilter = "all" | "new" | "bloggers" | "businesses" | "brandFaces" | "admins" | "blocked" | "deleted";
export const userFilters: UserFilter[] = ["all", "new", "bloggers", "businesses", "brandFaces", "admins", "blocked", "deleted"];

export type AdminUserDetails = {
  user: AdminUser;
  profiles: { role: "blogger" | "brandFace" | "business"; id: string; name: string; isHidden: boolean; isDeleted: boolean }[];
  activeDeals: number;
  completedDeals: number;
  cancelledDeals: number;
  recentDeals: { id: string; role: "blogger" | "brandFace" | "business"; partnerName: string | null; title: string | null; status: number; agreedPrice: number | null; createdAtUtc: string }[];
  complaintsAgainst: number;
  openComplaintsAgainst: number;
  complaintsFiled: number;
};

export type AdminBlogger = { id: string; name: string; city: string; categories: string[]; avatarUrl: string | null; totalFollowers: number; status: number; createdAtUtc: string; telegramUserId?: number | null };
export type AdminBusiness = { id: string; name: string; city: string | null; logoUrl: string | null; moderationStatus: number; isHidden: boolean; isDeleted: boolean; campaignsCount: number; completedDealsCount: number; createdAtUtc: string; telegramUserId?: number | null };
export type PendingBusiness = { id: string; name: string; city: string | null; description: string | null; moderationStatus: number; createdAtUtc: string };
export type AdminCampaign = { id: string; title: string; businessName: string; status: number; isPromoted: boolean; applicationsCount: number; createdAtUtc: string; businessTelegramUserId?: number | null };
export type ModerationDecision = "approve" | "needs-changes" | "reject";

export type ComplaintStatus = "open" | "resolved" | "dismissed";
export const complaintStatuses: ComplaintStatus[] = ["open", "resolved", "dismissed"];
export type AdminComplaint = {
  id: string;
  targetType: "blogger" | "brandFace" | "business" | "campaign" | "offer" | "deal";
  targetId: string;
  targetName: string | null;
  reason: string;
  comment: string | null;
  status: ComplaintStatus;
  reporterTelegramUserId: number;
  reporterName: string | null;
  createdAtUtc: string;
  resolvedAtUtc: string | null;
  resolutionNote: string | null;
  ownerTelegramUserId: number | null;
  ownerIsBlocked: boolean;
  ownerIsPlatformOwner: boolean;
};
export const maxComplaintNote = 500;

export type DealFilter = "all" | "active" | "completed" | "cancelled";
export const dealFilters: DealFilter[] = ["all", "active", "completed", "cancelled"];
export type AdminDeal = {
  id: string;
  title: string | null;
  fromOffer: boolean;
  businessName: string;
  businessTelegramUserId: number;
  creatorRole: "blogger" | "brandFace";
  creatorName: string;
  creatorTelegramUserId: number;
  status: number;
  agreedPrice: number | null;
  createdAtUtc: string;
  completedAtUtc: string | null;
};

export type AuditEntry = { id: string; actorTelegramUserId: number; action: string; targetType: string; targetId: string; details: string | null; createdAtUtc: string };
export type CspReport = { directive: string; blocked: string; source: string; count: number; firstSeenAtUtc: string; lastSeenAtUtc: string };

const query = (values: Record<string, string | number | undefined>) => {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) if (value !== undefined && value !== "") params.set(key, String(value));
  return params.toString();
};

export const getUsers = (search: string, filter: UserFilter) =>
  api<AdminUser[]>(`/api/admin/users?${query({ take: 200, search: search.trim(), filter: filter === "all" ? undefined : filter })}`);
export const getUser = (telegramUserId: number) => api<AdminUserDetails>(`/api/admin/users/${telegramUserId}`);
export const setUserRole = (telegramUserId: number, role: number) => api<AdminUser>(`/api/admin/users/${telegramUserId}/role`, { method: "PATCH", body: { role } });
export const setUserBlocked = (telegramUserId: number, isBlocked: boolean) => api<AdminUser>(`/api/admin/users/${telegramUserId}/blocked`, { method: "PATCH", body: { isBlocked } });

export const getBloggers = () => api<AdminBlogger[]>("/api/admin/bloggers?take=100");
export const getPendingBloggers = () => api<AdminBlogger[]>("/api/admin/bloggers/pending?take=100");
export const moderateBlogger = (id: string, decision: ModerationDecision) => api<AdminBlogger>(`/api/admin/bloggers/${id}/${decision}`, { method: "POST" });
export const getBusinesses = () => api<AdminBusiness[]>("/api/admin/businesses?take=100");
export const getPendingBusinesses = () => api<PendingBusiness[]>("/api/admin/businesses/pending");
export const moderateBusiness = (id: string, decision: ModerationDecision) => api<PendingBusiness>(`/api/admin/businesses/${id}/${decision}`, { method: "POST" });

export const getCampaigns = () => api<AdminCampaign[]>("/api/admin/campaigns?take=100");
export const moderateCampaign = (id: string, change: { status?: number; isPromoted?: boolean }) =>
  api<AdminCampaign>(`/api/admin/campaigns/${id}`, { method: "PATCH", body: change });

export const getComplaints = (status: ComplaintStatus) => api<AdminComplaint[]>(`/api/admin/complaints?${query({ status, take: 100 })}`);
export const closeComplaint = (id: string, status: Exclude<ComplaintStatus, "open">, note: string) =>
  api<unknown>(`/api/admin/complaints/${id}`, { method: "PATCH", body: { status, note: note.trim() || null } });

export const getDeals = (filter: DealFilter) => api<AdminDeal[]>(`/api/admin/deals?${query({ take: 200, status: filter === "all" ? undefined : filter })}`);
// Tashkent days, both included; without them the newest entries.
export const getAuditLog = (from?: string, to?: string) => api<AuditEntry[]>(`/api/admin/audit-logs?${query({ take: 500, from, to })}`);

export type ComplaintReview = {
  complaint: AdminComplaint;
  accusedTelegramUserId: number | null;
  accused: AdminUserDetails | null;
  reviews: { count: number; average: number | null; recent: { rating: number; comment: string | null; reviewerName: string | null; createdAtUtc: string; isPublished: boolean }[] };
  otherComplaints: AdminComplaint[];
  reporter: {
    person: { telegramUserId: number; firstName: string; username: string | null; createdAtUtc: string; isBlocked: boolean; isDeleted: boolean } | null;
    telegramUserId: number;
    filed: number;
    open: number;
    resolved: number;
    dismissed: number;
  };
  sharedHistory: { kind: "deal" | "offer"; id: string; title: string | null; status: number; createdAtUtc: string }[];
};
export const getComplaintReview = (id: string) => api<ComplaintReview>(`/api/admin/complaints/${id}/review`);
export const getCspReports = () => api<CspReport[]>("/api/admin/csp-reports");
export const signOutEverywhere = () => api<void>("/api/admin-web/logout-all", { method: "POST" });

// The journal entry for a CSV export, written before the browser saves the file.
export type ExportKind = "users" | "deals" | "complaints" | "journal" | "daily" | "money" | "cities" | "categories" | "sources" | "funnel";
export const recordExport = (kind: ExportKind, rows: number) => api<void>("/api/admin-web/exports", { method: "POST", body: { kind, rows } });
