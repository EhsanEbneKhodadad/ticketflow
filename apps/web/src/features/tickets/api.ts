import {
  getApiV1Agents,
  getApiV1Categories,
  getApiV1Tickets,
  getApiV1TicketsQueryKey,
  getApiV1TicketsById,
  getApiV1TicketsByIdQueryKey,
  postApiV1TicketsMutation,
  postApiV1TicketsByIdMessagesMutation,
  patchApiV1TicketsByIdStatusMutation,
  patchApiV1TicketsByIdPriorityMutation,
  patchApiV1TicketsByIdAssignmentMutation,
  putApiV1TicketsByIdMutation,
  type AssignmentRequest,
  type CreateTicketRequest,
  type EditTicketRequest,
  type MessageRequest,
  type PriorityRequest,
  type StatusRequest,
  type TicketPriority,
  type TicketStatus,
} from "@ticketflow/api-client";
export type Ticket = import("@ticketflow/api-client").TicketView;
export type Status = TicketStatus;
export type Priority = TicketPriority;
const headers = (principalId?: string) =>
  principalId ? { "X-TicketFlow-User": principalId } : undefined;
export const ticketKeys = {
  list: (principalId?: string) =>
    getApiV1TicketsQueryKey({ headers: headers(principalId) }),
  detail: (id: string, principalId?: string) =>
    getApiV1TicketsByIdQueryKey({
      path: { id },
      headers: headers(principalId),
    }),
};
export const ticketsApi = {
  list: async (
    query: NonNullable<
      import("@ticketflow/api-client").GetApiV1TicketsData["query"]
    >,
    principalId?: string,
  ) =>
    (
      await getApiV1Tickets({
        query,
        headers: headers(principalId),
        throwOnError: true,
      })
    ).data,
  get: async (id: string, principalId?: string) =>
    (
      await getApiV1TicketsById({
        path: { id },
        headers: headers(principalId),
        throwOnError: true,
      })
    ).data,
  categories: async (principalId?: string) =>
    (
      await getApiV1Categories({
        headers: headers(principalId),
        throwOnError: true,
      })
    ).data,
  agents: async (principalId?: string) =>
    (
      await getApiV1Agents({
        headers: headers(principalId),
        throwOnError: true,
      })
    ).data,
  create: async (input: CreateTicketRequest, principalId?: string) =>
    postApiV1TicketsMutation({ headers: headers(principalId) }).mutationFn!(
      { body: input, throwOnError: true },
      undefined as never,
    ),
  edit: async (id: string, input: EditTicketRequest, principalId?: string) =>
    putApiV1TicketsByIdMutation({ headers: headers(principalId) }).mutationFn!(
      { path: { id }, body: input, throwOnError: true },
      undefined as never,
    ),
  reply: async (id: string, input: MessageRequest, principalId?: string) =>
    postApiV1TicketsByIdMessagesMutation({
      headers: headers(principalId),
    }).mutationFn!(
      { path: { id }, body: input, throwOnError: true },
      undefined as never,
    ),
  status: async (id: string, input: StatusRequest, principalId?: string) =>
    patchApiV1TicketsByIdStatusMutation({
      headers: headers(principalId),
    }).mutationFn!(
      { path: { id }, body: input, throwOnError: true },
      undefined as never,
    ),
  priority: async (id: string, input: PriorityRequest, principalId?: string) =>
    patchApiV1TicketsByIdPriorityMutation({
      headers: headers(principalId),
    }).mutationFn!(
      { path: { id }, body: input, throwOnError: true },
      undefined as never,
    ),
  assign: async (id: string, input: AssignmentRequest, principalId?: string) =>
    patchApiV1TicketsByIdAssignmentMutation({
      headers: headers(principalId),
    }).mutationFn!(
      { path: { id }, body: input, throwOnError: true },
      undefined as never,
    ),
};
export const ticketStatuses: Status[] = [
  "open",
  "inProgress",
  "waitingForCustomer",
  "resolved",
  "closed",
];
export const ticketPriorities: Priority[] = ["low", "medium", "high", "urgent"];
export function initials(name: string) {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((n) => n[0])
    .join("")
    .toUpperCase();
}
export function dateLabel(value: string, locale = "en") {
  return new Intl.DateTimeFormat(locale, {
    month: "short",
    day: "numeric",
  }).format(new Date(value));
}
export function timeLabel(value: string, locale = "en") {
  return new Intl.DateTimeFormat(locale, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(value));
}
