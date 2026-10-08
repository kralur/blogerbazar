import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { PhoneVerification } from "../src/pages/PhoneVerification";

const api = vi.hoisted(() => ({ getCurrentPlatformUser: vi.fn() }));
const bridge = vi.hoisted(() => ({ requestContact: vi.fn() }));
vi.mock("../src/api/marketplace", () => api);
vi.mock("../src/components/LanguageSwitcher", () => ({ LanguageSwitcher: () => null }));
vi.mock("../src/telegram/TelegramProvider", () => ({ telegramBridge: bridge, useTelegram: () => ({ haptic: { selection: vi.fn() } }) }));

const ru = (key: string) => translate(key, undefined, "ru");

describe("phone verification", () => {
  beforeEach(() => vi.clearAllMocks());

  it("continues once Telegram has delivered the shared number to the server", async () => {
    bridge.requestContact.mockResolvedValue(true);
    api.getCurrentPlatformUser.mockResolvedValue({ verifiedPhone: "+998 88 197 29 29" });
    const onVerified = vi.fn();
    render(<I18nProvider><PhoneVerification onVerified={onVerified} /></I18nProvider>);

    fireEvent.click(screen.getByRole("button", { name: ru("phone.share") }));

    await waitFor(() => expect(onVerified).toHaveBeenCalledTimes(1));
    expect(bridge.requestContact).toHaveBeenCalledTimes(1);
  });

  it("explains the bot /phone way when the Telegram dialog was closed", async () => {
    bridge.requestContact.mockResolvedValue(false);
    const onVerified = vi.fn();
    render(<I18nProvider><PhoneVerification onVerified={onVerified} /></I18nProvider>);

    fireEvent.click(screen.getByRole("button", { name: ru("phone.share") }));

    expect(await screen.findByText(ru("phone.declined"))).toBeInTheDocument();
    expect(screen.getByRole("button", { name: ru("phone.sentInBot") })).toBeInTheDocument();
    expect(onVerified).not.toHaveBeenCalled();
    expect(api.getCurrentPlatformUser).not.toHaveBeenCalled();
  });
});
