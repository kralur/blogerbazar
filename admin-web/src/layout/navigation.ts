// The admin site's sections in menu order. Only those with a path are open; a section without one shows as "soon".
export type NavItem = { id: string; labelKey: string; path?: string; children?: NavItem[] };

export const navigation: NavItem[] = [
  { id: "overview", labelKey: "nav.overview", path: "/" },
  { id: "analytics", labelKey: "nav.analytics", path: "/analytics" },
  { id: "funnel", labelKey: "nav.funnel", path: "/funnel" },
  { id: "users", labelKey: "nav.users", path: "/users" },
  {
    id: "moderation",
    labelKey: "nav.moderation",
    children: [
      { id: "moderation-bloggers", labelKey: "nav.moderationBloggers", path: "/moderation/bloggers" },
      { id: "moderation-businesses", labelKey: "nav.moderationBusinesses", path: "/moderation/businesses" },
      { id: "moderation-campaigns", labelKey: "nav.moderationCampaigns", path: "/moderation/campaigns" }
    ]
  },
  { id: "complaints", labelKey: "nav.complaints", path: "/complaints" },
  { id: "deals", labelKey: "nav.deals", path: "/deals" },
  { id: "audit-log", labelKey: "nav.auditLog", path: "/audit-log" },
  { id: "security", labelKey: "nav.security", path: "/security" }
];
