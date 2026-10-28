import { fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Admin } from "./admin";
let mockRole = "Customer";
jest.mock("@/features/auth/auth-provider", () => ({
  useAuth: () => ({ user: { id: "u1", role: mockRole } }),
}));
it("does not fetch admin data for non-admins", () => {
  global.fetch = jest.fn();
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  render(
    <QueryClientProvider client={client}>
      <Admin />
    </QueryClientProvider>,
  );
  expect(
    screen.getByText("Administrator access required."),
  ).toBeInTheDocument();
  expect(global.fetch).not.toHaveBeenCalled();
});

it("allows an administrator to create a category and refresh the list", async () => {
  mockRole = "Admin";
  global.fetch = jest.fn(async (path, options) => {
    const url = String(path);
    const json = url.endsWith("/auth/csrf")
      ? { token: "csrf" }
      : url.endsWith("/admin/users")
        ? []
        : options?.method === "POST"
          ? { id: "new-id", name: "Technical", slug: "technical" }
          : [];
    return { ok: true, status: 200, json: async () => json } as Response;
  });
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  client.setQueryData(["session"], { id: "u1" });
  render(
    <QueryClientProvider client={client}>
      <Admin />
    </QueryClientProvider>,
  );
  fireEvent.change(screen.getByLabelText("New category name"), {
    target: { value: "Technical" },
  });
  fireEvent.change(screen.getByLabelText("New category slug"), {
    target: { value: "technical" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Create category" }));
  expect(await screen.findByText("Category created.")).toBeInTheDocument();
  expect(global.fetch).toHaveBeenCalledWith(
    "/api/v1/admin/categories",
    expect.objectContaining({
      method: "POST",
      headers: expect.objectContaining({
        "X-CSRF-TOKEN": "csrf",
        "X-TicketFlow-User": "u1",
      }),
    }),
  );
});
