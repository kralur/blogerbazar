import { describe, expect, it } from "vitest";
import { bloggerCompleteness, brandFaceCompleteness, businessCompleteness, profileCompleteness } from "../src/lib/profileCompleteness";
import type { MyBloggerProfile, MyBrandFaceProfile, MyBusinessProfile } from "../src/api/marketplace";

const blogger: MyBloggerProfile = {
  id: "b1", name: "Ali", city: "tashkent", categories: ["beauty"], totalFollowers: 1200, barterEnabled: false, status: 1, portfolioItems: [], platforms: [{ id: "p1", type: "instagram", url: "https://instagram.com/ali", followers: 1200 }]
};
const business: MyBusinessProfile = { id: "c1", name: "Cafe", isVerified: false, moderationStatus: 1 };
const brandFace: MyBrandFaceProfile = { id: "f1", name: "Dina", city: "tashkent", languages: ["uz"], categories: ["fashion"], isPromoted: false };

describe("profile completeness", () => {
  it("counts the optional blogger fields and names the first missing one", () => {
    // Reach, engagement and the stories and reels prices are required by the form, so they never count as steps.
    const empty = bloggerCompleteness({ ...blogger, averageReach: 300, engagementRate: 3, storiesPrice: 100, reelsPrice: 200 });
    expect(empty.percent).toBe(0);
    expect(empty.steps.map((step) => step.key)).toEqual(["photo", "about", "portfolio", "platforms", "prices"]);
    expect(empty.next?.key).toBe("photo");
    expect(empty.next?.href).toBe("#/blogger-form");

    const half = bloggerCompleteness({ ...blogger, avatarUrl: "https://cdn/a.webp", bio: "About me", integrationPrice: 100 });
    expect(half.percent).toBe(60);
    expect(half.next?.key).toBe("portfolio");

    const full = bloggerCompleteness({ ...blogger, avatarUrl: "https://cdn/a.webp", bio: "About me", postPrice: 50, portfolioItems: [{ id: "i", title: "t", type: 0, url: "https://x" }], platforms: [...blogger.platforms, { id: "p2", type: "telegram", url: "https://t.me/ali" }] });
    expect(full.percent).toBe(100);
    expect(full.next).toBeUndefined();
  });

  it("treats blank text as missing", () => {
    expect(bloggerCompleteness({ ...blogger, bio: "   " }).steps.find((step) => step.key === "about")?.done).toBe(false);
    expect(businessCompleteness({ ...business, websiteUrl: "  " }).steps.find((step) => step.key === "website")?.done).toBe(false);
  });

  it("counts the business logo, website, email and Telegram (name, city and description are required by the form)", () => {
    expect(businessCompleteness(business)).toMatchObject({ percent: 0, next: { key: "logo", href: "#/business" } });
    expect(businessCompleteness({ ...business, logoUrl: "https://cdn/l.webp", websiteUrl: "https://cafe.uz" })).toMatchObject({ percent: 50, next: { key: "email" } });
    expect(businessCompleteness({ ...business, logoUrl: "https://cdn/l.webp", websiteUrl: "https://cafe.uz", email: "hi@cafe.uz", username: "cafe" }).percent).toBe(100);
  });

  it("counts the brand face photo, gallery, story, showreel and price", () => {
    const empty = brandFaceCompleteness(brandFace);
    expect(empty.steps.map((step) => step.key)).toEqual(["photo", "gallery", "about", "showreel", "price"]);
    expect(empty.next).toMatchObject({ key: "photo", href: "#/brand-face" });
    const withOnePhoto = brandFaceCompleteness({ ...brandFace, avatarUrl: "https://cdn/a.webp", photoUrls: ["https://cdn/1.webp"] });
    expect(withOnePhoto.steps.find((step) => step.key === "gallery")?.done).toBe(false);
    expect(brandFaceCompleteness({ ...brandFace, avatarUrl: "https://cdn/a.webp", photoUrls: ["1", "2"], description: "Model", showreelUrl: "https://youtu.be/x", collaborationPrice: 500000 }).percent).toBe(100);
  });

  it("picks the calculator by role", () => {
    expect(profileCompleteness("Blogger", blogger).steps).toHaveLength(5);
    expect(profileCompleteness("Business", business).steps).toHaveLength(4);
    expect(profileCompleteness("BrandFace", brandFace).steps).toHaveLength(5);
  });
});
