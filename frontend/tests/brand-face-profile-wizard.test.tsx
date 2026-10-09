import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  addBrandFacePhoto: vi.fn(),
  brandFaceFormats: ["photoShoot", "video", "ugc", "event", "ambassador"],
  removeBrandFacePhoto: vi.fn(),
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

// Languages are picked from the list by their localized names (D49).
async function pickLanguages(user: ReturnType<typeof userEvent.setup>, codes: string[]) {
  const picker = within(screen.getByRole("region", { name: translate("brandFace.languages", undefined, "ru") }));
  for (const code of codes) await user.click(picker.getAllByRole("button", { name: translate(`language.${code}`, undefined, "ru") })[0]);
}

async function completeStepOne(user: ReturnType<typeof userEvent.setup>, languages = ["ru", "uz"]) {
  // Gender and age are required (QA Q20).
  await user.click(screen.getByRole("button", { name: translate("brandFace.gender.female", undefined, "ru") }));
  await user.type(screen.getByPlaceholderText("24"), "24");
  await pickLanguages(user, languages);
  await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
}

async function completeStepTwo(user: ReturnType<typeof userEvent.setup>, other = false) {
  await user.click(screen.getByRole("button", { name: other ? "choose other" : "choose category" }));
  await user.click(screen.getByRole("button", { name: translate("brandFace.format.photoShoot", undefined, "ru") }));
  await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
}

async function fillInstagram(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByPlaceholderText("@username"), "@madina_style");
}

async function reachReview(user: ReturnType<typeof userEvent.setup>) {
  await completeStepOne(user);
  await completeStepTwo(user);
  await fillInstagram(user);
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

  // Negative paths of the required fields (QA Q20): the step cannot be left and the reason is shown.
  it("blocks step one without a gender and shows why", async () => {
    const user = userEvent.setup();
    renderCreate();
    await user.type(screen.getByPlaceholderText("24"), "24");
    await pickLanguages(user, ["ru"]);
    expect(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: translate("brandFace.gender.male", undefined, "ru") }));
    expect(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") })).toBeEnabled();
  });

  it.each([["", "empty"], ["15", "too young"], ["81", "too old"]])("rejects the age %s (%s)", async (age) => {
    const user = userEvent.setup();
    renderCreate();
    await user.click(screen.getByRole("button", { name: translate("brandFace.gender.female", undefined, "ru") }));
    await pickLanguages(user, ["ru"]);
    const ageInput = screen.getByPlaceholderText("24");
    if (age) await user.type(ageInput, age);
    await user.click(ageInput);
    await user.tab();
    expect(screen.getByText(translate("brandFace.ageInvalid", { min: 16, max: 80 }, "ru"))).toBeInTheDocument();
    expect(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") })).toBeDisabled();
  });

  it("keeps only digits in the age field", async () => {
    const user = userEvent.setup();
    renderCreate();
    await user.type(screen.getByPlaceholderText("24"), "2a5!7");
    expect(screen.getByPlaceholderText("24")).toHaveValue("25");
  });

  it("blocks step two until at least one format is chosen and unblocks after", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeStepOne(user);
    await user.click(screen.getByRole("button", { name: "choose category" }));
    const continueButton = screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") });
    expect(continueButton).toBeDisabled();
    await user.click(screen.getByRole("button", { name: translate("brandFace.format.ugc", undefined, "ru") }));
    expect(continueButton).toBeEnabled();
    await user.click(screen.getByRole("button", { name: translate("brandFace.format.ugc", undefined, "ru") }));
    expect(continueButton).toBeDisabled();
    expect(screen.getByText(translate("brandFace.formatsRequired", undefined, "ru"))).toBeInTheDocument();
  });

  it("rejects an invalid Instagram handle and an insecure showreel link", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeStepOne(user);
    await completeStepTwo(user);
    await user.type(screen.getByPlaceholderText("@username"), "not a handle");
    await user.type(screen.getByPlaceholderText("https://instagram.com/reel/..."), "http://insecure.example");
    await user.tab();
    expect(screen.getAllByText(translate("form.validation.socialUsername", undefined, "ru")).length).toBeGreaterThan(0);
    expect(screen.getAllByText(translate("form.validation.website", undefined, "ru")).length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") })).toBeDisabled();
  });

  it("refuses a non-image or too large gallery file and stops adding after four photos", async () => {
    const user = userEvent.setup({ applyAccept: false });
    renderCreate();
    await completeStepOne(user);
    await completeStepTwo(user);
    const add = () => screen.getByLabelText(translate("brandFace.galleryAdd", undefined, "ru"));
    await user.upload(add(), new File(["text"], "notes.txt", { type: "text/plain" }));
    expect(screen.getByText(translate("profileMedia.invalidFile", undefined, "ru"))).toBeInTheDocument();
    await user.upload(add(), new File([new Uint8Array(5 * 1024 * 1024 + 1)], "huge.png", { type: "image/png" }));
    expect(screen.getByText(translate("profileMedia.invalidFile", undefined, "ru"))).toBeInTheDocument();
    for (let index = 0; index < 4; index++) await user.upload(add(), new File(["photo"], `photo-${index}.png`, { type: "image/png" }));
    expect(screen.queryByLabelText(translate("brandFace.galleryAdd", undefined, "ru"))).not.toBeInTheDocument();
    expect(screen.queryByText(translate("profileMedia.invalidFile", undefined, "ru"))).not.toBeInTheDocument();
  });

  it("shows the server photo limit as a warning without losing the saved profile", async () => {
    const user = userEvent.setup();
    api.addBrandFacePhoto.mockRejectedValue(new ApiError(409, "photo_limit"));
    const { onCompleted } = renderCreate();
    await completeStepOne(user);
    await completeStepTwo(user);
    await user.upload(screen.getByLabelText(translate("brandFace.galleryAdd", undefined, "ru")), new File(["photo"], "photo.png", { type: "image/png" }));
    await fillInstagram(user);
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    await waitFor(() => expect(onCompleted).toHaveBeenCalledTimes(1));
    expect(api.upsertBrandFaceProfile).toHaveBeenCalledTimes(1);
    expect(telegram.haptic.warning).toHaveBeenCalled();
  });

  it("routes server errors for gender, age and formats back to their steps", async () => {
    const user = userEvent.setup();
    api.upsertBrandFaceProfile.mockRejectedValue(new ApiError(400, "validation_failed", ["Formats"]));
    renderCreate();
    await reachReview(user);
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    expect(await screen.findByRole("heading", { level: 1, name: translate("wizard.brandFacePositioningStep", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getAllByText(translate("brandFace.formatsRequired", undefined, "ru")).length).toBeGreaterThan(0);
  });

  it("validates the first step before moving to Positioning", async () => {
    const user = userEvent.setup();
    renderCreate();
    await user.click(screen.getByRole("button", { name: translate("brandFace.gender.female", undefined, "ru") }));
    await user.type(screen.getByPlaceholderText("24"), "24");
    expect(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") })).toBeDisabled();
    await pickLanguages(user, ["ru"]);
    await pickLanguages(user, ["ru"]);
    expect(screen.getByText(translate("brandFace.languagesRequired", undefined, "ru"))).toBeInTheDocument();
    await pickLanguages(user, ["uz"]);
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.brandFacePositioningStep", undefined, "ru") })).toBeInTheDocument();
  });

  it("keeps master state when navigating between steps", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeStepOne(user, ["ru", "en"]);
    await completeStepTwo(user, true);
    await user.click(screen.getByRole("button", { name: translate("common.back", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("common.back", undefined, "ru") }));
    expect(screen.getByRole("button", { name: translate("languageSelect.remove", { language: translate("language.en", undefined, "ru") }, "ru") })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    expect(screen.getByText("@madina")).toBeInTheDocument();
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
    await user.click(screen.getByRole("button", { name: translate("wizard.changeSection", { section: translate("wizard.brandFacePositioningStep", undefined, "ru") }, "ru") }));
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.brandFacePositioningStep", undefined, "ru") })).toBeInTheDocument();
  });

  it("requires Instagram before Review: the business looks at the photos there (QA Q17)", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeStepOne(user);
    await completeStepTwo(user);
    expect(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") })).toBeDisabled();
    await user.click(screen.getByPlaceholderText("@username"));
    await user.tab();
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.brandFacePortfolioStep", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getAllByText(translate("brandFace.instagramRequired", undefined, "ru")).length).toBeGreaterThan(0);
    expect(screen.queryByText(translate("brandFace.experience", undefined, "ru"))).not.toBeInTheDocument();
    expect(api.upsertBrandFaceProfile).not.toHaveBeenCalled();
  });

  it("shows only supplied optional values on the populated Review section", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeStepOne(user);
    await completeStepTwo(user);
    await user.type(screen.getByPlaceholderText("@username"), "@madina_style");
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    const section = screen.getByRole("heading", { level: 3, name: translate("wizard.brandFacePortfolioStep", undefined, "ru") }).closest("section");
    expect(section).toHaveTextContent("@madina_style");
    expect(section).not.toHaveTextContent(translate("common.notSpecified", undefined, "ru"));
    expect(section).not.toHaveTextContent(translate("brandFace.portfolio", undefined, "ru"));
  });

  it("uses the short Change action with a section-specific accessible name", async () => {
    const user = userEvent.setup();
    renderCreate();
    await reachReview(user);
    const change = screen.getByRole("button", { name: translate("wizard.changeSection", { section: translate("wizard.brandFacePositioningStep", undefined, "ru") }, "ru") });
    expect(change).toHaveTextContent(translate("wizard.change", undefined, "ru"));
  });

  it("allows at most five languages and offers only the list, searchable, without free text (D49)", async () => {
    const user = userEvent.setup();
    renderCreate();
    await pickLanguages(user, ["uz", "ru", "en", "kk", "tr"]);
    const picker = within(screen.getByRole("region", { name: translate("brandFace.languages", undefined, "ru") }));
    expect(picker.getByRole("button", { name: translate("language.de", undefined, "ru") })).toBeDisabled();
    await user.type(screen.getByPlaceholderText(translate("languageSelect.searchPlaceholder", undefined, "ru")), "кор");
    expect(picker.getByRole("button", { name: translate("language.ko", undefined, "ru") })).toBeInTheDocument();
    expect(picker.queryByRole("button", { name: translate("language.de", undefined, "ru") })).not.toBeInTheDocument();
    await user.clear(screen.getByPlaceholderText(translate("languageSelect.searchPlaceholder", undefined, "ru")));
    await user.type(screen.getByPlaceholderText(translate("languageSelect.searchPlaceholder", undefined, "ru")), "эльфийский");
    expect(screen.getByText(translate("languageSelect.nothingFound", undefined, "ru"))).toBeInTheDocument();
  });

  it("maps old free-text languages to codes when an old profile is edited", async () => {
    const user = userEvent.setup();
    api.getMyBrandFaceProfile.mockResolvedValue({ id: "bf-1", name: "Madina", city: "tashkent-city", languages: ["Русский", "O‘zbekcha", "клингонский"], categories: ["beauty"], telegram: "@madina", instagram: "@madina", gender: "female", age: 24, formats: ["video"] });
    render(<I18nProvider><BrandFaceProfileForm /></I18nProvider>);
    await screen.findByDisplayValue("Madina");
    for (let index = 0; index < 3; index++) await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("wizard.saveChanges", undefined, "ru") }));
    await waitFor(() => expect(api.upsertBrandFaceProfile).toHaveBeenCalledWith(expect.objectContaining({ languages: ["ru", "uz"] })));
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
    await fillInstagram(user);
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    await waitFor(() => expect(api.upsertBrandFaceProfile).toHaveBeenCalledWith(expect.objectContaining({
      name: "Madina",
      city: "tashkent-city",
      languages: ["ru", "uz"],
      categories: ["beauty"],
      telegram: "@madina",
      instagram: "@madina_style",
      experience: null,
      age: 24,
      gender: "female",
      formats: ["photoShoot"],
      showreelUrl: null
    })));
    await waitFor(() => expect(api.uploadProfileImage).toHaveBeenCalledWith("brand-face", expect.any(File)));
    expect(order).toEqual(["core", "media"]);
  });

  it("returns to the profile after saving an edit, like the blogger and business forms", async () => {
    const user = userEvent.setup();
    api.getMyBrandFaceProfile.mockResolvedValue({ id: "bf-1", name: "Madina", city: "tashkent-city", languages: ["Русский"], categories: ["beauty"], telegram: "@madina", instagram: "@madina", gender: "female", age: 24, formats: ["video"] });
    window.location.hash = "#/brand-face";
    render(<I18nProvider><BrandFaceProfileForm /></I18nProvider>);
    await screen.findByDisplayValue("Madina");
    for (let index = 0; index < 3; index++) await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("wizard.saveChanges", undefined, "ru") }));

    expect(await screen.findByText(translate("brandFace.saved", undefined, "ru"))).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("form.understood", undefined, "ru") }));
    expect(window.location.hash).toBe("#/profile");
  });

  it("uploads new gallery photos and removes dropped ones after the profile is saved (QA Q20)", async () => {
    const user = userEvent.setup();
    api.getMyBrandFaceProfile.mockResolvedValue({ id: "bf-1", name: "Madina", city: "tashkent-city", languages: ["Русский"], categories: ["beauty"], telegram: "@madina", instagram: "@madina", gender: "female", age: 24, formats: ["video"], photoUrls: ["https://cdn.example/old.webp"] });
    api.removeBrandFacePhoto.mockResolvedValue({ photoUrls: [] });
    api.addBrandFacePhoto.mockResolvedValue({ photoUrls: ["https://cdn.example/new.webp"] });
    const order: string[] = [];
    api.upsertBrandFaceProfile.mockImplementation(async () => { order.push("core"); return {}; });
    api.removeBrandFacePhoto.mockImplementation(async () => { order.push("remove"); return { photoUrls: [] }; });
    api.addBrandFacePhoto.mockImplementation(async () => { order.push("add"); return { photoUrls: ["https://cdn.example/new.webp"] }; });
    render(<I18nProvider><BrandFaceProfileForm /></I18nProvider>);
    await screen.findByDisplayValue("Madina");
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("brandFace.galleryRemove", { index: 1 }, "ru") }));
    await user.upload(screen.getByLabelText(translate("brandFace.galleryAdd", undefined, "ru")), new File(["photo"], "photo.png", { type: "image/png" }));
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "ru") }));
    await user.click(screen.getByRole("button", { name: translate("wizard.saveChanges", undefined, "ru") }));

    await waitFor(() => expect(api.addBrandFacePhoto).toHaveBeenCalledWith(expect.any(File)));
    expect(api.removeBrandFacePhoto).toHaveBeenCalledWith("https://cdn.example/old.webp");
    expect(order).toEqual(["core", "remove", "add"]);
  });

  it("preserves the Other category in the final payload", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeStepOne(user);
    await completeStepTwo(user, true);
    await fillInstagram(user);
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
    await fillInstagram(user);
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
    api.upsertBrandFaceProfile.mockRejectedValue(new ApiError(400, "validation_failed", ["Instagram"]));
    renderCreate();
    await reachReview(user);
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    expect(await screen.findByRole("heading", { level: 1, name: translate("wizard.brandFacePortfolioStep", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getAllByText(translate("form.validation.socialUsername", undefined, "ru")).length).toBeGreaterThan(0);
  });

  it("hydrates edit mode and uses the same PUT endpoint", async () => {
    const user = userEvent.setup();
    api.getMyBrandFaceProfile.mockResolvedValue({
      name: "Aziza",
      city: "samarkand",
      gender: "female",
      age: 27,
      formats: ["photoShoot"],
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
    // The former "Experience" text moves into "About" (QA Q17), so saving loses nothing.
    await waitFor(() => expect(api.upsertBrandFaceProfile).toHaveBeenCalledWith(expect.objectContaining({ description: "Описание\n\nОпыт", experience: null })));
  });

  it("uses one native BackButton handler and returns the first FTUE step to role selection", async () => {
    const { onBackToRole } = renderCreate();
    await waitFor(() => expect(telegram.setBackButtonHandler).toHaveBeenCalledTimes(1));
    const nativeBack = telegram.setBackButtonHandler.mock.calls[0][0] as () => void;
    nativeBack();
    expect(onBackToRole).toHaveBeenCalledTimes(1);
  });
});
