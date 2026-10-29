import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { TicketSearch, SearchSnippet } from "./ticket-search";
const push = jest.fn();
let mockRole = "Customer";
jest.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));
jest.mock("@/features/auth/auth-provider", () => ({
  useAuth: () => ({ user: { id: "customer", role: mockRole } }),
}));
function mount(params: Record<string, string> = {}) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  client.setQueryData(["session"], { id: "customer" });
  render(
    <QueryClientProvider client={client}>
      <TicketSearch params={params} />
    </QueryClientProvider>,
  );
  return client;
}
beforeEach(() => {
  mockRole = "Customer";
  push.mockReset();
  global.fetch = jest.fn(
    async (path) =>
      ({
        ok: true,
        status: 200,
        json: async () =>
          String(path).includes("/categories")
            ? []
            : {
                items: [],
                total: 0,
                page: 1,
                pageSize: 20,
                candidateLimit: 1000,
              },
      }) as Response,
  );
});
it("renders malicious snippet markup as text and highlights only the supplied segments", () => {
  const { container } = render(
    <SearchSnippet
      segments={[
        { text: "<img src=x onerror=alert(1)>", highlighted: true },
        { text: " public text", highlighted: false },
      ]}
    />,
  );
  expect(container.querySelector("img")).toBeNull();
  expect(container.querySelector("mark")).toHaveTextContent(
    "<img src=x onerror=alert(1)>",
  );
  expect(container).toHaveTextContent("public text");
});
it("submits filter-only searches with inclusive UTC dates and resets pagination", async () => {
  mount({ page: "3" });
  fireEvent.change(screen.getByLabelText("Status"), {
    target: { value: "inProgress" },
  });
  fireEvent.change(screen.getByLabelText("Priority"), {
    target: { value: "high" },
  });
  fireEvent.change(screen.getByLabelText("Created from (UTC)"), {
    target: { value: "2026-10-01" },
  });
  fireEvent.change(screen.getByLabelText("Created through (UTC)"), {
    target: { value: "2026-10-28" },
  });
  fireEvent.change(screen.getByLabelText("Sort"), {
    target: { value: "newest" },
  });
  fireEvent.click(
    screen.getByRole("button", { name: "Search public content" }),
  );
  await waitFor(() =>
    expect(push).toHaveBeenCalledWith(
      "/search?status=inProgress&priority=high&from=2026-10-01&to=2026-10-28&sort=newest&page=1",
    ),
  );
  await screen.findByText("No tickets match your search.");
});
it("rejects reversed dates without changing the active search", async () => {
  mount();
  fireEvent.change(screen.getByLabelText("Created from (UTC)"), {
    target: { value: "2026-10-28" },
  });
  fireEvent.change(screen.getByLabelText("Created through (UTC)"), {
    target: { value: "2026-10-01" },
  });
  fireEvent.click(
    screen.getByRole("button", { name: "Search public content" }),
  );
  expect(await screen.findByRole("alert")).toHaveTextContent("end date");
  expect(push).not.toHaveBeenCalled();
});
it("keeps search results scoped to the active account and all URL filters", async () => {
  const client = mount({
    q: "reply",
    categoryId: "category",
    agentId: "agent",
    from: "2026-10-01",
    to: "2026-10-28",
    sort: "newest",
  });
  await screen.findByText("No tickets match your search.");
  expect(jest.mocked(fetch).mock.calls.map(([path]) => path)).toContain(
    "/api/v1/search/tickets?q=reply&categoryId=category&agentId=agent&from=2026-10-01&to=2026-10-28&sort=newest&page=1&pageSize=20",
  );
  expect(
    client.getQueryCache().findAll({ queryKey: ["search", "other"] }),
  ).toHaveLength(0);
});
it("explains a search outage and allows retry while linking to ticket browsing", async () => {
  global.fetch = jest.fn(async (path) =>
    String(path).includes("/search/tickets")
      ? ({
          ok: false,
          status: 503,
          json: async () => ({ detail: "Search unavailable." }),
        } as Response)
      : ({ ok: true, status: 200, json: async () => [] } as Response),
  );
  mount();
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "Search is temporarily unavailable",
  );
  expect(screen.getByRole("link", { name: "Browse tickets" })).toHaveAttribute(
    "href",
    "/tickets",
  );
  expect(screen.getByRole("button", { name: "Retry search" })).toBeVisible();
});

it("restores URL category and agent selections after delayed options without losing a dirty draft", async () => {
  mockRole = "Agent";
  const categoryId = "aabed648-e725-4b50-93c1-2cc6394c1941";
  const agentId = "6f4457e5-616d-4937-85fd-bd73cc741014";
  let resolveCategories!: (response: Response) => void;
  let resolveAgents!: (response: Response) => void;
  global.fetch = jest.fn((path) => {
    if (String(path).endsWith("/categories"))
      return new Promise<Response>((resolve) => {
        resolveCategories = resolve;
      });
    if (String(path).endsWith("/agents"))
      return new Promise<Response>((resolve) => {
        resolveAgents = resolve;
      });
    return Promise.resolve({
      ok: true,
      status: 200,
      json: async () => ({
        items: [],
        total: 0,
        page: 1,
        pageSize: 20,
        candidateLimit: 1000,
      }),
    } as Response);
  });
  mount({ q: "original", categoryId, agentId });
  await waitFor(() => expect(resolveCategories).toBeDefined());
  await waitFor(() => expect(resolveAgents).toBeDefined());
  expect(
    screen.queryByRole("option", { name: "General" }),
  ).not.toBeInTheDocument();
  expect(
    screen.queryByRole("option", { name: "Jordan Lee" }),
  ).not.toBeInTheDocument();
  fireEvent.change(screen.getByLabelText("Keywords or ticket number"), {
    target: { value: "draft in progress" },
  });
  await act(async () =>
    resolveCategories({
      ok: true,
      status: 200,
      json: async () => [{ id: categoryId, name: "General" }],
    } as Response),
  );
  await screen.findByRole("option", { name: "General" });
  expect(screen.getByLabelText("Category")).toHaveValue(categoryId);
  await act(async () =>
    resolveAgents({
      ok: true,
      status: 200,
      json: async () => [{ id: agentId, name: "Jordan Lee" }],
    } as Response),
  );
  await screen.findByRole("option", { name: "Jordan Lee" });
  expect(screen.getByLabelText("Assigned agent")).toHaveValue(agentId);
  expect(screen.getByLabelText("Keywords or ticket number")).toHaveValue(
    "draft in progress",
  );
  fireEvent.click(
    screen.getByRole("button", { name: "Search public content" }),
  );
  await waitFor(() =>
    expect(push).toHaveBeenCalledWith(
      `/search?q=draft+in+progress&categoryId=${categoryId}&agentId=${agentId}&sort=relevance&page=1`,
    ),
  );
});
