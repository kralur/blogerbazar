import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { useCallback, useEffect, useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { UnsavedChangesDialog, useUnsavedChanges } from "../src/hooks/useUnsavedChanges";
import { requestGuardedNavigation } from "../src/navigation/guardedNavigation";
import { TelegramProvider, useTelegram } from "../src/telegram/TelegramProvider";

const initialValues = { title: "Initial", description: "Description", category: "Food", requirements: "Reels", budget: "100000", deadline: "2026-12-31" };
type EditableField = keyof typeof initialValues;

function NativeEditHarness() {
  const [values, setValues] = useState(initialValues);
  const [destination, setDestination] = useState("");
  const guard = useUnsavedChanges(JSON.stringify(values) !== JSON.stringify(initialValues));
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
    <button onClick={() => { setValues(initialValues); guard.markClean(); }} type="button">saved</button>
    <output>{destination}</output>
    <UnsavedChangesDialog guard={guard} labels={{ title: translate("myCampaignEdit.unsavedTitle"), description: translate("myCampaignEdit.unsavedDescription"), continueEditing: translate("myCampaignEdit.continueEditing"), discard: translate("myCampaignEdit.discard") }} />
  </>;
}

function renderHarness(onClick: ReturnType<typeof vi.fn>) {
  window.Telegram = { WebApp: { platform: "ios", colorScheme: "light", ready: vi.fn(), expand: vi.fn(), requestFullscreen: vi.fn(), disableVerticalSwipes: vi.fn(), BackButton: { show: vi.fn(), hide: vi.fn(), onClick, offClick: vi.fn() } } };
  return render(<I18nProvider><TelegramProvider><NativeEditHarness /></TelegramProvider></I18nProvider>);
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
