import {
  getApiV1SearchTicketsQueryKey,
  type SearchSort,
  type SearchTextSegment,
  type TicketSearchPage,
} from "@ticketflow/api-client";
export type SearchPage = TicketSearchPage;
export type SearchSegment = SearchTextSegment;
export type { SearchSort };
export const searchQueryKey = (principalId?: string) =>
  getApiV1SearchTicketsQueryKey({
    headers: principalId ? { "X-TicketFlow-User": principalId } : undefined,
  });
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
export function searchRequest(query: URLSearchParams) {
  const request = new URLSearchParams(query);
  request.set("pageSize", "20");
  return {
    Q: request.get("q") ?? undefined,
    Page: request.get("page") ?? "1",
    PageSize: request.get("pageSize") ?? "20",
    Status: (request.get("status") || undefined) as
      | import("@ticketflow/api-client").TicketStatus
      | undefined,
    Priority: (request.get("priority") || undefined) as
      | import("@ticketflow/api-client").TicketPriority
      | undefined,
    CategoryId: request.get("categoryId") ?? undefined,
    AgentId: request.get("agentId") ?? undefined,
    From: request.get("from") ?? undefined,
    To: request.get("to") ?? undefined,
    Sort: (request.get("sort") || undefined) as SearchSort | undefined,
  };
}
