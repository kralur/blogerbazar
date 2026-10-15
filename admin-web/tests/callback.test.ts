import { describe, expect, it } from "vitest";
import { ApiError } from "../src/api/client";
import { completeTelegramSignIn, readWidgetFields, signInErrorFor } from "../src/auth/telegramCallback";
import { fakeApi, session, watchNavigation } from "./helpers";

const widgetQuery = "?id=5044343262&first_name=Umid&username=umid&photo_url=https%3A%2F%2Ft.me%2Fi%2Fuserpic%2F320%2Fa.jpg&auth_date=1760000000&hash=abc123";

describe("Telegram sign-in callback", () => {
  it("passes every widget field to the server unchanged", () => {
    expect(readWidgetFields(widgetQuery)).toEqual({
      id: "5044343262",
      first_name: "Umid",
      username: "umid",
      photo_url: "https://t.me/i/userpic/320/a.jpg",
      auth_date: "1760000000",
      hash: "abc123"
    });
    expect(readWidgetFields("?id=1")).toBeNull();
    expect(readWidgetFields("")).toBeNull();
  });

  it("signs in, drops the signed data from the address bar and opens the admin pages", async () => {
    const calls = fakeApi({ "POST /api/admin-web/session": { status: 200, body: session } });
    const navigation = watchNavigation();
    window.history.replaceState(null, "", `/auth/telegram${widgetQuery}`);

    await completeTelegramSignIn(window.location.search);

    expect(window.location.search).toBe("");
    expect(navigation.cleanAddress).toHaveBeenCalledWith("/auth/telegram");
    expect(calls).toHaveLength(1);
    expect(calls[0]).toMatchObject({ url: "/api/admin-web/session", method: "POST", credentials: "same-origin" });
    expect((calls[0].body as Record<string, string>).hash).toBe("abc123");
    expect(navigation.replace).toHaveBeenCalledWith("/");
  });

  it.each([
    [401, "expired"],
    [403, "denied"],
    [429, "rate"],
    [404, "config"],
    [500, "failed"]
  ])("answers %s with the sign-in page and the %s message", async (status, error) => {
    fakeApi({ "POST /api/admin-web/session": { status } });
    const navigation = watchNavigation();

    await completeTelegramSignIn(widgetQuery);

    expect(navigation.replace).toHaveBeenCalledWith(`/login?error=${error}`);
  });

  it("goes back to the sign-in page without widget data", async () => {
    const calls = fakeApi({});
    const navigation = watchNavigation();

    await completeTelegramSignIn("");

    expect(calls).toHaveLength(0);
    expect(navigation.replace).toHaveBeenCalledWith("/login");
  });

  it("treats a network failure as a failed sign-in", () => {
    expect(signInErrorFor(new TypeError("Failed to fetch"))).toBe("failed");
    expect(signInErrorFor(new ApiError(401))).toBe("expired");
  });
});
