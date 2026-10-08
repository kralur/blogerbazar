import { act, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { sessionExpiredEvent } from "../src/api/client";
import { SessionExpiredNotice } from "../src/components/SessionExpiredNotice";
import { I18nProvider, translate } from "../src/i18n";

describe("session expired notice", () => {
  it("asks to reopen the app once a request comes back 401", () => {
    render(<I18nProvider><SessionExpiredNotice /></I18nProvider>);
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();

    act(() => { window.dispatchEvent(new Event(sessionExpiredEvent)); });

    expect(screen.getByRole("alert")).toHaveTextContent(translate("error.session_expired", undefined, "ru"));
  });
});
