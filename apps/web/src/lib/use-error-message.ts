"use client";

import { useTranslations } from "next-intl";
import { ApiError } from "./api";

type ErrorKey =
  | "requestFailed"
  | "badRequest"
  | "unauthorized"
  | "forbidden"
  | "notFound"
  | "conflict"
  | "tooLarge"
  | "unsupported"
  | "rateLimited"
  | "unavailable"
  | "serverError"
  | "accountChanged"
  | "invalidCredentials"
  | "ticketChanged"
  | "dashboardUnavailable";

function errorKey(error: unknown): ErrorKey {
  if (!(error instanceof ApiError)) return "requestFailed";
  if (error.status === 0) return "accountChanged";
  if (error.status === 401 && /invalid email or password/i.test(error.message))
    return "invalidCredentials";
  if (error.status === 409 && /this ticket changed/i.test(error.message))
    return "ticketChanged";
  if (error.status === 503 && /dashboard unavailable/i.test(error.message))
    return "dashboardUnavailable";
  if (error.status === 400) return "badRequest";
  if (error.status === 401) return "unauthorized";
  if (error.status === 403) return "forbidden";
  if (error.status === 404) return "notFound";
  if (error.status === 409 || error.status === 412) return "conflict";
  if (error.status === 413) return "tooLarge";
  if (error.status === 415) return "unsupported";
  if (error.status === 429) return "rateLimited";
  if (error.status === 503) return "unavailable";
  if (error.status >= 500) return "serverError";
  return "requestFailed";
}

export function useErrorMessage() {
  const t = useTranslations("Errors");
  return (error: unknown) => t(errorKey(error));
}
