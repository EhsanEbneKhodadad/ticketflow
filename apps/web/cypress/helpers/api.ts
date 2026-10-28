import type { components } from "@ticketflow/api-client";
export type Schemas = components["schemas"];
export const general = "aabed648-e725-4b50-93c1-2cc6394c1941";
export const demoPassword = () => String(Cypress.expose("demoPassword"));
export function write<T = unknown>(
  method: "POST" | "PATCH" | "PUT",
  path: string,
  body: object,
) {
  return cy
    .request<Schemas["CsrfView"]>({ url: "/api/v1/auth/csrf", log: false })
    .then(({ body: csrf }) =>
      cy.request<T>({
        method,
        url: "/api/v1" + path,
        body,
        headers: { "X-CSRF-TOKEN": csrf.token },
        log: false,
      }),
    );
}
export function signIn(email: string) {
  cy.clearAllCookies();
  return write<Schemas["UserView"]>("POST", "/auth/login", {
    email,
    password: demoPassword(),
  });
}
export function registerCustomer(prefix: string) {
  const email = prefix + "." + Date.now() + "@example.test";
  cy.clearAllCookies();
  return write<Schemas["UserView"]>("POST", "/auth/register", {
    email,
    password: demoPassword(),
    name: "Morgan Chen",
  }).then(() => email);
}
export function createTicket(title: string) {
  return write<Schemas["TicketView"]>("POST", "/tickets", {
    title,
    description:
      "Please help us restore access to the billing workspace. The request has all the details needed for investigation.",
    categoryId: general,
    tags: ["billing"],
    priority: "high",
  }).then(({ body }) => body);
}
export function getTicket(id: string) {
  return cy
    .request<
      Schemas["TicketView"]
    >({ url: "/api/v1/tickets/" + id, log: false })
    .then(({ body }) => body);
}
export function reply(id: string, body: string, internal = false) {
  return getTicket(id)
    .then((ticket) =>
      write<Schemas["TicketView"]>("POST", `/tickets/${id}/messages`, {
        body,
        internal,
        expectedVersion: ticket.version,
      }),
    )
    .then(({ body }) => body);
}
