import { describe, expect, it } from "vitest";
import ru from "../src/i18n/ru.json";
import uz from "../src/i18n/uz.json";
import { navigation, type NavItem } from "../src/layout/navigation";
import { complaintStatuses, dealFilters, platformRoles, userFilters } from "../src/api/admin";
import { dictionaries } from "../src/i18n";
import { formatTashkentDate, formatTashkentDateTime, formatTashkentTime } from "../src/lib/time";

const flatten = (items: NavItem[]): NavItem[] => items.flatMap((item) => [item, ...flatten(item.children ?? [])]);

describe("Admin web texts", () => {
  it("have the same keys in Russian and Uzbek, including every menu item", () => {
    expect(Object.keys(uz).sort()).toEqual(Object.keys(ru).sort());
    for (const item of flatten(navigation)) {
      expect(ru).toHaveProperty([item.labelKey]);
      expect(uz).toHaveProperty([item.labelKey]);
    }
  });

  it("show times in Tashkent", () => {
    expect(formatTashkentTime("2026-10-15T19:05:00Z")).toBe("00:05");
    expect(formatTashkentTime("not a date")).toBe("");
    expect(formatTashkentDateTime("2026-10-15T19:05:00Z")).toBe("16.10.2026 00:05");
    expect(formatTashkentDate("2026-12-31T18:59:00Z")).toBe("31.12.2026");
    expect(formatTashkentDate(null)).toBe("");
  });

  // Keys the pages build from values (a status, a decision): the i18n audit cannot see them, so they are listed here.
  it("have every text that the pages pick by value, in both languages", () => {
    const keys = [
      ...platformRoles.map((role) => `admin.role.${role}`),
      ...userFilters.map((filter) => `admin.userFilter.${filter}`),
      ...complaintStatuses.map((status) => `admin.complaintStatus.${status}`),
      ...["blogger", "brandFace", "business", "campaign", "offer", "deal"].map((target) => `admin.complaintTarget.${target}`),
      ...["spam", "fraud", "fake", "offensive", "noShow", "other"].map((reason) => `report.reason.${reason}`),
      ...["blogger", "brandFace", "business"].map((role) => `admin.userProfileRole.${role}`),
      ...[0, 1, 2, 3, 4].map((status) => `admin.moderation.${status}`),
      ...[0, 1, 2, 3].map((status) => `admin.campaignStatus.${status}`),
      ...dealFilters.map((filter) => `deals.filter.${filter}`),
      ...["active", "completed", "cancelled"].map((state) => `deals.state.${state}`),
      ...["approve", "needs-changes", "reject"].flatMap((decision) => [`moderation.${decision}`, `moderation.${decision}.title`, `moderation.${decision}.text.blogger`, `moderation.${decision}.text.business`, `moderation.${decision}.done`]),
      ...["resolved", "dismissed"].flatMap((status) => [`complaints.${status}`, `complaints.${status}.title`, `complaints.${status}.text`, `complaints.${status}.done`]),
      ...["all", "users", "moderation", "complaints", "signIns"].map((group) => `journal.group.${group}`),
      ...["Campaign", "Complaint", "BloggerProfile", "BusinessProfile", "BrandFaceProfile"].map((type) => `journal.target.${type}`),
      ...["platform-user.role-updated", "platform-user.blocked", "platform-user.unblocked", "complaint.resolved", "complaint.dismissed", "campaign.status-changed", "campaign.promoted",
        "campaign.promotion-removed", "blogger-profile.approved", "blogger-profile.rejected", "blogger-profile.needs-changes", "business-profile.approved", "business-profile.rejected",
        "business-profile.needs-changes", "admin-web.signed-in", "admin-web.signed-out", "admin-web.sign-in-denied", "admin-web.signed-out-everywhere"].map((action) => `admin.log.${action}`)
    ];
    for (const language of ["ru", "uz"] as const) {
      expect(keys.filter((key) => !dictionaries[language][key])).toEqual([]);
    }
  });
});
