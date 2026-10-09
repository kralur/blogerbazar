import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ContactList } from "../src/components/ContactList";
import { I18nProvider, translate } from "../src/i18n";

const openLink = vi.fn();
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { selection: vi.fn(), success: vi.fn(), error: vi.fn() }, openLink, registerBackButtonHandler: () => () => undefined }) }));

const ru = (key: string) => translate(key, undefined, "ru");

describe("contact list", () => {
  it("offers call, Telegram chat by number and copy for a phone", () => {
    render(<I18nProvider><ContactList items={[{ kind: "phone", value: "+998 90 123 45 67" }]} /></I18nProvider>);

    fireEvent.click(screen.getByText("+998 90 123 45 67"));
    fireEvent.click(screen.getByRole("button", { name: ru("contacts.writeInTelegram") }));
    expect(openLink).toHaveBeenLastCalledWith("https://t.me/+998901234567");

    // Calling is a real tel: link the user taps: Telegram's WebViews drop a call started from script.
    fireEvent.click(screen.getByText("+998 90 123 45 67"));
    expect(screen.getByRole("link", { name: ru("contacts.call") })).toHaveAttribute("href", "tel:+998901234567");
  });

  it("does not offer a call in desktop Telegram, which has no phone", () => {
    window.Telegram = { WebApp: { platform: "tdesktop" } } as typeof window.Telegram;
    render(<I18nProvider><ContactList items={[{ kind: "phone", value: "+998 90 123 45 67" }]} /></I18nProvider>);

    fireEvent.click(screen.getByText("+998 90 123 45 67"));
    expect(screen.queryByRole("link", { name: ru("contacts.call") })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: ru("contacts.copyNumber") })).toBeInTheDocument();
    window.Telegram = undefined;
  });

  it("opens the Telegram chat directly for a username", () => {
    render(<I18nProvider><ContactList items={[{ kind: "telegram", value: "@madina" }]} /></I18nProvider>);

    fireEvent.click(screen.getByText("@madina"));
    expect(openLink).toHaveBeenLastCalledWith("https://t.me/madina");
  });
});
