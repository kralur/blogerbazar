import type { MarketplaceRole, MyBloggerProfile, MyBrandFaceProfile, MyBusinessProfile } from "../api/marketplace";

// A step is one part of the profile the form does not insist on (the required fields are always there);
// done when it is filled in. The key names the text (home.progress.<key>), the href opens the form.
export type CompletenessStep = { key: string; done: boolean; href: string };
export type ProfileCompleteness = { percent: number; steps: CompletenessStep[]; next?: CompletenessStep };

const filled = (value?: string | null) => Boolean(value && value.trim());
const positive = (value?: number | null) => typeof value === "number" && value > 0;

function summarize(href: string, entries: Array<[string, boolean]>): ProfileCompleteness {
  const steps = entries.map(([key, done]) => ({ key, done, href }));
  const done = steps.filter((step) => step.done).length;
  return { percent: Math.round((done / steps.length) * 100), steps, next: steps.find((step) => !step.done) };
}

// Reach, engagement and the stories and reels prices are required by the form; the rest is up to the blogger.
export function bloggerCompleteness(profile: MyBloggerProfile): ProfileCompleteness {
  return summarize("#/blogger-form", [
    ["photo", filled(profile.avatarUrl)],
    ["about", filled(profile.bio)],
    ["portfolio", profile.portfolioItems.length > 0],
    ["platforms", profile.platforms.length >= 2],
    ["prices", positive(profile.postPrice) || positive(profile.integrationPrice)]
  ]);
}

// Name, city and description are required by the form.
export function businessCompleteness(profile: MyBusinessProfile): ProfileCompleteness {
  return summarize("#/business", [
    ["logo", filled(profile.logoUrl)],
    ["website", filled(profile.websiteUrl)],
    ["email", filled(profile.email)],
    ["telegram", filled(profile.username)]
  ]);
}

// Instagram, age, gender, formats, languages and categories are required by the form (D47).
export function brandFaceCompleteness(profile: MyBrandFaceProfile): ProfileCompleteness {
  return summarize("#/brand-face", [
    ["photo", filled(profile.avatarUrl)],
    ["gallery", (profile.photoUrls?.length ?? 0) >= 2],
    ["about", filled(profile.description) || filled(profile.experience)],
    ["showreel", filled(profile.showreelUrl)],
    ["price", positive(profile.collaborationPrice)]
  ]);
}

export function profileCompleteness(role: MarketplaceRole, profile: MyBloggerProfile | MyBusinessProfile | MyBrandFaceProfile): ProfileCompleteness {
  if (role === "Business") return businessCompleteness(profile as MyBusinessProfile);
  if (role === "BrandFace") return brandFaceCompleteness(profile as MyBrandFaceProfile);
  return bloggerCompleteness(profile as MyBloggerProfile);
}
