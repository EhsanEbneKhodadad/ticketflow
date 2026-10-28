import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { components } from "@ticketflow/api-client";
import { ticketsApi } from "./api";
import { Conversation } from "./conversation";

const ticket: components["schemas"]["TicketView"] = {
  id: "6f4457e5-616d-4937-85fd-bd73cc741013",
  number: "TKT-1001",
  title: "Account issue",
  description: "I cannot access my account.",
  requesterId: "6f4457e5-616d-4937-85fd-bd73cc741014",
  requesterName: "Alex",
  categoryId: "aabed648-e725-4b50-93c1-2cc6394c1941",
  category: "General",
  tags: ["login"],
  status: "open",
  priority: "medium",
  agentId: null,
  agentName: null,
  version: 1,
  createdAt: "2026-10-27T00:00:00Z",
  updatedAt: "2026-10-27T00:00:00Z",
  messages: [],
  audit: [],
};

function mount(staff: boolean) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <Conversation ticket={ticket} staff={staff} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  global.fetch = jest.fn((input) => {
    const csrf = String(input).endsWith("/auth/csrf");
    return Promise.resolve({
      ok: csrf,
      status: csrf ? 200 : 409,
      json: async () =>
        csrf
          ? { token: "token" }
          : { detail: "This ticket changed. Refresh before trying again." },
    } as Response);
  }) as jest.Mock;
});

it("keeps internal-note controls out of the customer conversation", () => {
  mount(false);
  expect(screen.getByLabelText("Message")).toBeInTheDocument();
  expect(
    screen.queryByRole("button", { name: "Internal note" }),
  ).not.toBeInTheDocument();
});

it("preserves a reply draft when the server reports a conflicting update", async () => {
  mount(true);
  fireEvent.change(screen.getByLabelText("Message"), {
    target: { value: "Please retain this draft." },
  });
  fireEvent.click(screen.getByRole("button", { name: "Send reply" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "This ticket changed.",
  );
  expect(screen.getByLabelText("Message")).toHaveValue(
    "Please retain this draft.",
  );
});

it("does not render internal messages even if supplied to a customer view", () => {
  const client = new QueryClient();
  render(
    <QueryClientProvider client={client}>
      <Conversation
        staff={false}
        ticket={{
          ...ticket,
          messages: [
            {
              id: "internal",
              authorId: "agent",
              body: "Private investigation",
              internal: true,
              authorName: "Agent",
              authorRole: "Agent",
              createdAt: ticket.createdAt,
            },
          ],
        }}
      />
    </QueryClientProvider>,
  );
  expect(screen.queryByText("Private investigation")).not.toBeInTheDocument();
});

it("discards a staff mutation response after the active account changes", async () => {
  const client = new QueryClient({
    defaultOptions: { mutations: { retry: false } },
  });
  client.setQueryData(["session"], { id: "staff" });
  let finish!: (value: typeof ticket) => void;
  const reply = jest.spyOn(ticketsApi, "reply").mockImplementation(
    () =>
      new Promise((resolve) => {
        finish = resolve;
      }),
  );
  render(
    <QueryClientProvider client={client}>
      <Conversation ticket={ticket} staff />
    </QueryClientProvider>,
  );
  fireEvent.change(screen.getByLabelText("Message"), {
    target: { value: "Pending staff response" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Send reply" }));
  await waitFor(() => expect(reply).toHaveBeenCalled());
  client.setQueryData(["session"], { id: "customer" });
  await act(async () => {
    finish(ticket);
  });
  expect(
    client.getQueryData(["ticket", ticket.id, "customer"]),
  ).toBeUndefined();
  expect(client.getQueryData(["ticket", ticket.id, "staff"])).toBeUndefined();
  expect(screen.queryByText("Reply sent.")).not.toBeInTheDocument();
  reply.mockRestore();
});
