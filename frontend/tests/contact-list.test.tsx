import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ContactList } from "../src/components/ContactList";
import { I18nProvider, translate } from "../src/i18n";

const openLink = vi.fn();
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { selection: vi.fn(), success: vi.fn(), error: vi.fn() }, openLink, registerBackButtonHandler: () => () => undefined }) }));

const ru = (key: string) => translate(key, undefined, "ru");

describe("contact list", () => {
  // No call button: Telegram blocks calls from a Mini App on iPhone, so it would only disappoint.
  it("offers a Telegram chat by number and copy for a phone, without a call button", () => {
    render(<I18nProvider><ContactList items={[{ kind: "phone", value: "+998 90 123 45 67" }]} /></I18nProvider>);

    fireEvent.click(screen.getByText("+998 90 123 45 67"));
    expect(screen.getByRole("button", { name: ru("contacts.copyNumber") })).toBeInTheDocument();
    expect(screen.queryByText(/tel:|Позвонить/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: ru("contacts.writeInTelegram") }));
    expect(openLink).toHaveBeenLastCalledWith("https://t.me/+998901234567");
  });

  it("opens the Telegram chat directly for a username", () => {
    render(<I18nProvider><ContactList items={[{ kind: "telegram", value: "@madina" }]} /></I18nProvider>);

    fireEvent.click(screen.getByText("@madina"));
    expect(openLink).toHaveBeenLastCalledWith("https://t.me/madina");
  });

  it("sends the partner's contact card to the bot chat from a deal", async () => {
    const onSendToChat = vi.fn().mockResolvedValue(ru("contacts.sentToChat"));
    render(<I18nProvider><ContactList items={[{ kind: "phone", value: "+998 90 123 45 67" }]} onSendToChat={onSendToChat} /></I18nProvider>);

    fireEvent.click(screen.getByText("+998 90 123 45 67"));
    fireEvent.click(screen.getByRole("button", { name: ru("contacts.sendToChat") }));

    await waitFor(() => expect(onSendToChat).toHaveBeenCalledTimes(1));
  });

  it("offers no contact card outside a deal", () => {
    render(<I18nProvider><ContactList items={[{ kind: "phone", value: "+998 90 123 45 67" }]} /></I18nProvider>);

    fireEvent.click(screen.getByText("+998 90 123 45 67"));
    expect(screen.queryByRole("button", { name: ru("contacts.sendToChat") })).not.toBeInTheDocument();
  });
});
