// The admin site's sections in menu order. Only those with a path are open; the rest arrive in later phases
// (lists and actions, exports) and show as "soon" until then.
export type NavItem = { id: string; labelKey: string; path?: string; children?: NavItem[] };

export const navigation: NavItem[] = [
  { id: "overview", labelKey: "nav.overview", path: "/" },
  { id: "analytics", labelKey: "nav.analytics", path: "/analytics" },
  { id: "funnel", labelKey: "nav.funnel", path: "/funnel" },
  { id: "users", labelKey: "nav.users" },
  {
    id: "moderation",
    labelKey: "nav.moderation",
    children: [
      { id: "moderation-bloggers", labelKey: "nav.moderationBloggers" },
      { id: "moderation-businesses", labelKey: "nav.moderationBusinesses" },
      { id: "moderation-campaigns", labelKey: "nav.moderationCampaigns" }
    ]
  },
  { id: "complaints", labelKey: "nav.complaints" },
  { id: "deals", labelKey: "nav.deals" },
  { id: "audit-log", labelKey: "nav.auditLog" },
  { id: "security", labelKey: "nav.security" }
];
