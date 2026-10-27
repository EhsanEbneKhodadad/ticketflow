import { api } from "./api";
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
