import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { TicketForm } from "./ticket-form";
import { ticketsApi, type Ticket } from "./api";
jest.mock("@/i18n/navigation", () => ({
  useRouter: () => ({ push: jest.fn() }),
}));
jest.mock("./api", () => ({
  ticketsApi: { categories: jest.fn(), edit: jest.fn() },
  ticketPriorities: ["low", "medium", "high", "urgent"],
}));
it("keeps a draft's original concurrency version after a background refetch", async () => {
  const ticket = {
    id: "ticket",
    number: "TKT-1001",
    title: "Original title",
    description: "Original description",
    categoryId: "general",
    tags: [],
    priority: "medium",
    version: 1,
  } as unknown as Ticket;
  jest
    .mocked(ticketsApi.categories)
    .mockResolvedValue([{ id: "general", name: "General" }]);
  jest.mocked(ticketsApi.edit).mockRejectedValue(new Error("Conflict"));
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const view = (value: Ticket) => (
    <QueryClientProvider client={client}>
      <TicketForm ticket={value} onCancel={() => {}} />
    </QueryClientProvider>
  );
  const { rerender } = render(view(ticket));
  await waitFor(() =>
    expect(screen.getByRole("button", { name: "Save changes" })).toBeEnabled(),
  );
  fireEvent.change(screen.getByLabelText("Subject"), {
    target: { value: "My unsaved draft" },
  });
  rerender(view({ ...ticket, description: "Someone else's edit", version: 2 }));
  fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
  await waitFor(() =>
    expect(ticketsApi.edit).toHaveBeenCalledWith(
      "ticket",
      expect.objectContaining({
        expectedVersion: 1,
        description: "Original description",
        title: "My unsaved draft",
      }),
    ),
  );
  expect(screen.getByLabelText("What happened?")).toHaveValue(
    "Original description",
  );
});
