import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider, sessionQueryKey, useAuth } from "./auth-provider";

function SessionProbe() {
  const { user, loading } = useAuth();
  if (loading) return <p>Loading session</p>;
  return <p>{user?.name ?? "Signed out"}</p>;
}

it("keeps the generated session query while clearing stale private data", async () => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  client.setQueryData(["private", "previous-user"], { secret: true });
  global.fetch = jest.fn().mockResolvedValue(
    new Response(
      JSON.stringify({
        id: "current-user",
        name: "Current User",
        email: "current@example.com",
        role: "Customer",
      }),
      { status: 200, headers: { "Content-Type": "application/json" } },
    ),
  );

  render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <SessionProbe />
      </AuthProvider>
    </QueryClientProvider>,
  );

  expect(await screen.findByText("Current User")).toBeInTheDocument();
  expect(client.getQueryData(["private", "previous-user"])).toBeUndefined();
  expect(client.getQueryData(sessionQueryKey())).toMatchObject({
    id: "current-user",
  });
});
