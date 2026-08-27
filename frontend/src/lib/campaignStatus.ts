import type { MyCampaignStatus } from "../api/marketplace";

type Translate = (key: string, values?: Record<string, string | number>) => string;

export function campaignStatusLabel(status: MyCampaignStatus, t: Translate) {
  return t(`myCampaigns.status.${status}`);
}

export function campaignStatusTone(status: MyCampaignStatus) {
  if (status === 1) return "green" as const;
  if (status === 0) return "gray" as const;
  if (status === 2) return "orange" as const;
  return "red" as const;
}

export function campaignApplicationsLabel(count: number, language: "ru" | "uz", t: Translate) {
  if (language === "uz") return t("myCampaigns.applicationsOther", { count });
  const remainder = count % 100;
  if (remainder >= 11 && remainder <= 14) return t("myCampaigns.applicationsMany", { count });
  const lastDigit = count % 10;
  if (lastDigit === 1) return t("myCampaigns.applicationsOne", { count });
  if (lastDigit >= 2 && lastDigit <= 4) return t("myCampaigns.applicationsFew", { count });
  return t("myCampaigns.applicationsMany", { count });
}
