import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { StrictMode, useCallback, useEffect, useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { UnsavedChangesDialog, useUnsavedChanges } from "../src/hooks/useUnsavedChanges";
import { requestGuardedNavigation } from "../src/navigation/guardedNavigation";
import { navigateWithHistoryOrigin } from "../src/navigation/hashNavigation";
import { TelegramProvider, useTelegram } from "../src/telegram/TelegramProvider";

const initialValues = { title: "Initial", description: "Description", category: "Food", requirements: "Reels", budget: "100000", deadline: "2026-12-31" };
type EditableField = keyof typeof initialValues;

type HistoryMode = "none" | "origin" | "fallback";

function NativeEditHarness({ historyMode = "none" }: { historyMode?: HistoryMode }) {
  const [values, setValues] = useState(initialValues);
  const [destination, setDestination] = useState("");
  const guard = useUnsavedChanges(JSON.stringify(values) !== JSON.stringify(initialValues), historyMode === "none" ? undefined : {
    canCompactHistory: historyMode === "origin",
    historyExitHash: "#/my-campaign/campaign-a",
  });
  const { setBackButtonHandler } = useTelegram();
  const goBack = useCallback(() => {
    if (!requestGuardedNavigation("/my-campaign/campaign-a")) setDestination("/my-campaign/campaign-a");
  }, []);

  useEffect(() => {
    setBackButtonHandler(goBack);
    return () => setBackButtonHandler();
  }, [goBack, setBackButtonHandler]);

  const update = (field: EditableField) => (event: React.ChangeEvent<HTMLInputElement>) => setValues((current) => ({ ...current, [field]: event.target.value }));
  return <>
    {(Object.keys(initialValues) as EditableField[]).map((field) => <input aria-label={field} key={field} onChange={update(field)} value={values[field]} />)}
    <a href="#/profile">profile</a>
    <button onClick={() => { if (!requestGuardedNavigation("/")) setDestination("/"); }} type="button">home</button>
    <button onClick={() => {
      if (historyMode !== "none") {
        guard.exitToHistoryOrigin();
        return;
      }
      setValues(initialValues);
      guard.markClean();
    }} type="button">saved</button>
    <output>{destination}</output>
    <UnsavedChangesDialog guard={guard} labels={{ title: translate("myCampaignEdit.unsavedTitle"), description: translate("myCampaignEdit.unsavedDescription"), continueEditing: translate("myCampaignEdit.continueEditing"), discard: translate("myCampaignEdit.discard") }} />
  </>;
}

function renderHarness(onClick: ReturnType<typeof vi.fn>, historyMode: HistoryMode = "none", strictMode = false) {
  window.Telegram = { WebApp: { platform: "ios", colorScheme: "light", ready: vi.fn(), expand: vi.fn(), requestFullscreen: vi.fn(), disableVerticalSwipes: vi.fn(), BackButton: { show: vi.fn(), hide: vi.fn(), onClick, offClick: vi.fn() } } };
  const content = <I18nProvider><TelegramProvider><NativeEditHarness historyMode={historyMode} /></TelegramProvider></I18nProvider>;
  return render(strictMode ? <StrictMode>{content}</StrictMode> : content);
}

function setupCampaignEditHistory() {
  window.history.replaceState(null, "", "#/campaigns");
  window.location.hash = "/my-campaigns";
  window.location.hash = "/my-campaign/campaign-a";
  navigateWithHistoryOrigin("#/my-campaign/campaign-a", "#/my-campaign-edit/campaign-a");
}

describe("native edit unsaved navigation", () => {
  beforeEach(() => { window.location.hash = "#/my-campaign-edit/campaign-a"; });
  afterEach(() => cleanup());

  it("navigates once while clean", async () => {
    const onClick = vi.fn();
    renderHarness(onClick);
    await waitFor(() => expect(onClick).toHaveBeenCalledTimes(1));
    await act(async () => { (onClick.mock.calls[0][0] as () => void)(); });
    await screen.findByText("/my-campaign/campaign-a");
  });

  it("keeps dirty values on native Back until explicit discard", async () => {
    const onClick = vi.fn();
    renderHarness(onClick);
    await waitFor(() => expect(onClick).toHaveBeenCalledTimes(1));
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    await act(async () => { (onClick.mock.calls[0][0] as () => void)(); });
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    expect(screen.getByDisplayValue("Changed")).toBeInTheDocument();
    expect(screen.queryByText("/my-campaign/campaign-a")).not.toBeInTheDocument();
    await act(async () => { fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.discard", undefined, "ru") })); });
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign/campaign-a"));
  });

  it("cancels a native leave without losing the dirty value", async () => {
    const onClick = vi.fn();
    renderHarness(onClick);
    await waitFor(() => expect(onClick).toHaveBeenCalledTimes(1));
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    await act(async () => { (onClick.mock.calls[0][0] as () => void)(); });
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.continueEditing", undefined, "ru") }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(screen.getByDisplayValue("Changed")).toBeInTheDocument();
  });

  it.each(["title", "description", "category", "requirements", "budget", "deadline"] as EditableField[])("guards a dirty %s field before native Back", async (field) => {
    const onClick = vi.fn();
    renderHarness(onClick);
    await waitFor(() => expect(onClick).toHaveBeenCalledTimes(1));
    fireEvent.change(screen.getByLabelText(field), { target: { value: `${initialValues[field]} changed` } });
    await act(async () => { (onClick.mock.calls[0][0] as () => void)(); });
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    expect(screen.getByDisplayValue(`${initialValues[field]} changed`)).toBeInTheDocument();
  });

  it("intercepts ManagementBackLink-style hash links before the route changes", async () => {
    const onClick = vi.fn();
    renderHarness(onClick);
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    fireEvent.click(screen.getByRole("link", { name: "profile" }));
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    expect(window.location.hash).toBe("#/my-campaign-edit/campaign-a");
    fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.discard", undefined, "ru") }));
    await waitFor(() => expect(window.location.hash).toBe("#/profile"));
  });

  it("restores the edit route for a browser/hash navigation until discard", async () => {
    const onClick = vi.fn();
    renderHarness(onClick);
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    await act(async () => { window.location.hash = "/profile"; });
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign-edit/campaign-a"));
    fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.discard", undefined, "ru") }));
    await waitFor(() => expect(window.location.hash).toBe("#/profile"));
  });

  it("guards a real history.back host navigation", async () => {
    window.history.replaceState(null, "", "#/my-campaign/campaign-a");
    window.location.hash = "/my-campaign-edit/campaign-a";
    const onClick = vi.fn();
    renderHarness(onClick);
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    await act(async () => { window.history.back(); });
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    expect(screen.getByDisplayValue("Changed")).toBeInTheDocument();
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign-edit/campaign-a"));
  });

  it("reuses one sentinel across host Back, Cancel, and a second host Back", async () => {
    window.history.replaceState(null, "", "#/my-campaign/campaign-a");
    window.location.hash = "/my-campaign-edit/campaign-a";
    const onClick = vi.fn();
    renderHarness(onClick);
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });

    await act(async () => { window.history.back(); });
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.continueEditing", undefined, "ru") }));
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign-edit/campaign-a"));

    await act(async () => { window.history.back(); });
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    expect(screen.getByDisplayValue("Changed")).toBeInTheDocument();
  });

  it("bypasses the sentinel once when a host Back is discarded", async () => {
    window.history.replaceState(null, "", "#/my-campaign/campaign-a");
    window.location.hash = "/my-campaign-edit/campaign-a";
    const onClick = vi.fn();
    renderHarness(onClick);
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });

    await act(async () => { window.history.back(); });
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.discard", undefined, "ru") }));
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign/campaign-a"));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("returns through owner details to My Campaigns after host Back, Cancel, and Discard", async () => {
    setupCampaignEditHistory();
    const onClick = vi.fn();
    renderHarness(onClick, "origin");
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });

    await act(async () => { window.history.back(); });
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.continueEditing", undefined, "ru") }));
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign-edit/campaign-a"));

    await act(async () => { window.history.back(); });
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.discard", undefined, "ru") }));
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign/campaign-a"));

    await act(async () => { window.history.back(); });
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaigns"));
    await act(async () => { window.history.back(); });
    await waitFor(() => expect(window.location.hash).toBe("#/campaigns"));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("returns through owner details to My Campaigns after save", async () => {
    setupCampaignEditHistory();
    const onClick = vi.fn();
    const historyGo = vi.spyOn(window.history, "go");
    renderHarness(onClick, "origin");
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    fireEvent.click(screen.getByRole("button", { name: "saved" }));

    await waitFor(() => expect(historyGo).toHaveBeenCalledWith(-3));
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign/campaign-a"));
    await act(async () => { window.history.back(); });
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaigns"));
    historyGo.mockRestore();
  });

  it("does not restore edit, dirty state, or a sentinel through Forward after discard cleanup", async () => {
    setupCampaignEditHistory();
    const onClick = vi.fn();
    renderHarness(onClick, "origin");
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    await act(async () => { window.history.back(); });
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.discard", undefined, "ru") }));
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign/campaign-a"));
    await act(async () => { window.history.back(); });
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaigns"));

    await act(async () => { window.history.forward(); });
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign/campaign-a"));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("keeps Owner details in history after a clean exit and a second edit session", async () => {
    setupCampaignEditHistory();
    const onClick = vi.fn();
    renderHarness(onClick, "origin");

    await act(async () => { window.history.back(); });
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign/campaign-a"));

    navigateWithHistoryOrigin("#/my-campaign/campaign-a", "#/my-campaign-edit/campaign-a");
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign-edit/campaign-a"));
    await act(async () => { window.history.back(); });
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign/campaign-a"));
    await act(async () => { window.history.back(); });
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaigns"));
  });

  it("does not expose My Campaigns through hashchange while compacting a discarded edit", async () => {
    setupCampaignEditHistory();
    const observedHashes: string[] = [];
    const observeHash = () => observedHashes.push(window.location.hash);
    window.addEventListener("hashchange", observeHash);
    const onClick = vi.fn();
    renderHarness(onClick, "origin");
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });

    await act(async () => { window.history.back(); });
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.discard", undefined, "ru") }));
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign/campaign-a"));

    window.removeEventListener("hashchange", observeHash);
    expect(observedHashes).not.toContain("#/my-campaigns");
  });

  it("keeps one Owner entry after three consecutive discard cycles", async () => {
    setupCampaignEditHistory();

    for (let cycle = 0; cycle < 3; cycle += 1) {
      const onClick = vi.fn();
      const view = renderHarness(onClick, "origin");
      fireEvent.change(screen.getByLabelText("title"), { target: { value: `Changed ${cycle}` } });
      await act(async () => { window.history.back(); });
      await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
      fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.discard", undefined, "ru") }));
      await waitFor(() => expect(window.location.hash).toBe("#/my-campaign/campaign-a"));
      view.unmount();

      if (cycle < 2) navigateWithHistoryOrigin("#/my-campaign/campaign-a", "#/my-campaign-edit/campaign-a");
    }

    await act(async () => { window.history.back(); });
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaigns"));
  });

  it("keeps one Owner entry after three consecutive save cycles", async () => {
    setupCampaignEditHistory();

    for (let cycle = 0; cycle < 3; cycle += 1) {
      const onClick = vi.fn();
      const view = renderHarness(onClick, "origin");
      fireEvent.change(screen.getByLabelText("title"), { target: { value: `Saved ${cycle}` } });
      fireEvent.click(screen.getByRole("button", { name: "saved" }));
      await waitFor(() => expect(window.location.hash).toBe("#/my-campaign/campaign-a"));
      view.unmount();

      if (cycle < 2) navigateWithHistoryOrigin("#/my-campaign/campaign-a", "#/my-campaign-edit/campaign-a");
    }

    await act(async () => { window.history.back(); });
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaigns"));
  });

  it("keeps one sentinel through three host Back and Cancel cycles", async () => {
    setupCampaignEditHistory();
    const pushState = vi.spyOn(window.history, "pushState");
    const onClick = vi.fn();
    renderHarness(onClick, "origin", true);
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    await waitFor(() => expect(pushState).toHaveBeenCalledTimes(1));

    for (let cycle = 0; cycle < 3; cycle += 1) {
      await act(async () => { window.history.back(); });
      await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
      fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.continueEditing", undefined, "ru") }));
      await waitFor(() => expect(window.location.hash).toBe("#/my-campaign-edit/campaign-a"));
    }

    expect(pushState).toHaveBeenCalledTimes(1);
    pushState.mockRestore();
  });

  it("uses a safe owner-details fallback after host Back and discard from a direct-open edit route", async () => {
    window.history.replaceState(null, "", "#/my-campaign-edit/campaign-a");
    const onClick = vi.fn();
    renderHarness(onClick, "fallback");
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    await act(async () => { window.history.back(); });
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    fireEvent.click(screen.getByRole("button", { name: translate("myCampaignEdit.discard", undefined, "ru") }));
    await waitFor(() => expect(window.location.hash).toBe("#/my-campaign/campaign-a"));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("creates one sentinel for repeated edits in the same session", async () => {
    const pushState = vi.spyOn(window.history, "pushState");
    const onClick = vi.fn();
    renderHarness(onClick);
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    await waitFor(() => expect(pushState).toHaveBeenCalledTimes(1));
    fireEvent.change(screen.getByLabelText("description"), { target: { value: "Changed description" } });
    fireEvent.change(screen.getByLabelText("budget"), { target: { value: "200000" } });
    expect(pushState).toHaveBeenCalledTimes(1);
    pushState.mockRestore();
  });

  it("does not add a second sentinel in StrictMode", async () => {
    const pushState = vi.spyOn(window.history, "pushState");
    const onClick = vi.fn();
    window.Telegram = { WebApp: { platform: "ios", colorScheme: "light", ready: vi.fn(), expand: vi.fn(), requestFullscreen: vi.fn(), disableVerticalSwipes: vi.fn(), BackButton: { show: vi.fn(), hide: vi.fn(), onClick, offClick: vi.fn() } } };
    render(<StrictMode><I18nProvider><TelegramProvider><NativeEditHarness /></TelegramProvider></I18nProvider></StrictMode>);
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    await waitFor(() => expect(pushState).toHaveBeenCalledTimes(1));
    pushState.mockRestore();
  });

  it("clears the active sentinel before save navigation", async () => {
    const onClick = vi.fn();
    renderHarness(onClick);
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    await waitFor(() => expect(window.history.state).toHaveProperty("bloggerbazarUnsavedGuard"));
    fireEvent.click(screen.getByRole("button", { name: "saved" }));
    await waitFor(() => expect(window.history.state).not.toHaveProperty("bloggerbazarUnsavedGuard"));
  });

  it("becomes clean again when a value is restored to its initial value", async () => {
    const onClick = vi.fn();
    renderHarness(onClick);
    await waitFor(() => expect(onClick).toHaveBeenCalledTimes(1));
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    fireEvent.change(screen.getByLabelText("title"), { target: { value: initialValues.title } });
    await act(async () => { (onClick.mock.calls[0][0] as () => void)(); });
    await screen.findByText("/my-campaign/campaign-a");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("guards the BottomNav-style navigation request and lets repeated Back cancel only the dialog", async () => {
    const onClick = vi.fn();
    renderHarness(onClick);
    await waitFor(() => expect(onClick).toHaveBeenCalledTimes(1));
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    fireEvent.click(screen.getByRole("button", { name: "home" }));
    await screen.findByText(translate("myCampaignEdit.unsavedTitle", undefined, "ru"));
    await act(async () => { (onClick.mock.calls[0][0] as () => void)(); });
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(screen.getByDisplayValue("Changed")).toBeInTheDocument();
    expect(window.location.hash).toBe("#/my-campaign-edit/campaign-a");
  });

  it("removes the guard after a successful save before programmatic navigation", async () => {
    const onClick = vi.fn();
    renderHarness(onClick);
    await waitFor(() => expect(onClick).toHaveBeenCalledTimes(1));
    fireEvent.change(screen.getByLabelText("title"), { target: { value: "Changed" } });
    fireEvent.click(screen.getByRole("button", { name: "saved" }));
    await waitFor(() => expect(screen.getByDisplayValue("Initial")).toBeInTheDocument());
    await act(async () => { (onClick.mock.calls[0][0] as () => void)(); });
    await screen.findByText("/my-campaign/campaign-a");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
