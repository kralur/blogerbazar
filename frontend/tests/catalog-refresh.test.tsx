import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { useEffect } from "react";
import { describe, expect, it, vi } from "vitest";
import { usePaginatedCatalog } from "../src/components/catalog/usePaginatedCatalog";

type Item = { id: string; name: string };

function CatalogHarness({ fetchPage }: { fetchPage: (page: number, signal: AbortSignal) => Promise<{ items: Item[]; total: number; page: number; hasMore: boolean }> }) {
  const catalog = usePaginatedCatalog<Item>({ active: true, fetchPage });
  useEffect(() => { void catalog.load(1, false); }, [catalog.load]);
  return <div>
    {catalog.loading && !catalog.loadedInitialResult && <span>initial-loading</span>}
    {catalog.items.map((item) => <p key={item.id}>{item.name}</p>)}
    {catalog.loadedInitialResult && catalog.failure && <span>stale-error</span>}
    <button onClick={() => void catalog.load(1, false, true)} type="button">refresh</button>
    <button onClick={catalog.cancel} type="button">cancel</button>
  </div>;
}

describe("usePaginatedCatalog refresh states", () => {
  it("shows a skeleton only before the first result and preserves data during a refresh", async () => {
    let resolveRefresh!: (value: { items: Item[]; total: number; page: number; hasMore: boolean }) => void;
    const fetchPage = vi.fn()
      .mockResolvedValueOnce({ items: [{ id: "one", name: "First" }], total: 1, page: 1, hasMore: false })
      .mockImplementationOnce(() => new Promise((resolve) => { resolveRefresh = resolve; }));

    render(<CatalogHarness fetchPage={fetchPage} />);
    expect(screen.getByText("initial-loading")).toBeInTheDocument();
    await screen.findByText("First");

    fireEvent.click(screen.getByRole("button", { name: "refresh" }));
    expect(screen.getByText("First")).toBeInTheDocument();
    expect(screen.queryByText("initial-loading")).not.toBeInTheDocument();

    resolveRefresh({ items: [{ id: "two", name: "Updated" }], total: 1, page: 1, hasMore: false });
    await screen.findByText("Updated");
    expect(screen.queryByText("First")).not.toBeInTheDocument();
  });

  it("keeps stale content visible when a background refresh fails", async () => {
    const fetchPage = vi.fn()
      .mockResolvedValueOnce({ items: [{ id: "one", name: "First" }], total: 1, page: 1, hasMore: false })
      .mockRejectedValueOnce(new Error("network unavailable"));

    render(<CatalogHarness fetchPage={fetchPage} />);
    await screen.findByText("First");
    fireEvent.click(screen.getByRole("button", { name: "refresh" }));

    await screen.findByText("stale-error");
    expect(screen.getByText("First")).toBeInTheDocument();
  });

  it("finishes loading after an aborted initial request", async () => {
    let resolveRequest!: (value: { items: Item[]; total: number; page: number; hasMore: boolean }) => void;
    const fetchPage = vi.fn().mockImplementation(() => new Promise((resolve) => { resolveRequest = resolve; }));

    render(<CatalogHarness fetchPage={fetchPage} />);
    expect(screen.getByText("initial-loading")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "cancel" }));
    await waitFor(() => expect(screen.queryByText("initial-loading")).not.toBeInTheDocument());

    resolveRequest({ items: [{ id: "one", name: "First" }], total: 1, page: 1, hasMore: false });
    await waitFor(() => expect(screen.queryByText("First")).not.toBeInTheDocument());
  });
});
