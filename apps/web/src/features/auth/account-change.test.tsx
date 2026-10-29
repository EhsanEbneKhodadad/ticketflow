import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider, useAuth } from "./auth-provider";
import { AuthScreen } from "./auth-screen";
import { api } from "@/lib/api";
const mockReplace = jest.fn();
jest.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mockReplace, push: jest.fn() }),
}));

it("clears stale private queries and drafts on account change without logging out the shared session", async () => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  client.setQueryData(["session"], {
    id: "staff-A",
    name: "Staff A",
    role: "Agent",
  });
  client.setQueryData(["attachments", "staff-A"], [{ id: "private-file" }]);
  global.fetch = jest
    .fn()
    .mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({ token: "B-csrf" }),
    })
    .mockResolvedValueOnce({
      ok: false,
      status: 412,
      json: async () => ({
        code: "account_changed",
        detail:
          "Your account changed in another tab. Sign in to this workspace again.",
      }),
    });
  function Workspace() {
    const { user } = useAuth();
    if (!user) return <AuthScreen mode="login" />;
    return (
      <>
        <input aria-label="Private draft" defaultValue="A's private draft" />
        <button
          onClick={() =>
            void api("/tickets/id/attachments", {
              method: "POST",
              body: new FormData(),
              expectedPrincipalId: user.id,
            }).catch(() => {})
          }
        >
          Upload
        </button>
      </>
    );
  }
  render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <Workspace />
      </AuthProvider>
    </QueryClientProvider>,
  );
  fireEvent.click(screen.getByRole("button", { name: "Upload" }));
  await waitFor(() =>
    expect(screen.queryByLabelText("Private draft")).not.toBeInTheDocument(),
  );
  expect(await screen.findByRole("alert")).toHaveTextContent("account changed");
  expect(client.getQueryData(["attachments", "staff-A"])).toBeUndefined();
  expect(client.getQueryData(["session"])).toBeNull();
  expect(mockReplace).toHaveBeenCalledWith("/login");
  expect(jest.mocked(fetch).mock.calls.map(([path]) => path)).toEqual([
    "/api/v1/auth/csrf",
    "/api/v1/tickets/id/attachments",
  ]);
});
