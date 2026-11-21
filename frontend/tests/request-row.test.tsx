import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { RequestRow } from "../src/components/RequestRow";

describe("request row", () => {
  it("shows what waits for the viewer instead of the date", () => {
    const { container } = render(<RequestRow action="Ждёт вашего отзыва" href="#/deal/1" meta="Начата 7 окт." name="Umidjon" status={<span>Завершена</span>} title="Реклама" />);

    expect(screen.getByText("Ждёт вашего отзыва")).toBeInTheDocument();
    expect(screen.queryByText(/Начата 7 окт\./)).not.toBeInTheDocument();
    expect(container.querySelector("article")).toHaveClass("person-card--action");
  });

  it("keeps the date on rows with nothing to do", () => {
    const { container } = render(<RequestRow href="#/deal/2" meta="Начата 6 окт." name="Umidjon" status={<span>В работе</span>} title="Реклама" />);

    expect(screen.getByText("Реклама · Начата 6 окт.")).toBeInTheDocument();
    expect(container.querySelector("article")).not.toHaveClass("person-card--action");
  });

  // Owner, 2026-11-21: the same cards in Requests and deals as in Search.
  it("is the person card: the status opens the tag row, the budget sits where the price is", () => {
    const { container } = render(<RequestRow href="#/offer/1" meta="20 нояб." name="Aziza" price="3 000 000" priceLabel="Бюджет" priceUnit="сум" status={<span data-testid="status">Ждёт ответа</span>} tags={["Мода"]} title="Фотосъёмка" />);

    const card = container.querySelector("article.person-card")!;
    expect(card).toHaveClass("person-card--plain");
    expect(card.querySelector(".person-card__tags")!.firstElementChild).toBe(screen.getByTestId("status"));
    expect(card.querySelector(".person-card__tags")).toHaveTextContent("Мода");
    expect(card.querySelector(".person-card__price")).toHaveTextContent("Бюджет3 000 000сум");
    expect(screen.getByRole("link")).toHaveAttribute("href", "#/offer/1");
  });

  it("is a button when it opens a sheet instead of a page", async () => {
    const open = vi.fn();
    render(<RequestRow name="Kamron" onClick={open} status={<span>Новый</span>} title="Городской гид" />);

    await userEvent.setup().click(screen.getByRole("button"));
    expect(open).toHaveBeenCalledTimes(1);
  });
});
