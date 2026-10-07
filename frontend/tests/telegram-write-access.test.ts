import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { telegramBridge } from "../src/telegram/TelegramProvider";

function installWebApp(user: { id: number; allows_write_to_pm?: boolean }, version = "7.0") {
  const requestWriteAccess = vi.fn();
  window.Telegram = {
    WebApp: {
      initData: "signed-init-data",
      initDataUnsafe: { user },
      isVersionAtLeast: (required: string) => Number(version) >= Number(required),
      requestWriteAccess
    }
  };
  return requestWriteAccess;
}

describe("bot message permission", () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => { delete window.Telegram; localStorage.clear(); });

  it("asks once when the bot cannot message the user yet", () => {
    const requestWriteAccess = installWebApp({ id: 1 });

    telegramBridge.requestBotMessages();
    telegramBridge.requestBotMessages();

    expect(requestWriteAccess).toHaveBeenCalledTimes(1);
  });

  it("does not ask when the user already allowed messages", () => {
    const requestWriteAccess = installWebApp({ id: 1, allows_write_to_pm: true });

    telegramBridge.requestBotMessages();

    expect(requestWriteAccess).not.toHaveBeenCalled();
  });

  it("does not ask on clients without the permission request", () => {
    const requestWriteAccess = installWebApp({ id: 1 }, "6.2");

    telegramBridge.requestBotMessages();

    expect(requestWriteAccess).not.toHaveBeenCalled();
  });
});
