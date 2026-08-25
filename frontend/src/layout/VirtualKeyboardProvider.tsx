import { createContext, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";

export const virtualKeyboardThreshold = 120;

type ViewportMetrics = Pick<VisualViewport, "height" | "offsetTop">;
type VirtualKeyboardState = { isOpen: boolean; keyboardOffset: number };

const defaultState: VirtualKeyboardState = { isOpen: false, keyboardOffset: 0 };
const VirtualKeyboardContext = createContext<VirtualKeyboardState>(defaultState);

export function getKeyboardViewportState(layoutViewportHeight: number, viewport?: ViewportMetrics) {
  const keyboardOffset = viewport ? Math.max(0, layoutViewportHeight - viewport.height - viewport.offsetTop) : 0;
  return { keyboardOffset, isOpen: keyboardOffset > 0 };
}

export function isEditableElement(element: Element | null): element is HTMLElement {
  if (!(element instanceof HTMLElement) || element.hasAttribute("disabled") || element.getAttribute("aria-disabled") === "true") return false;
  if (element.isContentEditable || element.getAttribute("contenteditable") === "true") return true;
  if (element instanceof HTMLTextAreaElement) return !element.readOnly;
  if (!(element instanceof HTMLInputElement)) return false;
  if (element.readOnly) return false;
  return !["button", "submit", "reset", "checkbox", "radio", "range", "color", "file", "hidden", "image"].includes(element.type.toLowerCase());
}

export function getVirtualKeyboardState({ layoutViewportHeight, viewport, activeElement, threshold = virtualKeyboardThreshold }: { layoutViewportHeight: number; viewport?: ViewportMetrics; activeElement: Element | null; threshold?: number }): VirtualKeyboardState {
  const { keyboardOffset } = getKeyboardViewportState(layoutViewportHeight, viewport);
  return { keyboardOffset, isOpen: isEditableElement(activeElement) && keyboardOffset >= threshold };
}

function layoutViewportHeight() {
  return Math.max(window.innerHeight, document.documentElement.clientHeight, window.visualViewport?.height ?? 0);
}

function supportsTouchKeyboardFallback() {
  return navigator.maxTouchPoints > 0 || window.matchMedia?.("(pointer: coarse)").matches === true;
}

function findScrollContainer(element: HTMLElement) {
  for (let parent = element.parentElement; parent; parent = parent.parentElement) {
    if (parent.hasAttribute("data-keyboard-scroll-container")) return parent;
    const style = window.getComputedStyle(parent);
    if (/(auto|scroll)/.test(style.overflowY) && parent.scrollHeight > parent.clientHeight) return parent;
  }
  return undefined;
}

function keepActiveFieldVisible() {
  const activeElement = document.activeElement;
  if (!isEditableElement(activeElement) || !window.visualViewport) return;

  const target = activeElement.closest("label") ?? activeElement;
  const rect = target.getBoundingClientRect();
  const rootStyle = window.getComputedStyle(document.documentElement);
  const safeTop = Number.parseFloat(rootStyle.getPropertyValue("--tg-effective-content-top")) || 0;
  const visibleTop = window.visualViewport.offsetTop + safeTop + 12;
  const visibleBottom = window.visualViewport.offsetTop + window.visualViewport.height - 16;
  if (rect.top >= visibleTop && rect.bottom <= visibleBottom) return;

  const scrollContainer = findScrollContainer(activeElement);
  if (scrollContainer) {
    const delta = rect.top < visibleTop ? rect.top - visibleTop : rect.bottom - visibleBottom;
    if (typeof scrollContainer.scrollBy === "function") scrollContainer.scrollBy({ top: delta, behavior: "auto" });
    else scrollContainer.scrollTop += delta;
    return;
  }

  activeElement.scrollIntoView?.({ block: "nearest", inline: "nearest", behavior: "auto" });
}

export function VirtualKeyboardProvider({ children }: { children: ReactNode }) {
  const baselineHeightRef = useRef(0);
  const frameRef = useRef<number>();
  const [state, setState] = useState<VirtualKeyboardState>(defaultState);

  useEffect(() => {
    baselineHeightRef.current = layoutViewportHeight();
    const root = document.documentElement;

    const applyState = (resetBaseline = false) => {
      frameRef.current = undefined;
      const viewport = window.visualViewport;
      const currentLayoutHeight = layoutViewportHeight();
      if (resetBaseline) baselineHeightRef.current = viewport ? Math.max(currentLayoutHeight, viewport.height + viewport.offsetTop) : currentLayoutHeight;
      if (document.hidden || !isEditableElement(document.activeElement)) {
        baselineHeightRef.current = Math.max(currentLayoutHeight, viewport ? viewport.height + viewport.offsetTop : 0);
        setState(defaultState);
        return;
      }

      const fallbackViewport = viewport ?? { height: currentLayoutHeight, offsetTop: 0 };
      const comparisonHeight = viewport ? Math.max(baselineHeightRef.current, currentLayoutHeight) : baselineHeightRef.current;
      const next = getVirtualKeyboardState({ layoutViewportHeight: comparisonHeight, viewport: fallbackViewport, activeElement: document.activeElement });
      const fallbackOpen = !viewport && supportsTouchKeyboardFallback() && next.keyboardOffset >= virtualKeyboardThreshold;
      const resolved = { keyboardOffset: next.keyboardOffset, isOpen: viewport ? next.isOpen : fallbackOpen };
      if (!resolved.isOpen) baselineHeightRef.current = Math.max(baselineHeightRef.current, currentLayoutHeight, fallbackViewport.height + fallbackViewport.offsetTop);
      setState((current) => current.isOpen === resolved.isOpen && current.keyboardOffset === resolved.keyboardOffset ? current : resolved);
      if (resolved.isOpen) window.requestAnimationFrame(keepActiveFieldVisible);
    };

    const schedule = (resetBaseline = false) => {
      if (frameRef.current !== undefined) window.cancelAnimationFrame(frameRef.current);
      frameRef.current = window.requestAnimationFrame(() => applyState(resetBaseline));
    };
    const onFocusChange = () => schedule();
    const onVisibilityChange = () => schedule(document.hidden === false);
    const onOrientationChange = () => schedule(true);

    applyState(true);
    window.visualViewport?.addEventListener("resize", onFocusChange);
    window.visualViewport?.addEventListener("scroll", onFocusChange);
    window.addEventListener("resize", onFocusChange, { passive: true });
    window.addEventListener("orientationchange", onOrientationChange, { passive: true });
    window.addEventListener("hashchange", onFocusChange);
    document.addEventListener("focusin", onFocusChange);
    document.addEventListener("focusout", onFocusChange);
    document.addEventListener("visibilitychange", onVisibilityChange);
    return () => {
      if (frameRef.current !== undefined) window.cancelAnimationFrame(frameRef.current);
      window.visualViewport?.removeEventListener("resize", onFocusChange);
      window.visualViewport?.removeEventListener("scroll", onFocusChange);
      window.removeEventListener("resize", onFocusChange);
      window.removeEventListener("orientationchange", onOrientationChange);
      window.removeEventListener("hashchange", onFocusChange);
      document.removeEventListener("focusin", onFocusChange);
      document.removeEventListener("focusout", onFocusChange);
      document.removeEventListener("visibilitychange", onVisibilityChange);
      root.removeAttribute("data-virtual-keyboard-open");
      root.style.removeProperty("--app-keyboard-offset");
    };
  }, []);

  useEffect(() => {
    const root = document.documentElement;
    root.dataset.virtualKeyboardOpen = state.isOpen ? "true" : "false";
    root.style.setProperty("--app-keyboard-offset", `${state.isOpen ? state.keyboardOffset : 0}px`);
  }, [state]);

  const value = useMemo(() => state, [state]);
  return <VirtualKeyboardContext.Provider value={value}>{children}</VirtualKeyboardContext.Provider>;
}

export function useVirtualKeyboard() {
  return useContext(VirtualKeyboardContext);
}
