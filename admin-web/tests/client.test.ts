import { describe, expect, it } from "vitest";
import { ApiError, api } from "../src/api/client";
import { fakeApi } from "./helpers";

describe("API client", () => {
  it("sends the session cookie of this site only and JSON bodies", async () => {
    const calls = fakeApi({ "POST /api/admin-web/session": { status: 200, body: { ok: true } } });

    await api("/api/admin-web/session", { method: "POST", body: { id: "1" } });

    expect(calls[0]).toEqual({ url: "/api/admin-web/session", method: "POST", body: { id: "1" }, credentials: "same-origin" });
  });

  it("turns a problem answer into an ApiError with its code", async () => {
    fakeApi({ "GET /api/admin/dashboard": { status: 403, body: { code: "access_denied" } } });

    const error = await api("/api/admin/dashboard").catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 403, code: "access_denied" });
  });

  it("returns nothing for 204", async () => {
    fakeApi({ "POST /api/admin-web/logout": { status: 204 } });

    await expect(api("/api/admin-web/logout", { method: "POST" })).resolves.toBeUndefined();
  });
});
