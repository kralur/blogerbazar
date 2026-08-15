import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({
  createBloggerProfile: vi.fn(), deleteProfileImage: vi.fn(), getMyBloggerProfile: vi.fn(), updateBloggerProfile: vi.fn(), uploadProfileImage: vi.fn()
}));
const telegram = vi.hoisted(() => ({
  haptic: { selection: vi.fn(), success: vi.fn(), warning: vi.fn(), error: vi.fn() },
  registerBackButtonHandler: vi.fn(() => vi.fn()), setBackButtonHandler: vi.fn(), setClosingConfirmation: vi.fn(), isTelegram: true,
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

import { BloggerProfileForm } from "../src/pages/BloggerProfileForm";

function renderCreate(onBackToRole = vi.fn()) {
  const onCompleted = vi.fn();
  render(<I18nProvider><BloggerProfileForm onBackToRole={onBackToRole} onCompleted={onCompleted} /></I18nProvider>);
  return { onBackToRole, onCompleted };
}

const continueLabel = () => translate("wizard.continue", undefined, "ru");

async function completeBasic(user: ReturnType<typeof userEvent.setup>) {
  fireEvent.change(screen.getByPlaceholderText("+998 90 123 45 67"), { target: { value: "+998 88 123 45 67" } });
  await waitFor(() => expect(screen.getByRole("button", { name: continueLabel() })).toBeEnabled());
  await user.click(screen.getByRole("button", { name: continueLabel() }));
}

async function completeAudience(user: ReturnType<typeof userEvent.setup>) {
  await waitFor(() => expect(screen.getByRole("button", { name: continueLabel() })).toBeEnabled());
  await user.click(screen.getByRole("button", { name: continueLabel() }));
}

async function completePrices(user: ReturnType<typeof userEvent.setup>) {
  await waitFor(() => expect(screen.getByRole("button", { name: continueLabel() })).toBeEnabled());
  await user.click(screen.getByRole("button", { name: continueLabel() }));
}

async function reachReview(user: ReturnType<typeof userEvent.setup>) {
  await completeBasic(user);
  await completeAudience(user);
  await completePrices(user);
  await user.click(screen.getByRole("button", { name: continueLabel() }));
}

describe("Blogger profile wizard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getMyBloggerProfile.mockRejectedValue(new Error("not found"));
    api.createBloggerProfile.mockResolvedValue({});
    api.updateBloggerProfile.mockResolvedValue({});
    api.uploadProfileImage.mockResolvedValue({ url: "https://cdn.example/avatar.png" });
    api.deleteProfileImage.mockResolvedValue(undefined);
    telegram.user = { first_name: "Madina", username: "madina" };
    telegram.isTelegram = true;
  });

  it("starts at Basic and validates the required current step", async () => {
    const user = userEvent.setup();
    renderCreate();
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.bloggerBasicStep", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getByDisplayValue("Madina")).toBeInTheDocument();
    expect(screen.getByPlaceholderText(translate("form.blogger.lastNamePlaceholder", undefined, "ru"))).toHaveValue("");
    expect(document.querySelector(".wizard-screen__top-scrim")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: continueLabel() })).toBeDisabled();
    await user.click(screen.getByPlaceholderText("+998 90 123 45 67"));
    await user.tab();
    expect(screen.getByText(translate("form.validation.phone", undefined, "ru"))).toBeInTheDocument();
    expect(api.createBloggerProfile).not.toHaveBeenCalled();
  });

  it("moves through all five steps without calling an API before Review", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeBasic(user);
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.bloggerAudienceStep", undefined, "ru") })).toBeInTheDocument();
    await completeAudience(user);
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.bloggerPricesStep", undefined, "ru") })).toBeInTheDocument();
    await completePrices(user);
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.bloggerPortfolioStep", undefined, "ru") })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: continueLabel() }));
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.bloggerReviewStep", undefined, "ru") })).toBeInTheDocument();
    expect(api.createBloggerProfile).not.toHaveBeenCalled();
    expect(api.updateBloggerProfile).not.toHaveBeenCalled();
  });

  it("preserves categories, numbers, prices and barter while navigating back", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeBasic(user);
    await user.click(screen.getByRole("button", { name: "choose other" }));
    await completeAudience(user);
    await user.click(screen.getByRole("switch", { name: translate("form.barterTitle", undefined, "ru") }));
    await completePrices(user);
    await user.click(screen.getAllByRole("button", { name: translate("common.back", undefined, "ru") })[0]);
    await user.click(screen.getAllByRole("button", { name: translate("common.back", undefined, "ru") })[0]);
    expect(screen.getByRole("button", { name: "choose other" })).toBeInTheDocument();
    await completeAudience(user);
    expect(screen.getByRole("switch", { name: translate("form.barterTitle", undefined, "ru") })).toHaveAttribute("aria-checked", "false");
  });

  it("returns Review Change actions to the right step with accessible names", async () => {
    const user = userEvent.setup();
    renderCreate();
    await reachReview(user);
    const change = screen.getByRole("button", { name: translate("wizard.changeSection", { section: translate("wizard.bloggerAudienceStep", undefined, "ru") }, "ru") });
    expect(change).toHaveTextContent(translate("wizard.change", undefined, "ru"));
    await user.click(change);
    expect(screen.getByRole("heading", { level: 1, name: translate("wizard.bloggerAudienceStep", undefined, "ru") })).toBeInTheDocument();
  });

  it("renders one localized empty state for an empty optional portfolio section", async () => {
    const user = userEvent.setup();
    renderCreate();
    await reachReview(user);
    const section = screen.getByRole("heading", { level: 3, name: translate("wizard.bloggerPortfolioStep", undefined, "ru") }).closest("section");
    expect(section).toHaveTextContent(translate("common.notSpecified", undefined, "ru"));
  });

  it("submits the legacy-compatible numeric payload and transformed portfolio/platform arrays", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeBasic(user);
    await completeAudience(user);
    await completePrices(user);
    await user.type(screen.getByPlaceholderText("https://..."), "portfolio.example");
    await user.type(screen.getAllByPlaceholderText("@username")[0], "@madina_style");
    await user.type(screen.getByPlaceholderText("https://youtube.com/@channel"), "youtube.com/@madina");
    await user.click(screen.getByRole("button", { name: continueLabel() }));
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    await waitFor(() => expect(api.createBloggerProfile).toHaveBeenCalledWith(expect.objectContaining({
      name: "Madina", username: "@madina", phone: "+998 88 123 45 67", totalFollowers: 10000, averageReach: 25000, engagementRate: 5.5, storiesPrice: 250000, reelsPrice: 500000, barterEnabled: true,
      portfolioItems: [{ title: translate("form.blogger.portfolioTitle", undefined, "ru"), type: "IMAGE", url: "https://portfolio.example" }],
      platforms: expect.arrayContaining([{ type: "instagram", url: "https://instagram.com/madina_style" }, { type: "youtube", url: "https://youtube.com/@madina" }])
    })));
  });

  it("renders the visible RU ER and currency adornments without adding them to editable values", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeBasic(user);
    expect(screen.getByText(translate("form.engagementRateLabel", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getByText(translate("form.engagementRateHelper", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getByText("%")).toBeInTheDocument();
    expect(screen.getByDisplayValue("5.5")).toHaveValue("5.5");
    await completeAudience(user);
    expect(screen.getAllByText(translate("currency.uzs", undefined, "ru"))).toHaveLength(4);
    expect(screen.getByDisplayValue("250 000")).toHaveValue("250 000");
    expect(document.querySelectorAll(".input-control__input--with-suffix")).toHaveLength(4);
  });

  it("accepts a comma or dot ER value, submits numeric 5.5 and localizes the Review percentage", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeBasic(user);
    fireEvent.change(screen.getByDisplayValue("5.5"), { target: { value: "5,5" } });
    await completeAudience(user);
    await completePrices(user);
    await user.click(screen.getByRole("button", { name: continueLabel() }));
    expect(screen.getByText("5,5%")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    await waitFor(() => expect(api.createBloggerProfile).toHaveBeenCalledWith(expect.objectContaining({ engagementRate: 5.5, storiesPrice: 250000, reelsPrice: 500000 })));
  });

  it("keeps optional empty prices out of the payload and preserves Other", async () => {
    const user = userEvent.setup();
    renderCreate();
    await completeBasic(user);
    await user.click(screen.getByRole("button", { name: "choose other" }));
    await completeAudience(user);
    await user.clear(screen.getByDisplayValue("350 000"));
    await user.clear(screen.getByDisplayValue("900 000"));
    await completePrices(user);
    await user.click(screen.getByRole("button", { name: continueLabel() }));
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    await waitFor(() => expect(api.createBloggerProfile).toHaveBeenCalledWith(expect.objectContaining({ categories: ["other:Music"], postPrice: undefined, integrationPrice: undefined })));
  });

  it("runs media only after core save and does not duplicate the create request on media failure", async () => {
    const user = userEvent.setup();
    const order: string[] = [];
    api.createBloggerProfile.mockImplementation(async () => { order.push("core"); });
    api.uploadProfileImage.mockImplementation(async () => { order.push("media"); throw new Error("storage unavailable"); });
    const { onCompleted } = renderCreate();
    await completeBasic(user);
    await completeAudience(user);
    await completePrices(user);
    await user.click(screen.getByRole("button", { name: "media" }));
    await user.click(screen.getByRole("button", { name: continueLabel() }));
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    await waitFor(() => expect(api.createBloggerProfile).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(api.uploadProfileImage).toHaveBeenCalledWith("blogger", expect.any(File)));
    expect(order).toEqual(["core", "media"]);
    expect(onCompleted).toHaveBeenCalledTimes(1);
  });

  it("routes a server validation error to its matching step without hiding client errors", async () => {
    const user = userEvent.setup();
    api.createBloggerProfile.mockRejectedValue(new ApiError(400, "validation_failed", ["StoriesPrice"]));
    renderCreate();
    await reachReview(user);
    await user.click(screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") }));
    expect(await screen.findByRole("heading", { level: 1, name: translate("wizard.bloggerPricesStep", undefined, "ru") })).toBeInTheDocument();
    expect(screen.getAllByText(translate("form.validation.stories", undefined, "ru")).length).toBeGreaterThan(0);
  });

  it("hydrates edit mode and retains the PUT branch", async () => {
    const user = userEvent.setup();
    api.getMyBloggerProfile.mockResolvedValue({
      name: "Aziza", lastName: "Karimova", username: "@aziza", city: "samarkand", phone: "+998 90 123 45 67", email: "aziza@example.com", totalFollowers: 12000, averageReach: 3000, engagementRate: 4.5, storiesPrice: 200000, reelsPrice: 300000, postPrice: null, integrationPrice: null, barterEnabled: false, categories: ["beauty"], bio: "Bio", avatarUrl: null, portfolioItems: [{ id: "p1", title: "Work", type: 0, url: "https://portfolio.example" }], platforms: [{ id: "i1", type: "instagram", url: "https://instagram.com/aziza" }]
    });
    render(<I18nProvider><BloggerProfileForm /></I18nProvider>);
    await screen.findByDisplayValue("Aziza");
    await completeBasic(user);
    await completeAudience(user);
    expect(screen.getByDisplayValue("200 000")).toHaveValue("200 000");
    expect(screen.getAllByText(translate("currency.uzs", undefined, "ru"))).toHaveLength(4);
    await completePrices(user);
    await user.click(screen.getByRole("button", { name: continueLabel() }));
    await user.click(screen.getByRole("button", { name: translate("wizard.saveChanges", undefined, "ru") }));
    await waitFor(() => expect(api.updateBloggerProfile).toHaveBeenCalledTimes(1));
    expect(api.createBloggerProfile).not.toHaveBeenCalled();
  });

  it("uses one native back handler and sends first FTUE step back to Role Selection", async () => {
    const { onBackToRole } = renderCreate();
    await waitFor(() => expect(telegram.setBackButtonHandler).toHaveBeenCalledTimes(1));
    const nativeBack = telegram.setBackButtonHandler.mock.calls[0][0] as () => void;
    nativeBack();
    expect(onBackToRole).toHaveBeenCalledTimes(1);
  });

  it("prevents duplicate final submit while the request is pending", async () => {
    const user = userEvent.setup();
    let resolveRequest: (() => void) | undefined;
    api.createBloggerProfile.mockImplementation(() => new Promise<void>((resolve) => { resolveRequest = resolve; }));
    renderCreate();
    await reachReview(user);
    const submit = screen.getByRole("button", { name: translate("wizard.createProfile", undefined, "ru") });
    await user.click(submit);
    fireEvent.click(submit);
    expect(api.createBloggerProfile).toHaveBeenCalledTimes(1);
    resolveRequest?.();
  });

  it("renders the UZ ER copy, optional price labels and currency suffixes", async () => {
    const user = userEvent.setup();
    localStorage.setItem("bloggerbazar.language", "uz");
    renderCreate();
    fireEvent.change(screen.getByPlaceholderText("+998 90 123 45 67"), { target: { value: "+998 88 123 45 67" } });
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "uz") }));
    expect(screen.getByText(translate("form.engagementRateLabel", undefined, "uz"))).toBeInTheDocument();
    expect(screen.getByText(translate("form.engagementRateHelper", undefined, "uz"))).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: translate("wizard.continue", undefined, "uz") }));
    expect(screen.getAllByText(translate("currency.uzs", undefined, "uz"))).toHaveLength(4);
    expect(screen.getByText(translate("form.optionalField", { label: translate("card.post", undefined, "uz") }, "uz"))).toBeInTheDocument();
    localStorage.setItem("bloggerbazar.language", "ru");
  });
});
