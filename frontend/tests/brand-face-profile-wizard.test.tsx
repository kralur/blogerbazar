import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  deleteProfileImage: vi.fn(),
  getMyBrandFaceProfile: vi.fn(),
  upsertBrandFaceProfile: vi.fn(),
  uploadProfileImage: vi.fn()
}));
const telegram = vi.hoisted(() => ({
  haptic: { selection: vi.fn(), success: vi.fn(), warning: vi.fn(), error: vi.fn() },
  registerBackButtonHandler: vi.fn(() => vi.fn()),
  setBackButtonHandler: vi.fn(),
  setClosingConfirmation: vi.fn(),
  isTelegram: true,
  user: { first_name: "Madina", username: "madina" }
}));

vi.mock("../src/api/marketplace", () => api);
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => telegram }));
vi.mock("../src/hooks/useProfileDataRefresh", () => ({ notifyProfileDataChanged: vi.fn() }));
vi.mock("../src/components/ProfileMediaPicker", () => ({
  ProfileMediaPicker: ({ onChange }: { onChange: (image: File) => void }) => <button onClick={() => onChange(new File(["avatar"], "avatar.png", { type: "image/png" }))} type="button">media</button>
}));
vi.mock("../src/components/CategoryMultiSelect", () => ({
  CategoryMultiSelect: ({ onChange, error }: { onChange: (categories: string[]) => void; error?: string }) => <div><button onClick={() => onChange(["beauty"])} type="button">choose category</button><button onClick={() => onChange(["other:Music"])} type="button">choose other</button>{error && <p>{error}</p>}</div>
}));

import { BrandFaceProfileForm } from "../src/pages/BrandFaceProfileForm";

function renderCreate(onBackToRole = vi.fn()) {
  const onCompleted = vi.fn();
  render(<I18nProvider><BrandFaceProfileForm onBackToRole={onBackToRole} onCompleted={onCompleted} /></I18nProvider>);
  return { onBackToRole, onCompleted };
}

async function completeStepOne(user: ReturnType<typeof userEvent.setup>, languages = "Русский, Русский, O‘zbekcha") {
  await user.type(screen.getByPlaceholderText(translate("brandFace.languagesPlaceholder", undefined, "ru")), languages);
  await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
}

async function completeStepTwo(user: ReturnType<typeof userEvent.setup>, other = false) {
  await user.click(screen.getByRole("button", { name: other ? "choose other" : "choose category" }));
  await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
}

async function reachReview(user: ReturnType<typeof userEvent.setup>) {
  await completeStepOne(user);
  await completeStepTwo(user);
  await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
}

describe("Brand Face profile wizard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getMyBrandFaceProfile.mockRejectedValue(new Error("not found"));
    api.upsertBrandFaceProfile.mockResolvedValue({});
    api.uploadProfileImage.mockResolvedValue({ url: "https://cdn.example/avatar.png" });
    api.deleteProfileImage.mockResolvedValue(undefined);
    telegram.user = { first_name: "Madina", username: "madina" };
    telegram.isTelegram = true;
  });

  it("starts on About and blocks an incomplete first step", () => {
    renderCreate();
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.brandFaceAboutStep", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getByDisplayValue("Madina")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") })).toBeDisabled();
    expect(api.upsertBrandFaceProfile).not.toHaveBeenCalled();
  });

  it("validates the first step before moving to Positioning", async () => {
    const user = userEvent.setup();
    renderCreate();
    await user.click(screen.getByPlaceholderText(translate("brandFace.languagesPlaceholder", undefined, "ru")));
    await user.tab();
    expect(screen.getByText(translate("brandFace.languagesRequired", undefined, "ru"))).toBeInTheDocument();
    await completeStepOne(user);
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.brandFacePositioningStep", undefined, "ru") })).toBeInTheDocument();
  });

  it("keeps master state when navigating between steps", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeStepOne(user, "Русский, O‘zbekcha");
    await completeStepTwo(user, true);
    await user.click(screen.getByRole("button", { name: translate("common.back", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("common.back", undefined, "ru") }));
    expect(screen.getByDisplayValue("Русский, O‘zbekcha")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    expect(screen.getByDisplayValue("@madina")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "choose other" }));
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.brandFacePortfolioStep", undefined, "ru") })).toBeInTheDocument();
  });

  it("does not call the API before final Review and returns edits to their section", async () => {
    const user = userEvent.setup();
    renderCreate();
    await reachReview(user);
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.brandFaceReviewStep", undefined, "ru") })).toBeInTheDocument();
    expect(api.upsertBrandFaceProfile).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: translate("wizard.editSection", { section: translate("wizard.brandFacePositioningStep", undefined, "ru") }, "ru") }));
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.brandFacePositioningStep", undefined, "ru") })).toBeInTheDocument();
  });

  it("submits the compatible payload only from Review and uploads media afterwards", async () => {
    const user = userEvent.setup();
    const order: string[] = [];
    api.upsertBrandFaceProfile.mockImplementation(async (payload: unknown) => {
      order.push("core");
      return payload;
    });
    api.uploadProfileImage.mockImplementation(async () => {
      order.push("media");
      return { url: "https://cdn.example/avatar.png" };
    });
    renderCreate();
    await completeStepOne(user);
    await completeStepTwo(user);
    await user.click(screen.getByRole("button", { name: "media" }));
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    await waitFor(() => expect(api.upsertBrandFaceProfile).toHaveBeenCalledWith(expect.objectContaining({
      name: "Madina",
      city: "tashkent-city",
      languages: ["Русский", "O‘zbekcha"],
      categories: ["beauty"],
      telegram: "@madina",
      age: null,
      gender: null
    })));
    await waitFor(() => expect(api.uploadProfileImage).toHaveBeenCalledWith("brand-face", expect.any(File)));
    expect(order).toEqual(["core", "media"]);
  });

  it("preserves the Other category in the final payload", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeStepOne(user);
    await completeStepTwo(user, true);
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    await waitFor(() => expect(api.upsertBrandFaceProfile).toHaveBeenCalledWith(expect.objectContaining({ categories: ["other:Music"] })));
  });

  it("applies late Telegram prefill only while the field is untouched", async () => {
    telegram.user = undefined as unknown as { first_name: string; username: string };
    const view = render(<I18nProvider><BrandFaceProfileForm onCompleted={vi.fn()} /></I18nProvider>);
    telegram.user = { first_name: "Dilnoza", username: "dilnoza" };
    view.rerender(<I18nProvider><BrandFaceProfileForm onCompleted={vi.fn()} /></I18nProvider>);
    expect(await screen.findByDisplayValue("Dilnoza")).toBeInTheDocument();
    const name = screen.getByDisplayValue("Dilnoza");
    await userEvent.setup().clear(name);
    await userEvent.setup().type(name, "Malika");
    telegram.user = { first_name: "Updated", username: "updated" };
    view.rerender(<I18nProvider><BrandFaceProfileForm onCompleted={vi.fn()} /></I18nProvider>);
    expect(screen.getByDisplayValue("Malika")).toBeInTheDocument();
  });

  it("continues FTUE safely when media upload fails after the core save", async () => {
    const user = userEvent.setup();
    const { onCompleted } = renderCreate();
    api.uploadProfileImage.mockRejectedValue(new Error("storage unavailable"));
    await completeStepOne(user);
    await completeStepTwo(user);
    await user.click(screen.getByRole("button", { name: "media" }));
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    await waitFor(() => expect(api.upsertBrandFaceProfile).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(onCompleted).toHaveBeenCalledTimes(1));
    expect(telegram.haptic.warning).toHaveBeenCalled();
  });

  it("blocks a duplicate final submit while the PUT request is pending", async () => {
    const user = userEvent.setup();
    let resolveRequest: (() => void) | undefined;
    api.upsertBrandFaceProfile.mockImplementation(() => new Promise<void>((resolve) => { resolveRequest = resolve; }));
    renderCreate();
    await reachReview(user);
    const submit = screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") });
    await user.click(submit);
    fireEvent.click(submit);
    expect(api.upsertBrandFaceProfile).toHaveBeenCalledTimes(1);
    resolveRequest?.();
  });

  it("routes server validation to the affected step", async () => {
    const user = userEvent.setup();
    api.upsertBrandFaceProfile.mockRejectedValue(new ApiError(400, "validation_failed", ["Telegram"]));
    renderCreate();
    await reachReview(user);
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    expect(await screen.findByRole("heading", { level: 1, name: translate("wizard.brandFacePositioningStep", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getAllByText(translate("form.validation.username", undefined, "ru")).length).toBeGreaterThan(0);
  });

  it("hydrates edit mode and uses the same PUT endpoint", async () => {
    const user = userEvent.setup();
    api.getMyBrandFaceProfile.mockResolvedValue({
      name: "Aziza",
      city: "samarkand",
      languages: ["Русский"],
      categories: ["beauty"],
      experience: "Опыт",
      instagram: "@aziza",
      telegram: "@aziza",
      portfolioUrl: "https://portfolio.example",
      collaborationPrice: 200000,
      description: "Описание",
      avatarUrl: null
    });
    render(<I18nProvider><BrandFaceProfileForm /></I18nProvider>);
    await screen.findByDisplayValue("Aziza");
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: "choose category" }));
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("wizard.saveChanges", undefined, "ru") }));
    await waitFor(() => expect(api.upsertBrandFaceProfile).toHaveBeenCalled());
  });

  it("uses one native BackButton handler and returns the first FTUE step to role selection", async () => {
    const { onBackToRole } = renderCreate();
    await waitFor(() => expect(telegram.setBackButtonHandler).toHaveBeenCalledTimes(1));
    const nativeBack = telegram.setBackButtonHandler.mock.calls[0][0] as () => void;
    nativeBack();
    expect(onBackToRole).toHaveBeenCalledTimes(1);
  });
});
