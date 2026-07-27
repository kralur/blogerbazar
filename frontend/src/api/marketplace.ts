import { api } from "./client";
import type { BloggerCardData } from "../components/BloggerCard";
import type { CampaignCardData } from "../components/CampaignCard";

export type PlatformDetails = {
  id: string;
  type: "instagram" | "telegram" | "tiktok" | "youtube" | "threads";
  followers: number;
  url?: string | null;
};

export type PortfolioDetails = { id: string; title: string; type: "IMAGE" | "VIDEO"; url: string };

export type BloggerDetails = BloggerCardData & {
  username?: string | null;
  coverUrl?: string | null;
  averageReach: number;
  engagementRate: number;
  storiesPrice: number;
  reelsPrice: number;
  postPrice: number;
  integrationPrice: number;
  barterEnabled: boolean;
  bio?: string | null;
  platforms: PlatformDetails[];
  portfolioItems: PortfolioDetails[];
};

export type CampaignDetails = CampaignCardData & {
  businessId: string;
  company: string;
  logo: string;
  city: string;
  budgetFrom: number;
  budgetTo: number;
  date: string;
  applicants: number;
  requirements: string[];
};

type ApiBlogger = {
  id: string;
  name: string;
  username?: string | null;
  city: string;
  categories: string[];
  bio?: string | null;
  avatarUrl?: string | null;
  coverUrl?: string | null;
  totalFollowers: number;
  averageReach?: number | null;
  engagementRate?: number | null;
  storiesPrice?: number | null;
  reelsPrice?: number | null;
  postPrice?: number | null;
  integrationPrice?: number | null;
  barterEnabled: boolean;
  isVerified: boolean;
  isPromoted: boolean;
  rating?: number | null;
  reviewsCount: number;
  completedDealsCount: number;
  portfolioItems?: Array<{ id: string; title: string; type: number; url: string }>;
  platforms?: Array<{ id: string; type: string; url: string; followers?: number | null; screenshotUrl?: string | null }>;
};

type ApiCampaign = {
  id: string;
  businessId: string;
  businessName: string;
  title: string;
  description: string;
  city?: string | null;
  categories: string[];
  budgetFrom?: number | null;
  budgetTo?: number | null;
  isPromoted: boolean;
  createdAtUtc: string;
};

export type ContactUnlockOrder = {
  reference: string;
  targetType: number;
  targetId: string;
  amountUzs: number;
  status: number;
  isUnlocked: boolean;
};

export type TelegramInvoiceLink = { reference: string; invoiceLink: string };

export type ContactDetails = { phone?: string | null; email?: string | null };
export type BloggerReview = { id: string; rating: number; comment?: string | null; reviewerName?: string | null; createdAtUtc: string };
export type MyBusinessProfile = {
  id: string;
  name: string;
  username?: string | null;
  city?: string | null;
  logoUrl?: string | null;
  description?: string | null;
  phone?: string | null;
  email?: string | null;
  isVerified: boolean;
};
export type MyBloggerProfile = {
  id: string;
  name: string;
  lastName?: string | null;
  username?: string | null;
  city: string;
  categories: string[];
  bio?: string | null;
  avatarUrl?: string | null;
  phone?: string | null;
  email?: string | null;
  totalFollowers: number;
  averageReach?: number | null;
  engagementRate?: number | null;
  storiesPrice?: number | null;
  reelsPrice?: number | null;
  postPrice?: number | null;
  integrationPrice?: number | null;
  barterEnabled: boolean;
  portfolioItems: Array<{ id: string; title: string; type: number; url: string }>;
};

export type BloggerProfileInput = {
  name: string;
  lastName?: string;
  username?: string;
  city: string;
  categories: string[];
  bio?: string;
  phone?: string;
  email?: string;
  totalFollowers: number;
  averageReach?: number;
  engagementRate?: number;
  storiesPrice?: number;
  reelsPrice?: number;
  postPrice?: number;
  integrationPrice?: number;
  barterEnabled: boolean;
  portfolioItems: Array<{ title: string; type: "IMAGE" | "VIDEO"; url: string }>;
};

const asBloggerCard = (blogger: ApiBlogger): BloggerCardData => ({
  id: blogger.id,
  name: blogger.name,
  city: blogger.city,
  categories: blogger.categories,
  totalFollowers: blogger.totalFollowers,
  priceFrom: blogger.storiesPrice ?? blogger.reelsPrice ?? blogger.postPrice ?? blogger.integrationPrice,
  rating: blogger.rating ?? null,
  reviewsCount: blogger.reviewsCount,
  completedDealsCount: blogger.completedDealsCount,
  contactUnlocked: false,
  avatarUrl: blogger.avatarUrl,
  verified: blogger.isVerified,
  averageReach: blogger.averageReach,
  engagementRate: blogger.engagementRate,
  isPromoted: blogger.isPromoted
});

const asBloggerDetails = (blogger: ApiBlogger): BloggerDetails => ({
  ...asBloggerCard(blogger),
  username: blogger.username,
  verified: blogger.isVerified,
  averageReach: blogger.averageReach ?? 0,
  engagementRate: blogger.engagementRate ?? 0,
  storiesPrice: blogger.storiesPrice ?? 0,
  reelsPrice: blogger.reelsPrice ?? 0,
  postPrice: blogger.postPrice ?? 0,
  integrationPrice: blogger.integrationPrice ?? 0,
  barterEnabled: blogger.barterEnabled,
  bio: blogger.bio,
  rating: blogger.rating ?? null,
  platforms: (blogger.platforms ?? []).flatMap((platform) => {
    const type = platform.type.toLowerCase();
    return isDisplayPlatformType(type) ? [{ id: platform.id, type, url: platform.url, followers: platform.followers ?? 0 }] : [];
  }),
  portfolioItems: (blogger.portfolioItems ?? []).map((item) => ({ id: item.id, title: item.title, type: item.type === 1 ? "VIDEO" : "IMAGE", url: item.url }))
});

const asCampaignCard = (campaign: ApiCampaign): CampaignCardData => ({
  id: campaign.id,
  title: campaign.title,
  description: campaign.description,
  city: campaign.city,
  categories: campaign.categories,
  budgetFrom: campaign.budgetFrom,
  budgetTo: campaign.budgetTo,
  isPromoted: campaign.isPromoted,
  business: { name: campaign.businessName }
});

const initials = (name: string) => name.split(" ").map((part) => part[0]).join("").slice(0, 2).toUpperCase();

type DisplayPlatformType = "instagram" | "telegram" | "tiktok" | "youtube" | "threads";

function isDisplayPlatformType(value: string): value is DisplayPlatformType {
  return ["instagram", "telegram", "tiktok", "youtube", "threads"].includes(value);
}

const asCampaignDetails = (campaign: ApiCampaign): CampaignDetails => ({
  ...asCampaignCard(campaign),
  businessId: campaign.businessId,
  company: campaign.businessName,
  logo: initials(campaign.businessName),
  city: campaign.city ?? "Узбекистан",
  budgetFrom: campaign.budgetFrom ?? 0,
  budgetTo: campaign.budgetTo ?? 0,
  date: new Intl.DateTimeFormat("ru-RU", { day: "numeric", month: "short" }).format(new Date(campaign.createdAtUtc)),
  applicants: 0,
  requirements: campaign.categories.length ? campaign.categories.map((category) => `Категория: ${category}`) : ["Обсудите детали с бизнесом"]
});

export async function getBloggers(): Promise<BloggerCardData[]> {
  const response = await api<{ bloggers: ApiBlogger[] }>("/api/bloggers?pageSize=3");
  return response.bloggers.map(asBloggerCard);
}

export async function getBlogger(id: string): Promise<BloggerDetails> {
  return asBloggerDetails(await api<ApiBlogger>(`/api/bloggers/${id}`));
}

export async function getBloggerReviews(id: string): Promise<BloggerReview[]> {
  return api<BloggerReview[]>(`/api/bloggers/${id}/reviews?take=20`);
}

export async function getCampaigns(): Promise<CampaignCardData[]> {
  const response = await api<{ campaigns: ApiCampaign[] }>("/api/campaigns?pageSize=20");
  return response.campaigns.map(asCampaignCard);
}

export async function getCampaign(id: string): Promise<CampaignDetails> {
  return asCampaignDetails(await api<ApiCampaign>(`/api/campaigns/${id}`));
}

export async function applyToCampaign(id: string, message: string) {
  return api(`/api/campaigns/${id}/applications`, { method: "POST", body: JSON.stringify({ message }) });
}

export type MyCampaignApplication = {
  id: string;
  campaignId: string;
  campaignTitle: string;
  counterpartyName: string;
  message?: string | null;
  status: number;
  canAccept: boolean;
  createdAtUtc: string;
};

export async function getMyCampaignApplications() {
  return api<MyCampaignApplication[]>("/api/campaign-applications/me");
}

export async function acceptCampaignApplication(id: string) {
  return api(`/api/campaign-applications/${id}/accept`, { method: "POST" });
}

export type MyDeal = {
  id: string;
  campaignApplicationId?: string | null;
  collaborationRequestId?: string | null;
  title: string;
  counterpartyName: string;
  status: number;
  createdAtUtc: string;
  completedAtUtc?: string | null;
  canComplete: boolean;
  canReview: boolean;
};

export async function getMyDeals() {
  return api<MyDeal[]>("/api/deals/me");
}

export async function completeDeal(id: string) {
  return api(`/api/deals/${id}/complete`, { method: "POST" });
}

export async function createDealReview(id: string, rating: number, comment: string) {
  return api(`/api/deals/${id}/reviews`, { method: "POST", body: JSON.stringify({ rating, comment: comment.trim() || undefined }) });
}

export async function getContactUnlockStatus(targetType: "Blogger" | "Business", targetId: string) {
  return api<{ isUnlocked: boolean }>(`/api/payments/contact-unlocks/${targetType}/${targetId}`);
}

export async function createContactUnlockOrder(targetType: "Blogger" | "Business", targetId: string) {
  return api<ContactUnlockOrder>("/api/payments/contact-unlocks", { method: "POST", body: JSON.stringify({ targetType: targetType === "Blogger" ? 0 : 1, targetId }) });
}

export async function createContactUnlockInvoice(reference: string) {
  return api<TelegramInvoiceLink>(`/api/payments/contact-unlocks/${reference}/invoice`, { method: "POST" });
}

export async function getUnlockedContact(targetType: "Blogger" | "Business", targetId: string) {
  return api<ContactDetails>(`/api/contacts/${targetType}/${targetId}`);
}

export async function createBusinessProfile(input: {
  name: string;
  username?: string;
  city?: string;
  logoUrl?: string;
  description?: string;
  phone?: string;
  email?: string;
}) {
  return api("/api/businesses", { method: "POST", body: JSON.stringify(input) });
}

export async function getMyBusinessProfile() {
  return api<MyBusinessProfile>("/api/businesses/me");
}

export async function getMyBloggerProfile() {
  return api<MyBloggerProfile>("/api/bloggers/me");
}

export async function createBloggerProfile(input: BloggerProfileInput) {
  return api("/api/bloggers", { method: "POST", body: JSON.stringify(input) });
}

export async function updateBloggerProfile(input: BloggerProfileInput) {
  return api("/api/bloggers/me", { method: "PUT", body: JSON.stringify(input) });
}

export async function updateBusinessProfile(input: {
  name: string;
  username?: string;
  city?: string;
  logoUrl?: string;
  description?: string;
  phone?: string;
  email?: string;
}) {
  return api("/api/businesses/me", { method: "PUT", body: JSON.stringify(input) });
}

export async function createCampaign(input: {
  title: string;
  description: string;
  city?: string;
  categories: string[];
  budgetFrom?: number;
  budgetTo?: number;
  publishImmediately: boolean;
}) {
  return api("/api/campaigns", { method: "POST", body: JSON.stringify(input) });
}

export type PendingBloggerProfile = {
  id: string;
  name: string;
  city: string;
  categories: string[];
  avatarUrl?: string | null;
  totalFollowers: number;
  status: number;
  createdAtUtc: string;
};

export async function getPendingBloggerProfiles() {
  return api<PendingBloggerProfile[]>("/api/admin/bloggers/pending");
}

export async function moderateBloggerProfile(id: string, decision: "approve" | "reject") {
  return api<PendingBloggerProfile>(`/api/admin/bloggers/${id}/${decision}`, { method: "POST" });
}
