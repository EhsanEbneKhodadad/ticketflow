import type { components } from "@ticketflow/api-client";
import { api } from "@/lib/api";
type Schemas = components["schemas"];
export type Ticket = Schemas["TicketView"];
export type Status = Schemas["TicketStatus"];
export type Priority = Schemas["TicketPriority"];
export const ticketsApi = {
  list: (query: Record<string, string>) =>
    api<Schemas["TicketPage"]>("/tickets?" + new URLSearchParams(query)),
  get: (id: string) => api<Ticket>("/tickets/" + id),
  categories: () => api<Schemas["CategoryView"][]>("/categories"),
  agents: () => api<Schemas["AgentView"][]>("/agents"),
  create: (input: Schemas["CreateTicketRequest"]) =>
    api<Ticket>("/tickets", { method: "POST", body: JSON.stringify(input) }),
  edit: (id: string, input: Schemas["EditTicketRequest"]) =>
    api<Ticket>("/tickets/" + id, {
      method: "PUT",
      body: JSON.stringify(input),
    }),
  reply: (id: string, input: Schemas["MessageRequest"]) =>
    api<Ticket>("/tickets/" + id + "/messages", {
      method: "POST",
      body: JSON.stringify(input),
    }),
  status: (id: string, input: Schemas["StatusRequest"]) =>
    api<Ticket>("/tickets/" + id + "/status", {
      method: "PATCH",
      body: JSON.stringify(input),
    }),
  priority: (id: string, input: Schemas["PriorityRequest"]) =>
    api<Ticket>("/tickets/" + id + "/priority", {
      method: "PATCH",
      body: JSON.stringify(input),
    }),
  assign: (id: string, input: Schemas["AssignmentRequest"]) =>
    api<Ticket>("/tickets/" + id + "/assignment", {
      method: "PATCH",
      body: JSON.stringify(input),
    }),
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
