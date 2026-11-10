import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ManagementBackLink } from "../src/components/ManagementBackLink";

const telegram = vi.hoisted(() => ({ value: { isTelegram: false, isEmbedded: true, isEnvironmentReady: true } }));
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => telegram.value }));

describe("ManagementBackLink", () => {
  // The website loads the Telegram script too (no initData there), so the link must stay.
  it("shows on the website even when the Telegram script is loaded", () => {
    telegram.value = { isTelegram: false, isEmbedded: true, isEnvironmentReady: true };
    render(<ManagementBackLink ariaLabel="Back" href="#/" />);
    expect(screen.getByRole("link", { name: "Back" })).toBeInTheDocument();
  });

  it("hides inside Telegram, which has its own Back button", () => {
    telegram.value = { isTelegram: true, isEmbedded: true, isEnvironmentReady: true };
    render(<ManagementBackLink ariaLabel="Back" href="#/" />);
    expect(screen.queryByRole("link", { name: "Back" })).not.toBeInTheDocument();
  });
});
