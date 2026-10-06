import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider, translate } from "../src/i18n";
import { AppErrorBoundary } from "../src/components/AppErrorBoundary";

function Broken(): JSX.Element {
  throw new Error("render failed");
}

describe("AppErrorBoundary", () => {
  it("shows a retry screen instead of a blank app when rendering fails", () => {
    const consoleError = vi.spyOn(console, "error").mockImplementation(() => undefined);
    render(<I18nProvider><AppErrorBoundary><Broken /></AppErrorBoundary></I18nProvider>);

    expect(screen.getByText(translate("ui.errorTitle", undefined, "ru"))).toBeInTheDocument();
    expect(screen.getByRole("button", { name: translate("common.retry", undefined, "ru") })).toBeInTheDocument();
    consoleError.mockRestore();
  });
});
