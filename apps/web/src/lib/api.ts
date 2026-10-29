import {
  client as generatedApiClient,
  type components,
} from "@ticketflow/api-client";
export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}
type Options = Omit<RequestInit, "headers"> & {
  headers?: Record<string, string>;
  isCurrent?: () => boolean;
  expectedPrincipalId?: string;
};
let refreshInFlight: Promise<{
  renewed: boolean;
  principalId?: string;
}> | null = null;

async function csrfToken(): Promise<string> {
  const response = await fetch("/api/v1/auth/csrf", {
    credentials: "same-origin",
    cache: "no-store",
  });
  if (!response.ok)
    throw new ApiError(
      response.status,
      "Could not verify the request. Please try again.",
    );
  const data = await response.json();
  if (typeof data.token !== "string")
    throw new ApiError(400, "Could not verify the request.");
  return data.token;
}

function assertCurrent(options: Options) {
  if (options.isCurrent && !options.isCurrent())
    throw new ApiError(
      0,
      "The account changed. Please try again from your workspace.",
    );
}

async function perform(path: string, options: Options = {}) {
  assertCurrent(options);
  const method = (options.method ?? "GET").toUpperCase();
  const headers = { ...options.headers } as Record<string, string>;
  if (options.expectedPrincipalId) {
    for (const name of Object.keys(headers)) {
      if (name.toLowerCase() === "x-ticketflow-user") delete headers[name];
    }
    headers["X-TicketFlow-User"] = options.expectedPrincipalId;
  }
  if (
    options.body &&
    !headers["Content-Type"] &&
    typeof options.body === "string"
  )
    headers["Content-Type"] = "application/json";
  if (!["GET", "HEAD", "OPTIONS"].includes(method))
    headers["X-CSRF-TOKEN"] = await csrfToken();
  assertCurrent(options);
  const {
    isCurrent: _isCurrent,
    expectedPrincipalId: _expectedPrincipalId,
    ...request
  } = options;
  void _isCurrent;
  void _expectedPrincipalId;
  return fetch("/api/v1" + path, {
    ...request,
    method,
    headers,
    credentials: "same-origin",
    cache: "no-store",
  });
}

/**
 * Hey API transport adapter. Generated SDK calls still pass through the same
 * CSRF, refresh, account-change, and no-store handling as handwritten calls.
 */
export async function apiClientFetch(
  input: RequestInfo | URL,
  init?: RequestInit,
): Promise<Response> {
  const request =
    input instanceof Request
      ? new Request(input, init)
      : new Request(new URL(input.toString(), window.location.origin), init);
  const url = new URL(request.url, window.location.origin);
  const apiPrefix = "/api/v1";
  if (!url.pathname.startsWith(apiPrefix))
    throw new ApiError(0, "The API request URL is not supported.");
  const path = url.pathname.slice(apiPrefix.length) + url.search;
  const response = await apiResponse(path, {
    method: request.method,
    headers: Object.fromEntries(request.headers.entries()),
    // Browser streaming uploads require duplex/HTTP support that the proxy does
    // not provide. Buffer once so JSON and multipart bytes also survive retries.
    body: request.body ? await request.clone().arrayBuffer() : undefined,
    signal: request.signal,
    expectedPrincipalId: request.headers.get("X-TicketFlow-User") ?? undefined,
  });
  if (typeof response.headers?.get !== "function") {
    const payload = await response.json().catch(() => undefined);
    const normalized = new Response(
      payload === undefined ? null : JSON.stringify(payload),
      {
        status: response.status || 200,
        headers: { "Content-Type": "application/json" },
      },
    );
    return normalized;
  }
  return response;
}

generatedApiClient.setConfig({
  baseUrl: typeof window === "undefined" ? "" : window.location.origin,
  fetch: apiClientFetch,
});

async function renewedIdentity(response: Response) {
  const user: components["schemas"]["UserView"] = await response.json();
  return {
    renewed: true,
    principalId: typeof user.id === "string" ? user.id : undefined,
  };
}

async function refreshSession() {
  if (!refreshInFlight) {
    refreshInFlight = (async () => {
      const renew = async () => {
        // Another tab may have renewed the shared cookies while this one waited.
        const current = await perform("/auth/me");
        if (current.ok) return renewedIdentity(current);
        if (current.status !== 401)
          throw new ApiError(current.status, "Unable to verify the session.");
        const response = await perform("/auth/refresh", {
          method: "POST",
          body: "{}",
        });
        if (response.ok) return renewedIdentity(response);
        if (response.status === 401) return { renewed: false };
        throw new ApiError(
          response.status,
          "Unable to renew the session. Please try again.",
        );
      };
      return typeof navigator !== "undefined" && navigator.locks
        ? navigator.locks.request("ticketflow-session-refresh", renew)
        : renew();
    })().finally(() => {
      refreshInFlight = null;
    });
  }
  return refreshInFlight;
}

async function apiResponse(
  path: string,
  options: Options = {},
): Promise<Response> {
  let response = await perform(path, options);
  if (
    response.status === 401 &&
    (!path.startsWith("/auth/") || path === "/auth/me")
  ) {
    const session = await refreshSession();
    assertCurrent(options);
    if (
      session.renewed &&
      options.expectedPrincipalId &&
      session.principalId !== options.expectedPrincipalId
    )
      throw new ApiError(
        0,
        "The account changed. Please reload your workspace before trying again.",
      );
    if (session.renewed) response = await perform(path, options);
  }
  assertCurrent(options);
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    if (
      (response.status === 412 ||
        (response.status === 401 && !path.startsWith("/auth/"))) &&
      typeof window !== "undefined"
    )
      window.dispatchEvent(
        new CustomEvent("ticketflow:session-expired", {
          detail: {
            code: response.status === 412 ? "account_changed" : undefined,
          },
        }),
      );
    throw new ApiError(
      response.status,
      problem.detail ??
        problem.title ??
        "The request failed. Please try again.",
    );
  }
  return response;
}

export async function api<T>(path: string, options: Options = {}): Promise<T> {
  const response = await apiResponse(path, options);
  if (response.status === 204) return undefined as T;
  const value = (await response.json()) as T;
  assertCurrent(options);
  return value;
}

export async function apiDownload(path: string, options: Options = {}) {
  const response = await apiResponse(path, options);
  const blob = await response.blob();
  assertCurrent(options);
  const disposition = response.headers.get("Content-Disposition") ?? "";
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1];
  const plain = /filename="([^"\r\n]+)"/i.exec(disposition)?.[1];
  let fileName = plain ?? "attachment";
  if (encoded) {
    try {
      fileName = decodeURIComponent(encoded);
    } catch {
      /* Use the safe fallback. */
    }
  }
  return { blob, fileName };
}
