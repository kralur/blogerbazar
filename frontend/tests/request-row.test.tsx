import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { RequestRow } from "../src/components/RequestRow";

describe("request row", () => {
  it("shows what waits for the viewer instead of the date", () => {
    render(<RequestRow action="Ждёт вашего отзыва" href="#/deal/1" meta="Начата 7 окт." name="Umidjon" status={<span>Завершена</span>} title="Реклама" />);

    expect(screen.getByText("Ждёт вашего отзыва")).toBeInTheDocument();
    expect(screen.queryByText("Начата 7 окт.")).not.toBeInTheDocument();
    expect(screen.getByRole("link")).toHaveClass("request-row--action");
  });

  it("keeps the date on rows with nothing to do", () => {
    render(<RequestRow href="#/deal/2" meta="Начата 6 окт." name="Umidjon" status={<span>В работе</span>} title="Реклама" />);

    expect(screen.getByText("Начата 6 окт.")).toBeInTheDocument();
    expect(screen.getByRole("link")).not.toHaveClass("request-row--action");
  });
});
