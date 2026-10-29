import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Dashboard } from "./dashboard";
jest.mock("@/features/auth/auth-provider", () => ({
  useAuth: () => ({ user: { id: "u1", role: "Customer" } }),
}));
function mount() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  client.setQueryData(["session"], { id: "u1" });
  render(
    <QueryClientProvider client={client}>
      <Dashboard />
    </QueryClientProvider>,
  );
}
it("shows real scoped counts returned by the API", async () => {
  global.fetch = jest.fn(
    async () =>
      ({
        ok: true,
        status: 200,
        json: async () => ({
          total: 3,
          open: 2,
          inProgress: 1,
          waitingForCustomer: 0,
          resolved: 0,
          closed: 0,
          unassigned: 2,
          highPriority: 0,
        }),
      }) as Response,
  );
  mount();
  expect(await screen.findByText("3")).toBeInTheDocument();
  expect(screen.getByText("Your tickets")).toBeInTheDocument();
  expect(global.fetch).toHaveBeenCalledWith(
    "/api/v1/dashboard",
    expect.objectContaining({
      headers: expect.objectContaining({ "X-TicketFlow-User": "u1" }),
    }),
  );
});
it("shows a recoverable failure instead of invented counts", async () => {
  global.fetch = jest.fn(
    async () =>
      ({
        ok: false,
        status: 503,
        json: async () => ({ detail: "Dashboard unavailable" }),
      }) as Response,
  );
  mount();
  expect(await screen.findByText("Dashboard unavailable")).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Try again" })).toBeInTheDocument();
});
