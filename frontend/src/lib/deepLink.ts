// Bot messages open the Mini App with ?bb_route=<route>. Only these routes are accepted: a deal or offer by id,
// and the fixed screens the bot tips (D54) and the complaint alert (D52) point to.
const entityRoute = /^\/(deal|offer)\/[0-9a-f-]{36}$/i;
const screenRoutes = new Set(["/", "/search", "/campaigns", "/requests", "/profile", "/settings", "/favorites", "/my-campaigns", "/blogger-form", "/business", "/brand-face", "/admin"]);

export function isDeepLinkRoute(route: string | null | undefined): route is string {
  return Boolean(route && (entityRoute.test(route) || screenRoutes.has(route)));
}
