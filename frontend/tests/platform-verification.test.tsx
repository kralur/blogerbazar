import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ verifyMyPlatform: vi.fn() }));
vi.mock("../src/api/marketplace", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/api/marketplace")>()), verifyMyPlatform: api.verifyMyPlatform }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { selection: vi.fn(), success: vi.fn(), error: vi.fn() } }) }));

import { PlatformVerification } from "../src/components/PlatformVerification";

const ru = (key: string, values?: Record<string, string | number>) => translate(key, values, "ru");
const telegram = { id: "p-tg", type: "telegram", url: "https://t.me/madina_blog", followers: 5000 };
const youTube = { id: "p-yt", type: "youtube", url: "https://youtube.com/@madina", followers: 1000 };

describe("Platform verification (D67)", () => {
  beforeEach(() => api.verifyMyPlatform.mockReset());

  it("shows only Telegram and YouTube channels and explains how to add the bot", async () => {
    api.verifyMyPlatform.mockResolvedValue({ outcome: "botNotAdmin", platform: telegram, botUsername: "BloggerBazarBot" });
    render(<I18nProvider><PlatformVerification onVerified={vi.fn()} platforms={[telegram, { id: "p-ig", type: "instagram", url: "https://instagram.com/madina" }]} /></I18nProvider>);

    expect(screen.queryByText(/instagram\.com/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: ru("verify.confirm") }));
    expect(await screen.findByText(ru("verify.botNotAdmin.step2", { bot: "@BloggerBazarBot" }))).toBeInTheDocument();
    expect(screen.getByRole("button", { name: ru("verify.checkAgain") })).toBeInTheDocument();
  });

  it("gives the YouTube code to put in the channel description", async () => {
    api.verifyMyPlatform.mockResolvedValue({ outcome: "codeMissing", platform: youTube, code: "BB-7K2Q9X" });
    render(<I18nProvider><PlatformVerification onVerified={vi.fn()} platforms={[youTube]} /></I18nProvider>);

    fireEvent.click(screen.getByRole("button", { name: ru("verify.confirm") }));
    expect(await screen.findByText("BB-7K2Q9X")).toBeInTheDocument();
  });

  it("marks a confirmed channel and offers to refresh its numbers", async () => {
    const onVerified = vi.fn();
    api.verifyMyPlatform.mockResolvedValue({ outcome: "verified", platform: { ...telegram, followers: 12345, verificationSource: "telegram", verifiedAtUtc: "2026-11-20T09:00:00Z" } });
    render(<I18nProvider><PlatformVerification onVerified={onVerified} platforms={[telegram]} /></I18nProvider>);

    fireEvent.click(screen.getByRole("button", { name: ru("verify.confirm") }));
    expect(await screen.findByRole("button", { name: ru("verify.refresh") })).toBeInTheDocument();
    expect(onVerified).toHaveBeenCalledTimes(1);
  });
});
