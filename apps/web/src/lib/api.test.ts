import { api, apiDownload } from "./api";
it("rechecks shared cookies after obtaining the cross-tab refresh lock", async () => {
  const request = jest.fn(async (_name, callback) => callback());
  Object.defineProperty(navigator, "locks", {
    configurable: true,
    value: { request },
  });
  const response = (status: number, data: unknown) =>
    ({ ok: status === 200, status, json: async () => data }) as Response;
  global.fetch = jest
    .fn()
    .mockResolvedValueOnce(response(401, {}))
    .mockResolvedValueOnce(response(200, { id: "renewed-by-another-tab" }))
    .mockResolvedValueOnce(response(200, { items: [] }));
  await expect(api("/tickets")).resolves.toEqual({ items: [] });
  expect(request).toHaveBeenCalledWith(
    "ticketflow-session-refresh",
    expect.any(Function),
  );
  expect(jest.mocked(fetch).mock.calls.map((call) => call[0])).toEqual([
    "/api/v1/tickets",
    "/api/v1/auth/me",
    "/api/v1/tickets",
  ]);
});

it("sends multipart with CSRF and no JSON content type through session renewal", async () => {
  const form = new FormData();
  form.set("file", new File(["hello"], "hello.txt", { type: "text/plain" }));
  const response = (status: number, data: unknown) =>
    ({ ok: status === 200, status, json: async () => data }) as Response;
  global.fetch = jest
    .fn()
    .mockResolvedValueOnce(response(200, { token: "first" }))
    .mockResolvedValueOnce(response(401, {}))
    .mockResolvedValueOnce(response(200, { id: "same-user" }))
    .mockResolvedValueOnce(response(200, { token: "renewed" }))
    .mockResolvedValueOnce(response(200, { id: "file" }));
  await expect(
    api("/tickets/id/attachments", {
      method: "POST",
      body: form,
      expectedPrincipalId: "same-user",
    }),
  ).resolves.toEqual({ id: "file" });
  const calls = jest
    .mocked(fetch)
    .mock.calls.filter(([path]) => path === "/api/v1/tickets/id/attachments");
  expect(calls).toHaveLength(2);
  expect(calls[1][1]).toMatchObject({
    body: form,
    credentials: "same-origin",
    headers: { "X-CSRF-TOKEN": "renewed" },
  });
  expect(calls[1][1]?.headers).not.toHaveProperty("Content-Type");
});

it("downloads original bytes with server metadata after session renewal", async () => {
  const blob = new Blob(["original"], { type: "text/plain" });
  global.fetch = jest
    .fn()
    .mockResolvedValueOnce({ ok: false, status: 401 })
    .mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({ id: "same-user" }),
    })
    .mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({
        "Content-Disposition":
          "attachment; filename*=UTF-8''report%20final.txt",
      }),
      blob: async () => blob,
    });
  await expect(
    apiDownload("/tickets/id/attachments/file/download"),
  ).resolves.toEqual({ blob, fileName: "report final.txt" });
});

it("discards a download when the initiating account changes while reading bytes", async () => {
  let active = true;
  global.fetch = jest.fn().mockResolvedValue({
    ok: true,
    status: 200,
    headers: new Headers(),
    blob: async () => {
      active = false;
      return new Blob(["private"]);
    },
  });
  await expect(
    apiDownload("/tickets/id/attachments/file/download", {
      isCurrent: () => active,
    }),
  ).rejects.toThrow("account changed");
});

it("does not replay an upload after an account change during renewal", async () => {
  let active = true;
  global.fetch = jest
    .fn()
    .mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({ token: "csrf" }),
    })
    .mockResolvedValueOnce({ ok: false, status: 401 })
    .mockImplementationOnce(async () => {
      active = false;
      return { ok: true, status: 200, json: async () => ({ id: "same-user" }) };
    });
  await expect(
    api("/tickets/id/attachments", {
      method: "POST",
      body: new FormData(),
      isCurrent: () => active,
    }),
  ).rejects.toThrow("account changed");
  expect(
    jest
      .mocked(fetch)
      .mock.calls.filter(([path]) => path === "/api/v1/tickets/id/attachments"),
  ).toHaveLength(1);
});

it.each(["me", "refresh"])(
  "does not replay an upload when %s identifies another account despite a stale local cache",
  async (stage) => {
    const jsonResponse = (status: number, data: unknown) =>
      ({ ok: status === 200, status, json: async () => data }) as Response;
    const responses = [
      jsonResponse(200, { token: "csrf" }),
      jsonResponse(401, {}),
    ];
    if (stage === "me") responses.push(jsonResponse(200, { id: "customer-B" }));
    else
      responses.push(
        jsonResponse(401, {}),
        jsonResponse(200, { token: "refresh-csrf" }),
        jsonResponse(200, { id: "customer-B" }),
      );
    global.fetch = jest
      .fn()
      .mockResolvedValue(
        jsonResponse(200, { token: "csrf", id: "wrongly-replayed" }),
      );
    for (const response of responses)
      jest.mocked(fetch).mockResolvedValueOnce(response);
    await expect(
      api("/tickets/id/attachments", {
        method: "POST",
        body: new FormData(),
        expectedPrincipalId: "staff-A",
        isCurrent: () => true,
      }),
    ).rejects.toThrow("account changed");
    expect(
      jest
        .mocked(fetch)
        .mock.calls.filter(
          ([path]) => path === "/api/v1/tickets/id/attachments",
        ),
    ).toHaveLength(1);
  },
);

it.each(["/search/tickets?q=account", "/categories"])(
  "binds the initial scoped GET %s to its initiating account",
  async (path) => {
    global.fetch = jest
      .fn()
      .mockResolvedValue({ ok: true, status: 200, json: async () => [] });
    await api(path, { expectedPrincipalId: "staff-A" });
    expect(fetch).toHaveBeenCalledWith(
      "/api/v1" + path,
      expect.objectContaining({
        headers: { "X-TicketFlow-User": "staff-A" },
      }),
    );
  },
);

it("rejects an initial wrong-account upload without renewal or replay and expires the local view", async () => {
  const expired = jest.fn();
  window.addEventListener("ticketflow:session-expired", expired);
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
  try {
    await expect(
      api("/tickets/id/attachments", {
        method: "POST",
        body: new FormData(),
        expectedPrincipalId: "staff-A",
        isCurrent: () => true,
      }),
    ).rejects.toMatchObject({
      status: 412,
      message: expect.stringContaining("account changed"),
    });
    expect(jest.mocked(fetch).mock.calls.map(([path]) => path)).toEqual([
      "/api/v1/auth/csrf",
      "/api/v1/tickets/id/attachments",
    ]);
    expect(jest.mocked(fetch).mock.calls[1][1]?.headers).toEqual({
      "X-CSRF-TOKEN": "B-csrf",
      "X-TicketFlow-User": "staff-A",
    });
    expect(expired).toHaveBeenCalledTimes(1);
  } finally {
    window.removeEventListener("ticketflow:session-expired", expired);
  }
});
