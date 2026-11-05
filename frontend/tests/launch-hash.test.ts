import { describe, expect, it, vi } from "vitest";
import { normalizeLaunchHash } from "../src/lib/launchHash";

const location = (hash: string) => ({ hash, pathname: "/", search: "?bb_route=%2Fdeal%2F1" });

describe("launch hash", () => {
  it("turns Telegram's launch data in the fragment into the home route, keeping the query", () => {
    const history = { state: { a: 1 }, replaceState: vi.fn() };

    expect(normalizeLaunchHash(location("#tgWebAppData=query_id%3D1&tgWebAppVersion=8.0"), history)).toBe(true);
    expect(history.replaceState).toHaveBeenCalledWith({ a: 1 }, "", "/?bb_route=%2Fdeal%2F1#/");
  });

  it("leaves our own routes and an empty fragment alone", () => {
    const history = { state: null, replaceState: vi.fn() };

    expect(normalizeLaunchHash(location("#/deal/abc"), history)).toBe(false);
    expect(normalizeLaunchHash(location("#/"), history)).toBe(false);
    expect(normalizeLaunchHash(location(""), history)).toBe(false);
    expect(history.replaceState).not.toHaveBeenCalled();
  });
});
