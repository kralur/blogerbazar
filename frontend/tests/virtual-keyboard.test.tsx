import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import { useEffect } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { BottomNav } from "../src/components/ui";
import { VirtualKeyboardProvider, getVirtualKeyboardState, isEditableElement, useVirtualKeyboard } from "../src/layout/VirtualKeyboardProvider";

const haptic = vi.hoisted(() => ({ impact: vi.fn(), selection: vi.fn() }));

vi.mock("../src/telegram/TelegramProvider", () => ({
  useTelegram: () => ({ haptic })
}));

class MockVisualViewport extends EventTarget {
  height = 844;
  offsetTop = 0;
}

let viewport: MockVisualViewport;
let originalViewport: PropertyDescriptor | undefined;
let originalInnerHeight: PropertyDescriptor | undefined;

function Probe() {
  const { isOpen, keyboardOffset } = useVirtualKeyboard();
  return <output data-offset={keyboardOffset} data-open={isOpen ? "true" : "false"}>keyboard</output>;
}

function renderKeyboard(children: React.ReactNode) {
  return render(<I18nProvider><VirtualKeyboardProvider>{children}</VirtualKeyboardProvider></I18nProvider>);
}

function resizeViewport(height: number, offsetTop = 0) {
  act(() => {
    viewport.height = height;
    viewport.offsetTop = offsetTop;
    viewport.dispatchEvent(new Event("resize"));
  });
}

beforeEach(() => {
  originalViewport = Object.getOwnPropertyDescriptor(window, "visualViewport");
  originalInnerHeight = Object.getOwnPropertyDescriptor(window, "innerHeight");
  viewport = new MockVisualViewport();
  Object.defineProperty(window, "visualViewport", { configurable: true, value: viewport });
  Object.defineProperty(window, "innerHeight", { configurable: true, value: 844 });
  window.location.hash = "#/search";
});

afterEach(() => {
  cleanup();
  if (originalViewport) Object.defineProperty(window, "visualViewport", originalViewport);
  else Reflect.deleteProperty(window, "visualViewport");
  if (originalInnerHeight) Object.defineProperty(window, "innerHeight", originalInnerHeight);
  document.documentElement.removeAttribute("data-virtual-keyboard-open");
  document.documentElement.style.removeProperty("--app-keyboard-offset");
});

describe("virtual keyboard detection", () => {
  it("opens only for an editable field plus a substantial visual viewport reduction", () => {
    const input = document.createElement("input");
    document.body.append(input);
    expect(getVirtualKeyboardState({ layoutViewportHeight: 844, viewport: { height: 720, offsetTop: 0 }, activeElement: input }).isOpen).toBe(true);
    expect(getVirtualKeyboardState({ layoutViewportHeight: 844, viewport: { height: 844, offsetTop: 0 }, activeElement: input }).isOpen).toBe(false);
    expect(getVirtualKeyboardState({ layoutViewportHeight: 844, viewport: { height: 520, offsetTop: 0 }, activeElement: document.body }).isOpen).toBe(false);
    input.remove();
  });

  it("does not treat desktop-like focus, a checkbox, or a button as an open keyboard", async () => {
    renderKeyboard(<><input aria-label="text" /><input aria-label="checkbox" type="checkbox" /><button type="button">Action</button><Probe /></>);
    screen.getByLabelText("text").focus();
    await waitFor(() => expect(screen.getByText("keyboard")).toHaveAttribute("data-open", "false"));

    screen.getByLabelText("checkbox").focus();
    resizeViewport(520);
    await waitFor(() => expect(screen.getByText("keyboard")).toHaveAttribute("data-open", "false"));

    screen.getByRole("button", { name: "Action" }).focus();
    await waitFor(() => expect(screen.getByText("keyboard")).toHaveAttribute("data-open", "false"));
  });

  it("hides BottomNav from the interaction tree while typing and restores the active route after close without blur", async () => {
    renderKeyboard(<><input aria-label="search" type="search" /><BottomNav /><Probe /></>);
    expect(screen.getByRole("link", { name: translate("nav.search", undefined, "ru") })).toHaveAttribute("aria-current", "page");

    screen.getByLabelText("search").focus();
    resizeViewport(520);
    await waitFor(() => expect(screen.queryByRole("link", { name: translate("nav.search", undefined, "ru") })).not.toBeInTheDocument());
    expect(screen.getByText("keyboard")).toHaveAttribute("data-open", "true");

    resizeViewport(844);
    await waitFor(() => expect(screen.getByRole("link", { name: translate("nav.search", undefined, "ru") })).toHaveAttribute("aria-current", "page"));
    expect(screen.getByText("keyboard")).toHaveAttribute("data-open", "false");
  });

  it("closes when the editable field blurs even if the viewport has not expanded yet", async () => {
    renderKeyboard(<><input aria-label="search" type="search" /><BottomNav /><Probe /></>);
    const input = screen.getByLabelText("search");
    input.focus();
    resizeViewport(520);
    await waitFor(() => expect(screen.getByText("keyboard")).toHaveAttribute("data-open", "true"));

    input.blur();
    await waitFor(() => expect(screen.getByText("keyboard")).toHaveAttribute("data-open", "false"));
    expect(screen.getByRole("link", { name: translate("nav.search", undefined, "ru") })).toBeInTheDocument();
  });

  it("does not remount surrounding content while BottomNav is hidden", async () => {
    const mounted = vi.fn();
    function Content() {
      useEffect(() => {
        mounted();
      }, []);
      return <div>Catalog content</div>;
    }

    renderKeyboard(<><input aria-label="search" /><Content /><BottomNav /></>);
    screen.getByLabelText("search").focus();
    resizeViewport(520);
    await waitFor(() => expect(screen.queryByRole("link", { name: translate("nav.search", undefined, "ru") })).not.toBeInTheDocument());
    resizeViewport(844);
    await waitFor(() => expect(screen.getByRole("link", { name: translate("nav.search", undefined, "ru") })).toBeInTheDocument());
    expect(mounted).toHaveBeenCalledTimes(1);
  });

  it("keeps the keyboard open when focus moves between editable fields and supports textarea", async () => {
    renderKeyboard(<><input aria-label="first" /><textarea aria-label="second" /><Probe /></>);
    screen.getByLabelText("first").focus();
    resizeViewport(520);
    await waitFor(() => expect(screen.getByText("keyboard")).toHaveAttribute("data-open", "true"));

    screen.getByLabelText("second").focus();
    await waitFor(() => expect(screen.getByText("keyboard")).toHaveAttribute("data-open", "true"));
  });

  it("keeps BottomNav visible for a sheet without keyboard and hides it when a sheet field opens the keyboard", async () => {
    renderKeyboard(<><BottomNav /><div data-keyboard-scroll-container><input aria-label="sheet budget" inputMode="numeric" /></div><Probe /></>);
    expect(screen.getByRole("link", { name: translate("nav.search", undefined, "ru") })).toBeInTheDocument();

    screen.getByLabelText("sheet budget").focus();
    resizeViewport(520);
    await waitFor(() => expect(screen.queryByRole("link", { name: translate("nav.search", undefined, "ru") })).not.toBeInTheDocument());
  });

  it("does not interpret an orientation resize as keyboard opening", async () => {
    renderKeyboard(<><input aria-label="field" /><Probe /></>);
    screen.getByLabelText("field").focus();
    act(() => Object.defineProperty(window, "innerHeight", { configurable: true, value: 390 }));
    resizeViewport(390);
    act(() => window.dispatchEvent(new Event("orientationchange")));
    await waitFor(() => expect(screen.getByText("keyboard")).toHaveAttribute("data-open", "false"));
  });

  it("uses the nearest sheet scroll container instead of the background page for an obscured field", async () => {
    const scrollBy = vi.fn();
    renderKeyboard(<div data-keyboard-scroll-container ref={(element) => { if (element) { Object.defineProperty(element, "scrollBy", { configurable: true, value: scrollBy }); Object.defineProperty(element, "scrollLeft", { configurable: true, value: 0, writable: true }); } }}><label>Message<textarea aria-label="message" /></label></div>);
    const textarea = screen.getByLabelText("message");
    vi.spyOn(textarea.closest("label")!, "getBoundingClientRect").mockReturnValue({ top: 500, bottom: 620 } as DOMRect);
    textarea.focus();
    resizeViewport(520);
    await waitFor(() => expect(scrollBy).toHaveBeenCalledWith({ top: 116, behavior: "auto" }));
    expect(textarea.closest("[data-keyboard-scroll-container]")).toHaveProperty("scrollLeft", 0);
  });

  it("does not scroll a field that already fits between Telegram chrome and the keyboard", async () => {
    const scrollIntoView = vi.fn();
    renderKeyboard(<><input aria-label="visible field" /><Probe /></>);
    const input = screen.getByLabelText("visible field");
    Object.defineProperty(input, "scrollIntoView", { configurable: true, value: scrollIntoView });
    vi.spyOn(input, "getBoundingClientRect").mockReturnValue({ top: 120, bottom: 160 } as DOMRect);
    input.focus();
    resizeViewport(520);

    await waitFor(() => expect(screen.getByText("keyboard")).toHaveAttribute("data-open", "true"));
    expect(scrollIntoView).not.toHaveBeenCalled();
  });

  it("recognizes supported editable types and cleans document state on unmount", async () => {
    const date = document.createElement("input");
    date.type = "date";
    const file = document.createElement("input");
    file.type = "file";
    const editable = document.createElement("div");
    editable.setAttribute("contenteditable", "true");
    expect(isEditableElement(date)).toBe(true);
    expect(isEditableElement(file)).toBe(false);
    expect(isEditableElement(editable)).toBe(true);

    const rendered = renderKeyboard(<><input aria-label="field" /><Probe /></>);
    screen.getByLabelText("field").focus();
    resizeViewport(520);
    await waitFor(() => expect(document.documentElement.dataset.virtualKeyboardOpen).toBe("true"));
    rendered.unmount();
    expect(document.documentElement.dataset.virtualKeyboardOpen).toBeUndefined();
  });
});
