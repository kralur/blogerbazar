import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { ProfileMediaPicker } from "../src/components/ProfileMediaPicker";

vi.mock("../src/telegram/TelegramProvider", async (importOriginal) => ({ ...(await importOriginal<typeof import("../src/telegram/TelegramProvider")>()), useTelegram: () => ({ haptic: { selection: vi.fn() }, registerBackButtonHandler: vi.fn(() => vi.fn()), setBackButtonHandler: vi.fn() }) }));

const ru = (key: string) => translate(key, undefined, "ru");

describe("ProfileMediaPicker (compact)", () => {
  it("has a single photo action and no separate delete badge", () => {
    const onChange = vi.fn();
    render(<I18nProvider><ProfileMediaPicker canRemove compact currentUrl="https://cdn.example/a.jpg" name="Umid" onChange={onChange} pending={undefined} /></I18nProvider>);

    expect(screen.queryByRole("button", { name: ru("profileMedia.delete") })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: ru("profileMedia.manage") }));
    fireEvent.click(screen.getByRole("button", { name: ru("profileMedia.deletePhoto") }));
    expect(onChange).toHaveBeenCalledWith(null);
  });

  it("goes straight to choosing a file when there is no photo to remove", () => {
    render(<I18nProvider><ProfileMediaPicker canRemove={false} compact name="Umid" onChange={vi.fn()} pending={undefined} /></I18nProvider>);
    const input = screen.getByLabelText(ru("profileMedia.selectAria")) as HTMLInputElement;
    const click = vi.spyOn(input, "click");

    fireEvent.click(screen.getByRole("button", { name: ru("profileMedia.upload") }));
    expect(click).toHaveBeenCalled();
    expect(screen.queryByRole("button", { name: ru("profileMedia.deletePhoto") })).not.toBeInTheDocument();
  });
});
