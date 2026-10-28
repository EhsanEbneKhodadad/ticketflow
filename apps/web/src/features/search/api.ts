import type { components } from "@ticketflow/api-client";
import { api } from "@/lib/api";
export type SearchPage = components["schemas"]["TicketSearchPage"];
export type SearchSegment = components["schemas"]["SearchTextSegment"];
export type SearchSort = components["schemas"]["SearchSort"];
export const searchFields = [
  "q",
  "status",
  "priority",
  "categoryId",
  "agentId",
  "from",
  "to",
  "sort",
] as const;
export function searchParameters(input: Record<string, string>) {
  const query = new URLSearchParams();
  for (const field of searchFields)
    if (input[field]?.trim()) query.set(field, input[field].trim());
  const page = Number(input.page ?? 1);
  query.set(
    "page",
    String(Number.isInteger(page) && page >= 1 && page <= 50 ? page : 1),
  );
  return query;
}
export function searchTickets(
  query: URLSearchParams,
  signal: AbortSignal,
  principalId: string,
  isCurrent: () => boolean,
) {
  const request = new URLSearchParams(query);
  request.set("pageSize", "20");
  return api<SearchPage>("/search/tickets?" + request, {
    signal,
    isCurrent,
    expectedPrincipalId: principalId,
  });
}
