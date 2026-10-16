// Phone screen check: every main screen at phone widths, light and dark, RU and UZ, against a running API with the
// development seed (DevelopmentData:Seed) and the Vite dev server. It fails when something sticks out of the screen,
// a label is cut off, or text is too faint to read (WCAG contrast below 3:1). Run by CI (job "Phone screen check");
// locally: start the API and `npm run dev`, then
//   SCREEN_CHECK_BOT_TOKEN=<the API's Telegram:BotToken> SCREEN_CHECK_WEBHOOK_SECRET=<its webhook secret> npm --workspace frontend run screen-check
// Only for local and CI test instances: the token signs fake Telegram logins, never use production values.
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "@playwright/test";

const BASE = process.env.SCREEN_CHECK_BASE_URL ?? "http://127.0.0.1:5173";
const BOT_TOKEN = process.env.SCREEN_CHECK_BOT_TOKEN;
const WEBHOOK_SECRET = process.env.SCREEN_CHECK_WEBHOOK_SECRET;
const WIDTHS = (process.env.SCREEN_CHECK_WIDTHS ?? "360,320").split(",").map(Number);
const FILTER = process.env.SCREEN_CHECK_FILTER;
const OUT = process.env.SCREEN_CHECK_OUT ?? path.join(path.dirname(fileURLToPath(import.meta.url)), "output");
// Phones render with SF Pro / Roboto; headless Linux would fall back to the much wider DejaVu Sans.
const FONT = process.env.SCREEN_CHECK_FONT ?? '"Liberation Sans", Arial, sans-serif';
if (!BOT_TOKEN) throw new Error("SCREEN_CHECK_BOT_TOKEN is required (the test API's bot token).");
fs.mkdirSync(OUT, { recursive: true });

// Seeded profiles (DevelopmentDataSeeder) and the seeded owner, who makes the business user an admin.
const users = {
  blogger: { id: 50000000313, first_name: "Madina", username: "madina_blog" },
  business: { id: 60000000003, first_name: "Beeline Uzbekistan", username: "beeline_uz" },
  brandFace: { id: 70000000000, first_name: "Aziza", username: "aziza_face" }
};
const owner = { id: 5044343262, first_name: "Owner", username: "umidkb" };
const roleNames = { blogger: "Blogger", business: "Business", brandFace: "BrandFace" };

function initData(user, language) {
  const fields = {
    auth_date: String(Math.floor(Date.now() / 1000)),
    query_id: "screen-check",
    user: JSON.stringify({ ...user, language_code: language, allows_write_to_pm: true })
  };
  const check = Object.keys(fields).sort().map((key) => `${key}=${fields[key]}`).join("\n");
  const secret = crypto.createHmac("sha256", "WebAppData").update(BOT_TOKEN).digest();
  const hash = crypto.createHmac("sha256", secret).update(check).digest("hex");
  return new URLSearchParams({ ...fields, hash }).toString();
}

async function api(user, method, url, body) {
  const response = await fetch(`${BASE}${url}`, {
    method,
    headers: { Authorization: `tma ${initData(user, "ru")}`, "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  const text = await response.text();
  if (!response.ok) throw new Error(`${method} ${url} -> ${response.status} ${text.slice(0, 200)}`);
  return text ? JSON.parse(text) : null;
}

// The phone is confirmed the way the bot does it: a contact the user shares about themselves (D41).
async function verifyPhone(user, phone) {
  const response = await fetch(`${BASE}/api/webhooks/telegram`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Telegram-Bot-Api-Secret-Token": WEBHOOK_SECRET },
    body: JSON.stringify({ message: { from: { id: user.id, first_name: user.first_name }, chat: { id: user.id }, contact: { phone_number: phone, user_id: user.id } } })
  });
  if (!response.ok) throw new Error(`webhook contact -> ${response.status}`);
}

async function prepare() {
  let index = 0;
  for (const [role, user] of Object.entries(users)) {
    const me = await api(user, "GET", "/api/users/me");
    if (!me.verifiedPhone) {
      if (!WEBHOOK_SECRET) throw new Error(`${role} has no confirmed phone; set SCREEN_CHECK_WEBHOOK_SECRET.`);
      await verifyPhone(user, `+99890000000${index}`);
    }
    index += 1;
    if (me.selectedMarketplaceRole !== roleNames[role]) await api(user, "PUT", "/api/users/me/selected-role", { role: roleNames[role] });
  }
  await api(owner, "GET", "/api/users/me");
  await api(owner, "PATCH", `/api/admin/users/${users.business.id}/role`, { role: 3 });
  const blogger = await api(users.blogger, "GET", "/api/bloggers/me");
  const business = await api(users.business, "GET", "/api/businesses/me");
  const brandFace = await api(users.brandFace, "GET", "/api/brand-faces/me");
  const myCampaigns = await api(users.business, "GET", "/api/campaigns/mine?pageSize=20");
  const campaign = myCampaigns.items.find((item) => item.applicationsCount > 0) ?? myCampaigns.items[0];

  // An active deal from an offer with a long message and a publication, so the deal page has every block.
  let activeDealId = null;
  try {
    const offer = await api(users.business, "POST", "/api/offers", {
      bloggerId: blogger.id, format: "integration", offeredBudget: 12500000,
      deadline: new Date(Date.now() + 10 * 86400000).toISOString().slice(0, 10),
      message: "Здравствуйте! Предлагаем долгосрочное сотрудничество: серия из трёх интеграций в Reels и Stories, обязательная отметка аккаунта, ссылка в шапке профиля на 48 часов."
    });
    const decision = await api(users.blogger, "POST", `/api/offers/mine/${offer.id}/accept`);
    activeDealId = decision.dealId;
    await api(users.blogger, "POST", `/api/deals/me/${activeDealId}/publications`, { url: "https://www.instagram.com/reel/C9xYzAbCdEfGhIjKlMnOpQrStUvWxYz0123456789/", views: 1234567 });
  } catch (error) {
    console.log("offer setup skipped:", error.message);
  }

  // A pending offer to the brand face, and a complaint with the longest reason for the admin queue.
  let pendingOfferId = null;
  try {
    pendingOfferId = (await api(users.business, "POST", "/api/offers", {
      brandFaceId: brandFace.id, format: "photoShoot", offeredBudget: 3000000,
      deadline: new Date(Date.now() + 5 * 86400000).toISOString().slice(0, 10), message: "Фотосессия для новой коллекции."
    })).id;
  } catch (error) { console.log("brand face offer skipped:", error.message); }
  try {
    await api(users.blogger, "POST", "/api/complaints", { targetType: "campaign", targetId: campaign.id, reason: "spam", comment: "Очень длинный комментарий к жалобе, чтобы проверить перенос строк на узком экране телефона и в тёмной теме." });
  } catch (error) { console.log("complaint skipped:", error.message); }

  if (!pendingOfferId) pendingOfferId = (await api(users.brandFace, "GET", "/api/offers/mine")).find((offer) => offer.state === "pending")?.id ?? null;
  const bloggerDeals = await api(users.blogger, "GET", "/api/deals/me");
  const completedDeal = bloggerDeals.find((deal) => deal.status === 1) ?? bloggerDeals[0];
  activeDealId = activeDealId ?? bloggerDeals.find((deal) => deal.status === 0)?.id ?? null;
  const applications = await api(users.blogger, "GET", "/api/campaign-applications/mine?pageSize=10");
  const application = (applications.items ?? applications)[0];
  const catalogBlogger = (await api(users.business, "GET", "/api/bloggers?pageSize=5")).bloggers?.[0] ?? null;
  const catalogBrandFace = (await api(users.business, "GET", "/api/brand-faces/catalog?pageSize=5")).items?.[0] ?? null;
  const catalogCampaign = (await api(users.blogger, "GET", "/api/campaigns/catalog?pageSize=5")).items?.[0] ?? null;
  return { blogger, business, brandFace, campaign, activeDealId, completedDeal, application, catalogBlogger, catalogBrandFace, catalogCampaign, pendingOfferId };
}

function routesFor(role, data) {
  const common = ["/", "/search", "/campaigns", "/requests", "/profile", "/settings", "/favorites"];
  const details = [
    data.catalogBlogger && `/blogger/${data.catalogBlogger.id}`,
    data.catalogBrandFace && `/brand-face-detail/${data.catalogBrandFace.id}`,
    `/company/${data.business.id}`,
    data.catalogCampaign && `/campaign/${data.catalogCampaign.id}`
  ].filter(Boolean);
  if (role === "blogger") return [...common, ...details, "/blogger-form",
    data.activeDealId && `/deal/${data.activeDealId}`, data.completedDeal && `/deal/${data.completedDeal.id}`,
    data.application && `/my-application/${data.application.id}`].filter(Boolean);
  if (role === "business") return [...common, ...details, "/business", "/my-campaigns", `/my-campaign/${data.campaign.id}`,
    `/my-campaign-applications/${data.campaign.id}`, `/my-campaign-edit/${data.campaign.id}`,
    data.activeDealId && `/deal/${data.activeDealId}`,
    "/admin", "/admin/complaints", "/admin/bloggers", "/admin/businesses",
    `/admin/user?id=${users.blogger.id}`].filter(Boolean);
  return [...common, ...details, "/brand-face", data.pendingOfferId && `/offer/${data.pendingOfferId}`].filter(Boolean);
}

// Runs in the page: elements that stick out of the viewport (not inside a sideways-scrolling rail) and clipped labels.
function inspect() {
  const viewport = document.documentElement.clientWidth;
  const scrollsSideways = (element) => {
    for (let node = element.parentElement; node && node !== document.body; node = node.parentElement) {
      const style = getComputedStyle(node);
      if (style.overflowX === "auto" || style.overflowX === "scroll") return true;
    }
    return false;
  };
  const label = (element) => {
    const name = element.tagName.toLowerCase();
    const cls = typeof element.className === "string" ? element.className.trim().split(/\s+/).slice(0, 3).join(".") : "";
    const text = (element.innerText || element.getAttribute("aria-label") || "").replace(/\s+/g, " ").trim().slice(0, 60);
    return `${name}${cls ? "." + cls : ""} "${text}"`;
  };
  const outside = [];
  for (const element of document.querySelectorAll("body *")) {
    const style = getComputedStyle(element);
    if (style.display === "none" || style.visibility === "hidden" || style.position === "fixed") continue;
    const rect = element.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) continue;
    if ((rect.right > viewport + 1 || rect.left < -1) && !scrollsSideways(element)) outside.push({ element, rect });
  }
  // Report the outermost element of each overflowing subtree, with how far it sticks out.
  const roots = outside.filter(({ element }) => !outside.some((other) => other.element !== element && other.element.contains(element)));
  const clipped = [];
  for (const element of document.querySelectorAll("button, a, [role=button], .ds-badge, h1, h2, h3")) {
    if (element.closest(".sr-only")) continue;
    const style = getComputedStyle(element);
    if (style.display === "none" || style.visibility === "hidden") continue;
    if (element.scrollWidth > element.clientWidth + 1 && style.textOverflow !== "ellipsis" && style.overflow !== "visible" && style.overflowX !== "visible") {
      clipped.push(label(element));
    }
  }
  // Text wider than its own block spills onto its neighbour (like two bottom-nav labels running together).
  const spilled = [];
  for (const element of document.querySelectorAll("span, p, strong, a, button, label, dd, dt, h1, h2, h3, li")) {
    if (element.closest(".sr-only")) continue;
    const style = getComputedStyle(element);
    if (style.display === "none" || style.display === "inline" || style.visibility === "hidden" || style.textOverflow === "ellipsis") continue;
    if (style.overflowX === "auto" || style.overflowX === "scroll" || scrollsSideways(element)) continue;
    if (!element.innerText || !element.innerText.trim()) continue;
    if (element.scrollWidth > element.clientWidth + 2 && element.clientWidth > 0) spilled.push(`${label(element)} (+${element.scrollWidth - element.clientWidth}px)`);
  }
  // Text that is hard to read: WCAG contrast between the text colour and the background actually behind it.
  const parse = (value) => {
    const match = value.match(/rgba?\(([^)]+)\)/);
    if (!match) return null;
    const [r, g, b, a = 1] = match[1].split(/[ ,/]+/).filter(Boolean).map(Number);
    return [r, g, b, a];
  };
  const over = (top, bottom) => [0, 1, 2].map((index) => top[index] * top[3] + bottom[index] * (1 - top[3])).concat(1);
  const luminance = ([r, g, b]) => {
    const channel = (value) => { const v = value / 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; };
    return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
  };
  const backgroundOf = (element) => {
    const layers = [];
    for (let node = element; node; node = node.parentElement) {
      const color = parse(getComputedStyle(node).backgroundColor);
      if (color && color[3] > 0) { layers.push(color); if (color[3] >= 1) break; }
    }
    let result = [255, 255, 255, 1];
    for (const layer of layers.reverse()) result = over(layer, result);
    return result;
  };
  const faint = [];
  const seen = new Set();
  for (const element of document.querySelectorAll("body *")) {
    if (element.closest(".sr-only, [aria-hidden=true], svg")) continue;
    const ownText = [...element.childNodes].filter((node) => node.nodeType === 3).map((node) => node.textContent).join("").trim();
    if (!ownText) continue;
    const style = getComputedStyle(element);
    if (style.visibility === "hidden" || style.display === "none" || Number(style.opacity) < 1) continue;
    if (element.closest("[disabled], [aria-disabled=true], input, textarea, select")) continue;
    const rect = element.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) continue;
    const color = parse(style.color);
    if (!color) continue;
    const background = backgroundOf(element);
    const text = over(color, background);
    const [light, dark] = [luminance(text), luminance(background)].sort((x, y) => y - x);
    const ratio = (light + 0.05) / (dark + 0.05);
    if (ratio < 3) {
      const key = `${label(element)}`;
      if (!seen.has(key)) { seen.add(key); faint.push(`${key} contrast ${ratio.toFixed(2)}`); }
    }
  }
  return {
    pageScroll: document.documentElement.scrollWidth - viewport,
    outside: roots.slice(0, 8).map(({ element, rect }) => `${label(element)} → ${Math.round(Math.max(rect.right - viewport, -rect.left))}px`),
    clipped: [...clipped, ...spilled].slice(0, 8),
    faint: faint.slice(0, 8)
  };
}

async function run() {
  const data = await prepare();
  const browser = await chromium.launch(process.env.SCREEN_CHECK_CHROMIUM ? { executablePath: process.env.SCREEN_CHECK_CHROMIUM } : {});
  const findings = [];
  let screens = 0;
  const variants = ["blogger", "business", "brandFace"].flatMap((role) => ["ru", "uz"].flatMap((language) =>
    ["light", "dark"].flatMap((theme) => WIDTHS.map((width) => ({ role, language, theme, width })))));
  for (const { role, language, theme, width } of variants) {
    const context = await browser.newContext({ viewport: { width, height: 760 }, deviceScaleFactor: 2, colorScheme: theme, hasTouch: true, isMobile: true });
    const telegramUser = users[role];
    const telegramInitData = initData(telegramUser, language);
    await context.addInitScript(({ telegramInitData, telegramUser, language, theme, FONT }) => {
      localStorage.setItem("bloggerbazar.language", language);
      localStorage.setItem("bloggerbazar.theme", theme);
      localStorage.setItem("bloggerbazar.onboarding.completed", "true");
      localStorage.setItem("bloggerbazar.onboarding.welcomeViewed", "true");
      localStorage.setItem("bloggerbazar.writeAccessRequested", "true");
      // Phones render with SF Pro / Roboto; headless Linux would fall back to the much wider DejaVu Sans.
      document.addEventListener("DOMContentLoaded", () => {
        const style = document.createElement("style");
        style.textContent = `:root, body, button, input, select, textarea { font-family: ${FONT} !important; }`;
        document.head.appendChild(style);
      });
      const noop = () => {};
      const button = { show: noop, hide: noop, onClick: noop, offClick: noop, setText: noop, setParams: noop, enable: noop, disable: noop, isVisible: false };
      window.Telegram = { WebApp: {
        initData: telegramInitData, initDataUnsafe: { user: { ...telegramUser, language_code: language, allows_write_to_pm: true } },
        platform: "ios", version: "8.0", colorScheme: theme, themeParams: {}, viewportHeight: 760, viewportStableHeight: 760,
        safeAreaInset: { top: 0, bottom: 0, left: 0, right: 0 }, contentSafeAreaInset: { top: 0, bottom: 0, left: 0, right: 0 },
        ready: noop, expand: noop, close: noop, onEvent: noop, offEvent: noop, isVersionAtLeast: () => true,
        setHeaderColor: noop, setBackgroundColor: noop, setBottomBarColor: noop, disableVerticalSwipes: noop, requestFullscreen: noop,
        openLink: noop, openTelegramLink: noop, requestWriteAccess: (callback) => callback && callback(true), requestContact: (callback) => callback && callback(true),
        HapticFeedback: { impactOccurred: noop, notificationOccurred: noop, selectionChanged: noop },
        BackButton: button, MainButton: button, SettingsButton: button, ClosingBehavior: { enableConfirmation: noop, disableConfirmation: noop }
      } };
    }, { telegramInitData, telegramUser, language, theme, FONT });
    const page = await context.newPage();
    await page.route("https://telegram.org/**", (route) => route.fulfill({ status: 200, contentType: "application/javascript", body: "" }));
    const errors = [];
    page.on("pageerror", (error) => errors.push(error.message));
    await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
    for (const route of routesFor(role, data).filter((item) => !FILTER || item.includes(FILTER))) {
      await page.evaluate((hash) => { window.location.hash = hash; }, route);
      await page.waitForLoadState("networkidle").catch(() => {});
      await page.waitForTimeout(450);
      const result = await page.evaluate(inspect);
      const name = `${width}-${role}-${language}-${theme}-${route.replace(/[^a-z0-9]+/gi, "_").replace(/^_|_$/g, "") || "home"}`;
      screens += 1;
      if (result.pageScroll > 0 || result.outside.length || result.clipped.length || result.faint.length || errors.length) {
        await page.screenshot({ path: path.join(OUT, `${name}.png`), fullPage: true });
        findings.push({ name, route, ...result, errors: [...errors] });
      }
      errors.length = 0;
    }
    await context.close();
  }
  await browser.close();
  fs.writeFileSync(path.join(OUT, "findings.json"), JSON.stringify(findings, null, 2));
  console.log(`screens checked: ${screens}, with problems: ${findings.length}`);
  for (const finding of findings) {
    console.log(`\n# ${finding.name}  (page scroll ${finding.pageScroll}px)`);
    for (const item of finding.outside) console.log(`  outside: ${item}`);
    for (const item of finding.clipped) console.log(`  clipped: ${item}`);
    for (const item of finding.faint) console.log(`  faint: ${item}`);
    for (const item of finding.errors) console.log(`  error: ${item}`);
  }
  return findings.length > 0;
}

run().then((failed) => { process.exitCode = failed ? 1 : 0; }).catch((error) => { console.error(error); process.exit(1); });
