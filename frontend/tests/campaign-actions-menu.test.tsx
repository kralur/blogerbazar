import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CampaignActionsMenu } from "../src/components/CampaignActionsMenu";
import { ApiError } from "../src/api/client";
import { I18nProvider, translate } from "../src/i18n";

const api = vi.hoisted(() => ({ closeMyCampaign: vi.fn(), reopenMyCampaign: vi.fn(), deleteMyCampaign: vi.fn() }));
vi.mock("../src/api/marketplace", () => api);
vi.mock("../src/telegram/TelegramProvider", () => ({ useTelegram: () => ({ haptic: { selection: vi.fn(), success: vi.fn(), error: vi.fn() }, registerBackButtonHandler: () => () => undefined }) }));

const ru = (key: string, values?: Record<string, string | number>) => translate(key, values, "ru");
const campaign = { id: "c1", title: "Запуск", status: 1 as const, applicationsCount: 0, deadline: null };

function openMenu(target = campaign, onResult = vi.fn()) {
  render(<I18nProvider><CampaignActionsMenu campaign={target} onResult={onResult} /></I18nProvider>);
  fireEvent.click(screen.getByRole("button", { name: ru("campaignMenu.openAria", { title: target.title }) }));
  return onResult;
}

describe("campaign actions menu", () => {
  beforeEach(() => vi.clearAllMocks());

  it("offers edit, close and delete for a published campaign without applications", () => {
    openMenu();
    expect(screen.getByRole("button", { name: ru("myCampaignDetails.edit") })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: ru("campaignMenu.close") })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: ru("campaignMenu.delete") })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: ru("campaignMenu.reopen") })).not.toBeInTheDocument();
  });

  it("hides delete once bloggers applied and explains why", () => {
    openMenu({ ...campaign, applicationsCount: 2 });
    expect(screen.queryByRole("button", { name: ru("campaignMenu.delete") })).not.toBeInTheDocument();
    expect(screen.getByText(ru("campaignMenu.deleteUnavailable"))).toBeInTheDocument();
    expect(screen.getByRole("button", { name: ru("applications.openInbox") })).toBeInTheDocument();
  });

  it("publishes a closed campaign again", async () => {
    api.reopenMyCampaign.mockResolvedValue({});
    const onResult = openMenu({ ...campaign, status: 2 });
    expect(screen.queryByRole("button", { name: ru("myCampaignDetails.edit") })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: ru("campaignMenu.reopen") }));
    await waitFor(() => expect(onResult).toHaveBeenCalledWith({ kind: "status", status: 1, message: ru("campaignMenu.reopened") }));
    expect(api.reopenMyCampaign).toHaveBeenCalledWith("c1");
  });

  it("deletes only after confirmation", async () => {
    api.deleteMyCampaign.mockResolvedValue(undefined);
    const onResult = openMenu();
    fireEvent.click(screen.getByRole("button", { name: ru("campaignMenu.delete") }));
    expect(api.deleteMyCampaign).not.toHaveBeenCalled();
    expect(screen.getByText(ru("campaignMenu.deleteDescription"))).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: ru("campaignMenu.delete") }));
    await waitFor(() => expect(onResult).toHaveBeenCalledWith({ kind: "deleted", message: ru("campaignMenu.deleted") }));
  });

  it("reports a campaign that got an application meanwhile", async () => {
    api.deleteMyCampaign.mockRejectedValue(new ApiError(409, "campaign_has_applications"));
    const onResult = openMenu();
    fireEvent.click(screen.getByRole("button", { name: ru("campaignMenu.delete") }));
    fireEvent.click(screen.getByRole("button", { name: ru("campaignMenu.delete") }));
    await waitFor(() => expect(onResult).toHaveBeenCalledWith({ kind: "failed", message: ru("error.campaign_has_applications"), reload: true }));
  });
});
