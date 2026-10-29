import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { getApiV1TicketsByIdAttachmentsQueryKey } from "@ticketflow/api-client";
import type { components } from "@ticketflow/api-client";
import { sessionQueryKey } from "@/features/auth/auth-provider";
import { ticketKeys } from "@/features/tickets/api";
import { AttachmentPanel } from "./attachment-panel";
type Attachment = components["schemas"]["AttachmentView"];
const publicFile: Attachment = {
  id: "file",
  ticketId: "ticket",
  messageId: null,
  fileName: "public.txt",
  contentType: "text/plain",
  size: 5,
  internal: false,
  uploadedBy: "customer",
  createdAt: "2026-10-28T00:00:00Z",
  ticketVersion: 2,
};
function mount(staff = false, files: Attachment[] = [], status = "open") {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  client.setQueryData(sessionQueryKey(), { id: "customer" });
  client.setQueryData(
    getApiV1TicketsByIdAttachmentsQueryKey({
      path: { id: "ticket" },
      headers: { "X-TicketFlow-User": "customer" },
    }),
    files,
  );
  render(
    <QueryClientProvider client={client}>
      <AttachmentPanel
        ticketId="ticket"
        version={2}
        closed={status === "closed"}
        principalId="customer"
        staff={staff}
      />
    </QueryClientProvider>,
  );
  return client;
}
beforeEach(() => {
  global.fetch = jest.fn();
});
it("hides internal metadata from customers even if supplied in the list", () => {
  mount(false, [
    publicFile,
    {
      ...publicFile,
      id: "secret",
      internal: true,
      fileName: "investigation.txt",
    },
  ]);
  expect(
    screen.getByRole("button", { name: /Download public.txt/ }),
  ).toBeVisible();
  expect(screen.queryByText("investigation.txt")).not.toBeInTheDocument();
  expect(screen.queryByText(/remaining/)).not.toBeInTheDocument();
});
it("rejects unsupported, empty and oversized selections before sending", () => {
  mount();
  for (const file of [
    new File(["bad"], "unsafe.html", { type: "text/html" }),
    new File([], "empty.txt"),
    new File([new Uint8Array(10485761)], "large.txt"),
  ]) {
    fireEvent.change(screen.getByLabelText("Choose ticket file"), {
      target: { files: [file] },
    });
    fireEvent.click(screen.getByRole("button", { name: "Upload file" }));
    expect(screen.getByRole("alert")).toBeVisible();
  }
  expect(fetch).not.toHaveBeenCalled();
});
it("preserves the selected file on a conflict and refreshes both scoped queries", async () => {
  const client = mount();
  const invalidate = jest.spyOn(client, "invalidateQueries");
  global.fetch = jest
    .fn()
    .mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({ token: "csrf" }),
    })
    .mockResolvedValueOnce({
      ok: false,
      status: 409,
      json: async () => ({ detail: "This ticket changed." }),
    })
    .mockResolvedValue({ ok: true, status: 200, json: async () => [] });
  const file = new File(["hello"], "draft.txt", { type: "text/plain" });
  fireEvent.change(screen.getByLabelText("Choose ticket file"), {
    target: { files: [file] },
  });
  fireEvent.click(screen.getByRole("button", { name: "Upload file" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "selected file is preserved",
  );
  expect(screen.getByText(/draft.txt/)).toBeVisible();
  expect(invalidate).toHaveBeenCalledWith({
    queryKey: ticketKeys.detail("ticket", "customer"),
  });
  expect(invalidate).toHaveBeenCalledWith({
    queryKey: getApiV1TicketsByIdAttachmentsQueryKey({
      path: { id: "ticket" },
      headers: { "X-TicketFlow-User": "customer" },
    }),
  });
  const upload = jest
    .mocked(fetch)
    .mock.calls.find(([path]) => path === "/api/v1/tickets/ticket/attachments");
  expect(upload?.[1]?.body).toBeDefined();
  expect(new Headers(upload?.[1]?.headers).get("Content-Type")).toMatch(
    /^multipart\/form-data; boundary=/,
  );
});
it("allows closed-ticket downloads while disabling uploads", () => {
  mount(false, [publicFile], "closed");
  expect(
    screen.getByRole("button", { name: /Download public.txt/ }),
  ).toBeEnabled();
  expect(screen.getByLabelText("Choose ticket file")).toBeDisabled();
  expect(screen.getByRole("button", { name: "Upload file" })).toBeDisabled();
});
it("does not save a late download after switching accounts", async () => {
  const client = mount(false, [publicFile]);
  let finish!: (value: Response) => void;
  global.fetch = jest.fn(
    () =>
      new Promise((resolve) => {
        finish = resolve;
      }),
  );
  const create = jest.fn();
  URL.createObjectURL = create;
  fireEvent.click(screen.getByRole("button", { name: /Download public.txt/ }));
  await waitFor(() => expect(fetch).toHaveBeenCalled());
  client.setQueryData(sessionQueryKey(), { id: "other" });
  await act(async () =>
    finish({
      ok: true,
      status: 200,
      headers: new Headers(),
      blob: async () => new Blob(["private"]),
    } as Response),
  );
  expect(create).not.toHaveBeenCalled();
  expect(screen.queryByRole("alert")).not.toBeInTheDocument();
});
