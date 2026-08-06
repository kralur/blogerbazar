import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, beforeEach, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  createBusinessProfile: vi.fn(),
  deleteProfileImage: vi.fn(),
  getMyBusinessProfile: vi.fn(),
  updateBusinessProfile: vi.fn(),
  uploadProfileImage: vi.fn()
}));
const telegram = vi.hoisted(() => ({
  haptic: { selection: vi.fn(), success: vi.fn(), warning: vi.fn(), error: vi.fn() },
  registerBackButtonHandler: vi.fn(() => vi.fn()),
  setBackButtonHandler: vi.fn(),
  setClosingConfirmation: vi.fn(),
  user: { username: "lumibeauty" }
}));

vi.mock("../src/api/marketplace", () => api);
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => telegram }));
vi.mock("../src/hooks/useProfileDataRefresh", () => ({ notifyProfileDataChanged: vi.fn() }));
vi.mock("../src/components/ProfileMediaPicker", () => ({
  ProfileMediaPicker: ({ onChange }: { onChange: (image: File) => void }) => <button onClick={() => onChange(new File(["logo"], "logo.png", { type: "image/png" }))} type="button">media</button>
}));

import { BusinessProfileForm } from "../src/pages/BusinessProfileForm";

function renderCreate(onBackToRole = vi.fn()) {
  const onCompleted = vi.fn();
  render(<I18nProvider><BusinessProfileForm onBackToRole={onBackToRole} onCompleted={onCompleted} /></I18nProvider>);
  return { onBackToRole, onCompleted };
}

async function completeStepOne(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByPlaceholderText("Lumi Beauty"), "Lumi Beauty");
  const username = screen.getByPlaceholderText("@username");
  await user.clear(username);
  await user.type(username, "@lumibeauty");
  await user.selectOptions(screen.getByRole("combobox", { name: translate("common.city", undefined, "ru") }), "tashkent-city");
  await waitFor(() => expect(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") })).toBeEnabled());
  await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
}

async function completeStepTwo(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByPlaceholderText(translate("form.companyDescriptionPlaceholder", undefined, "ru")), "Beauty brand");
  fireEvent.change(screen.getByPlaceholderText("+998 90 123 45 67"), { target: { value: "+998 90 123 45 67" } });
  await waitFor(() => expect(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") })).toBeEnabled());
  await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
}

describe("Business profile wizard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getMyBusinessProfile.mockRejectedValue(new Error("not found"));
    api.createBusinessProfile.mockResolvedValue({});
    api.updateBusinessProfile.mockResolvedValue({});
    api.uploadProfileImage.mockResolvedValue({ url: "https://cdn.example/logo.png" });
    api.deleteProfileImage.mockResolvedValue(undefined);
  });

  it("starts on Company and blocks an invalid first step", () => {
    renderCreate();
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.businessCompanyStep", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") })).toBeDisabled();
    expect(api.createBusinessProfile).not.toHaveBeenCalled();
  });

  it("keeps completed first-step values when navigating back", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeStepOne(user);
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.businessDetailsStep", undefined, "ru") })).toBeInTheDocument();
    await user.click(screen.getAllByRole("button", { name: translate("common.back", undefined, "ru") })[0]);
    expect(screen.getByDisplayValue("Lumi Beauty")).toBeInTheDocument();
  });

  it("does not call the Business API before final Review submit", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeStepOne(user);
    await completeStepTwo(user);
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.businessReviewStep", undefined, "ru") })).toBeInTheDocument();
    expect(api.createBusinessProfile).not.toHaveBeenCalled();
    expect(api.updateBusinessProfile).not.toHaveBeenCalled();
  });

  it("submits the existing create payload and uploads media only afterwards", async () => {
    const user = userEvent.setup();
    const callOrder: string[] = [];
    api.createBusinessProfile.mockImplementation(async (input: unknown) => {
      callOrder.push("core");
      return input;
    });
    api.uploadProfileImage.mockImplementation(async () => {
      callOrder.push("media");
      return { url: "https://cdn.example/logo.png" };
    });
    renderCreate();
    await completeStepOne(user);
    await user.click(screen.getByRole("button", { name: "media" }));
    await completeStepTwo(user);
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    await waitFor(() => expect(api.createBusinessProfile).toHaveBeenCalledWith(expect.objectContaining({
      name: "Lumi Beauty",
      username: "@lumibeauty",
      city: "tashkent-city",
      description: "Beauty brand",
      phone: "+998 90 123 45 67"
    })));
    await waitFor(() => expect(api.uploadProfileImage).toHaveBeenCalledWith("business", expect.any(File)));
    expect(callOrder).toEqual(["core", "media"]);
  });

  it("keeps a selected pending image while moving between steps", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeStepOne(user);
    await user.click(screen.getByRole("button", { name: "media" }));
    await user.click(screen.getAllByRole("button", { name: translate("common.back", undefined, "ru") })[0]);
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await completeStepTwo(user);
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    await waitFor(() => expect(api.uploadProfileImage).toHaveBeenCalled());
  });

  it("returns Review edits to the matching step", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeStepOne(user);
    await completeStepTwo(user);
    await user.click(screen.getByRole("button", { name: translate("wizard.editSection", { section: translate("wizard.businessCompanyStep", undefined, "ru") }, "ru") }));
    expect(screen.getByDisplayValue("Lumi Beauty")).toBeInTheDocument();
  });

  it("routes a server validation failure to its field step", async () => {
    const user = userEvent.setup();
    api.createBusinessProfile.mockRejectedValue(new ApiError(400, "validation_failed", ["Description"]));
    renderCreate();
    await completeStepOne(user);
    await completeStepTwo(user);
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    expect((await screen.findAllByText(translate("form.validation.description", undefined, "ru"))).length).toBeGreaterThan(0);
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.businessDetailsStep", undefined, "ru") })).toBeInTheDocument();
  });

  it("uses the existing update branch after edit hydration", async () => {
    const user = userEvent.setup();
    api.getMyBusinessProfile.mockResolvedValue({ name: "Lumi Beauty", username: "@lumibeauty", city: "samarkand", description: "Beauty brand", phone: "+998 90 123 45 67", email: "", websiteUrl: "", logoUrl: null });
    render(<I18nProvider><BusinessProfileForm /></I18nProvider>);
    await screen.findByDisplayValue("Lumi Beauty");
    await waitFor(() => expect(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") })).toBeEnabled());
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await waitFor(() => expect(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") })).toBeEnabled());
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("wizard.saveChanges", undefined, "ru") }));
    await waitFor(() => expect(api.updateBusinessProfile).toHaveBeenCalled());
    expect(api.createBusinessProfile).not.toHaveBeenCalled();
  });

  it("returns from the first FTUE step to role selection without creating a profile", async () => {
    const { onBackToRole } = renderCreate();
    await userEvent.setup().click(screen.getAllByRole("button", { name: translate("common.back", undefined, "ru") })[0]);
    expect(onBackToRole).toHaveBeenCalledTimes(1);
    expect(api.createBusinessProfile).not.toHaveBeenCalled();
  });

  it("prevents a double submit while the profile request is pending", async () => {
    const user = userEvent.setup();
    let resolveCreate: (() => void) | undefined;
    api.createBusinessProfile.mockImplementation(() => new Promise<void>((resolve) => { resolveCreate = resolve; }));
    renderCreate();
    await completeStepOne(user);
    await completeStepTwo(user);
    const submit = screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") });
    await user.click(submit);
    fireEvent.click(submit);
    expect(api.createBusinessProfile).toHaveBeenCalledTimes(1);
    resolveCreate?.();
  });
});
