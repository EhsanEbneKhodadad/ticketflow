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
};
let refreshInFlight: Promise<boolean> | null = null;

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

async function perform(path: string, options: Options = {}) {
  const method = (options.method ?? "GET").toUpperCase();
  const headers = { ...options.headers } as Record<string, string>;
  if (options.body) headers["Content-Type"] = "application/json";
  if (!["GET", "HEAD", "OPTIONS"].includes(method))
    headers["X-CSRF-TOKEN"] = await csrfToken();
  return fetch("/api/v1" + path, {
    ...options,
    method,
    headers,
    credentials: "same-origin",
    cache: "no-store",
  });
}

async function refreshSession() {
  if (!refreshInFlight) {
    refreshInFlight = (async () => {
      const renew = async () => {
        // Another tab may have renewed the shared cookies while this one waited.
        const current = await perform("/auth/me");
        if (current.ok) return true;
        if (current.status !== 401)
          throw new ApiError(current.status, "Unable to verify the session.");
        const response = await perform("/auth/refresh", {
          method: "POST",
          body: "{}",
        });
        if (response.ok) return true;
        if (response.status === 401) return false;
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

export async function api<T>(path: string, options: Options = {}): Promise<T> {
  let response = await perform(path, options);
  if (
    response.status === 401 &&
    (!path.startsWith("/auth/") || path === "/auth/me")
  ) {
    if (await refreshSession()) response = await perform(path, options);
  }
  if (!response.ok) {
    if (
      response.status === 401 &&
      !path.startsWith("/auth/") &&
      typeof window !== "undefined"
    )
      window.dispatchEvent(new Event("ticketflow:session-expired"));
    const problem = await response.json().catch(() => ({}));
    throw new ApiError(
      response.status,
      problem.detail ??
        problem.title ??
        "The request failed. Please try again.",
    );
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export function errorMessage(error: unknown) {
  return error instanceof Error
    ? error.message
    : "Something went wrong. Please try again.";
}
