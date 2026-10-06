import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { I18nProvider } from "../src/i18n";
import { FixedActionBar } from "../src/components/ui";

describe("FixedActionBar", () => {
  it("asks the page to reserve room while it is shown", () => {
    const { unmount } = render(<I18nProvider><FixedActionBar><button type="button">Act</button></FixedActionBar></I18nProvider>);
    expect(document.body.dataset.fixedActionBar).toBe("true");

    unmount();
    expect(document.body.dataset.fixedActionBar).toBeUndefined();
  });
});
