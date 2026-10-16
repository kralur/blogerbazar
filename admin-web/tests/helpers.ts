import { vi } from "vitest";
import { browser } from "../src/lib/browser";

export type FakeRoute = { status: number; body?: unknown };
export type FetchCall = { url: string; method: string; body?: unknown; credentials?: RequestCredentials };

// fetch replaced by a table of "METHOD /path" answers; every call is recorded.
export function fakeApi(routes: Record<string, FakeRoute>) {
  const calls: FetchCall[] = [];
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === "string" ? input : input instanceof URL ? input.pathname : input.url;
    const method = init?.method ?? "GET";
    calls.push({ url, method, body: init?.body ? JSON.parse(String(init.body)) : undefined, credentials: init?.credentials });
    // An exact "METHOD /path?query" wins; otherwise "METHOD /path" answers whatever the query is.
    const route = routes[`${method} ${url}`] ?? routes[`${method} ${url.split("?")[0]}`] ?? { status: 404, body: { code: "not_found" } };
    return new Response(route.body === undefined ? null : JSON.stringify(route.body), {
      status: route.status,
      headers: { "Content-Type": "application/json" }
    });
  }));
  return calls;
}

export function watchNavigation() {
  return {
    replace: vi.spyOn(browser, "replace").mockImplementation(() => undefined),
    cleanAddress: vi.spyOn(browser, "cleanAddress")
  };
}

export const session = {
  telegramUserId: 5044343262,
  firstName: "Umid",
  username: "umid",
  role: "owner",
  expiresAtUtc: "2026-10-15T16:30:00Z"
};
