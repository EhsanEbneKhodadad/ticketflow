import {
  createTicket,
  general,
  getTicket,
  registerCustomer,
  reply,
  signIn,
  write,
  type Schemas,
} from "../helpers/api";
// Poll the actual API until the real worker/Elasticsearch projection contains the latest public reply.
function indexed(
  term: string,
  id: string,
  deadline = Date.now() + 120000,
): Cypress.Chainable<Cypress.Response<Schemas["TicketSearchPage"]>> {
  return cy
    .request<
      Schemas["TicketSearchPage"]
    >({ url: "/api/v1/search/tickets?q=" + encodeURIComponent(term), failOnStatusCode: false, log: false })
    .then((response) => {
      if (
        response.status === 200 &&
        response.body.items.some((item) => item.ticket.id === id)
      )
        return cy.wrap(response, { log: false });
      expect(Date.now(), "public reply indexed before deadline").lessThan(
        deadline,
      );
      return cy
        .wait(1000, { log: false })
        .then(() => indexed(term, id, deadline));
    });
}
describe("Milestone 3 real public search", () => {
  it("finds public replies with safe highlights and filters while excluding internal and foreign content", () => {
    const token = String(Date.now());
    const publicTerm = "publicanswer" + token,
      privateTerm = "privateaudit" + token;
    let id = "",
      owner = "",
      agentId = "",
      day = "";
    registerCustomer("search").then((email) => {
      owner = email;
    });
    createTicket("Restore workspace billing access").then((ticket) => {
      id = ticket.id;
      day = ticket.createdAt.slice(0, 10);
    });
    signIn("agent@ticketflow.local").then(({ body }) => {
      agentId = body.id;
    });
    cy.then(() => getTicket(id)).then((ticket) =>
      write("PATCH", `/tickets/${id}/status`, {
        status: "inProgress",
        expectedVersion: ticket.version,
      }),
    );
    cy.then(() => getTicket(id)).then((ticket) =>
      write("PATCH", `/tickets/${id}/assignment`, {
        agentId,
        expectedVersion: ticket.version,
      }),
    );
    // Internal first, public second: the observed public term proves the later state was projected.
    cy.then(() => reply(id, "Internal investigation " + privateTerm, true));
    cy.then(() =>
      reply(
        id,
        `The billing connection is restored. ${publicTerm} <img src=x onerror=alert(1)>`,
        false,
      ),
    );
    cy.then(() => indexed(publicTerm, id));
    cy.then(() => cy.visit("/search?q=" + publicTerm));
    cy.get(".search-result-list").should(
      "contain.text",
      "Restore workspace billing access",
    );
    cy.get(".search-snippet mark").should("contain.text", publicTerm);
    cy.get(".search-snippet").should(
      "contain.text",
      "<img src=x onerror=alert(1)>",
    );
    cy.get(".search-snippet img").should("not.exist");
    cy.get("#search-status").select("inProgress");
    cy.get("#search-priority").select("high");
    cy.get("#search-category").select(general);
    cy.then(() => cy.get("#search-agent").select(agentId));
    cy.then(() => cy.get("#search-from").type(day));
    cy.then(() => cy.get("#search-to").type(day));
    cy.get("#search-sort").select("newest");
    cy.contains("button", "Search public content").click();
    cy.location("search")
      .should("contain", "status=inProgress")
      .and("contain", "priority=high")
      .and("contain", "sort=newest");
    cy.get(".search-result-list").should(
      "contain.text",
      "Restore workspace billing access",
    );
    cy.scrollTo("top");
    cy.screenshot("m3-search-results", { capture: "viewport" });
    // Filter-only requests use the same actual service.
    cy.get("#search-query").clear();
    cy.contains("button", "Search public content").click();
    cy.get(".search-result-list").should(
      "contain.text",
      "Restore workspace billing access",
    );
    cy.then(() => cy.visit("/search?q=" + privateTerm));
    cy.contains("No tickets match your search.").should("be.visible");
    cy.then(() => signIn(owner));
    cy.then(() => cy.visit("/search?q=" + publicTerm));
    cy.get(".search-result-list").should(
      "contain.text",
      "Restore workspace billing access",
    );
    cy.then(() => cy.visit("/search?q=" + privateTerm));
    cy.contains("No tickets match your search.").should("be.visible");
    cy.then(() => cy.visit("/search?q=" + publicTerm));
    cy.viewport(390, 844);
    cy.get(".search-result-list").should(
      "contain.text",
      "Restore workspace billing access",
    );
    cy.get("body").then((body) => expect(body[0].scrollWidth).at.most(392));
    cy.scrollTo("top");
    cy.screenshot("m3-search-mobile", { capture: "viewport" });
    registerCustomer("foreign-search");
    cy.then(() => cy.visit("/search?q=" + publicTerm));
    cy.contains("No tickets match your search.").should("be.visible");
    cy.contains("Restore workspace billing access").should("not.exist");
  });
});
