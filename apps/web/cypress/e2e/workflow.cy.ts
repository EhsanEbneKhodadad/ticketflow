import type { components } from "@ticketflow/api-client";
type Schemas = components["schemas"];
const general = "aabed648-e725-4b50-93c1-2cc6394c1941";
const password = () => String(Cypress.expose("demoPassword"));
function write<T = unknown>(
  method: "POST" | "PATCH" | "PUT",
  path: string,
  body: object,
): Cypress.Chainable<Cypress.Response<T>> {
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
function signIn(email: string) {
  cy.clearAllCookies();
  return write<Schemas["UserView"]>("POST", "/auth/login", {
    email,
    password: password(),
  });
}
function fixture(title: string, priority: Schemas["TicketPriority"]) {
  return cy
    .request<
      Schemas["TicketPage"]
    >({ url: "/api/v1/tickets?q=" + encodeURIComponent(title), log: false })
    .then(({ body }) => {
      if (!body.items.some((ticket) => ticket.title === title))
        return write("POST", "/tickets", {
          title,
          description:
            "A development support request used to exercise the real inbox and filtering workflow.",
          categoryId: general,
          tags: ["account"],
          priority,
        });
    });
}

describe("Milestone 2 complete workflow", () => {
  before(() => {
    signIn("customer@ticketflow.local");
    fixture("Unable to download the latest invoice", "medium");
    fixture("Two-factor sign-in code is not arriving", "urgent");
    fixture("Update workspace email preferences", "low");
    write("POST", "/auth/logout", {});
    cy.clearAllCookies();
  });

  it("registers, creates and edits a ticket, supports the agent journey, and protects internal notes", () => {
    const email = "taylor." + Date.now() + "@example.test";
    let ticketPath = "";
    cy.visit("/login");
    cy.get("#email").should("be.visible").and("not.be.disabled");
    cy.screenshot("sign-in", { capture: "viewport" });
    cy.visit("/register");
    cy.get("#name").should("not.be.disabled").type("Taylor Reed");
    cy.get("#email").should("not.be.disabled").type(email);
    cy.get("#password")
      .should("not.be.disabled")
      .type(password(), { log: false });
    cy.contains("button", "Create account").click();
    cy.location("pathname").should("eq", "/tickets");
    cy.get('a[href="/tickets/new"]').last().click();
    cy.get("#ticket-title").type("Cannot update the billing contact");
    cy.get("#ticket-description").type(
      "The billing settings page shows an error when I try to change our contact email. I have tried another browser and the issue continues.",
    );
    cy.get("#ticket-category").select("Billing");
    cy.get("#ticket-priority").select("high");
    cy.get("#ticket-tags").type("billing, account");
    cy.intercept("POST", "/api/v1/tickets").as("created");
    cy.contains("button", "Create ticket").click();
    cy.wait("@created").then(({ response }) => {
      ticketPath = "/tickets/" + response!.body.id;
    });
    cy.contains("h1", "Cannot update the billing contact").should("be.visible");
    cy.get("#reply-body").type(
      "The error also happens after signing out and back in.",
    );
    cy.contains("button", "Send reply").click();
    cy.contains("[role=status]", "Reply sent.").should("be.visible");
    cy.contains("button", "Edit ticket").click();
    cy.get("#ticket-title")
      .clear()
      .type("Unable to update the billing contact");
    cy.contains("button", "Save changes").click();
    cy.contains("h1", "Unable to update the billing contact").should(
      "be.visible",
    );

    signIn("agent@ticketflow.local");
    cy.visit("/tickets");
    cy.contains("h1", "Support inbox").should("be.visible");
    cy.contains(
      "a.ticket-title-link",
      "Unable to update the billing contact",
    ).should("be.visible");
    cy.get("body").then((body) =>
      expect(body[0].scrollWidth).to.be.at.most(1440),
    );
    cy.screenshot("agent-inbox", { capture: "viewport" });
    cy.get('input[aria-label="Search tickets"]').type("billing contact");
    cy.get(".search-field").submit();
    cy.contains(
      "a.ticket-title-link",
      "Unable to update the billing contact",
    ).click();
    cy.get("#manage-status").select("inProgress");
    cy.get('button[aria-label="Update status"]').click();
    cy.get(".ticket-heading-badges").should("contain.text", "In progress");
    cy.get("#manage-assignment").select("Jordan Lee");
    cy.get('button[aria-label="Update assignee"]').click();
    cy.contains(".side-field", "Jordan Lee").should("exist");
    cy.contains("button", /^Internal note$/).click();
    cy.get("#reply-body").type(
      "Private: verify the billing audit before changing the contact. Keep the original address on file.",
    );
    cy.contains("button", "Add internal note").click();
    cy.contains(".internal-note-label", "Team only").should("be.visible");
    cy.contains("button", /^Public reply$/).click();
    cy.get("#reply-body").type(
      "Thanks for the details, Taylor. We found the issue and updated the billing settings. Please try changing the contact email again.",
    );
    cy.contains("button", "Send reply").click();
    cy.contains("[role=status]", "Reply sent.").should("be.visible");
    cy.scrollTo("top");
    cy.screenshot("agent-ticket-detail", { capture: "viewport" });
    cy.get("#manage-status").select("resolved");
    cy.get('button[aria-label="Update status"]').click();
    cy.get(".ticket-heading-badges").should("contain.text", "Resolved");

    signIn(email);
    cy.then(() => cy.visit(ticketPath));
    cy.contains("h1", "Unable to update the billing contact").should(
      "be.visible",
    );
    cy.contains("Private: verify the billing audit").should("not.exist");
    cy.contains("button", /^Internal note$/).should("not.exist");
    cy.contains("button", "Close ticket").click();
    cy.get(".ticket-heading-badges").should("contain.text", "Closed");
    cy.get("#reply-body").should("be.disabled");
    cy.contains("button", "Reopen ticket").click();
    cy.get(".ticket-heading-badges").should("contain.text", "Open");
    cy.scrollTo("top");
    cy.screenshot("customer-ticket-detail", { capture: "viewport" });
    cy.viewport(390, 844);
    cy.then(() => cy.visit(ticketPath));
    cy.contains("h1", "Unable to update the billing contact").should(
      "be.visible",
    );
    cy.get("body").then((body) =>
      expect(body[0].scrollWidth).to.be.at.most(392),
    );
    cy.scrollTo("top");
    cy.screenshot("mobile-ticket-detail", { capture: "viewport" });
  });

  it("redirects guests and hides another customer's ticket in the UI", () => {
    cy.clearAllCookies();
    cy.visit("/tickets");
    cy.location("pathname").should("eq", "/login");
    signIn("customer@ticketflow.local");
    write<Schemas["TicketView"]>("POST", "/tickets", {
      title: "Private customer access request",
      description: "Only the requesting customer may read this ticket.",
      categoryId: general,
      tags: [],
      priority: "low",
    }).then(({ body: ticket }) => {
      cy.clearAllCookies();
      write("POST", "/auth/register", {
        email: "isolated." + Date.now() + "@example.test",
        password: password(),
        name: "Another Customer",
      });
      cy.visit("/tickets/" + ticket.id);
      cy.contains("h2", "Ticket not found.").should("be.visible");
      cy.contains("Private customer access request").should("not.exist");
      cy.visit("/tickets");
      cy.contains("A fresh start for your support.").should("be.visible");
    });
  });

  it("delivers a real reset email and accepts the new password after API reset", () => {
    const email = "reset." + Date.now() + "@example.test";
    const changed = "ChangedPassword!2026";
    cy.clearAllCookies();
    write("POST", "/auth/register", {
      email,
      password: password(),
      name: "Reset Customer",
    });
    cy.visit("/forgot-password");
    cy.get("#email").should("not.be.disabled").type(email);
    cy.contains("button", "Send reset link").click();
    cy.contains("If an account exists, a reset link has been sent.").should(
      "be.visible",
    );
    cy.request({
      url: String(Cypress.expose("mailpitUrl")) + "/api/v1/messages",
      log: false,
    }).then(({ body }) => {
      const message = body.messages.find(
        (item: { To: { Address: string }[] }) =>
          item.To.some((to) => to.Address === email),
      );
      expect(message, "captured reset email").not.to.equal(undefined);
      cy.request({
        url:
          String(Cypress.expose("mailpitUrl")) +
          "/api/v1/message/" +
          message.ID,
        log: false,
      }).then(({ body: content }) => {
        const link = String(content.Text)
          .split(String.fromCharCode(10))
          .map((line) => line.trim())
          .find((line) =>
            line.startsWith("http://localhost:3000/reset-password?"),
          );
        expect(link, "reset link").to.be.a("string");
        const url = new URL(link!);
        cy.visit(url.pathname + url.search);
        cy.get("#email").should("have.value", email);
        write("POST", "/auth/reset-password", {
          email,
          token: url.searchParams.get("token"),
          password: changed,
        });
        cy.visit("/login");
        cy.get("#email").should("not.be.disabled").type(email);
        cy.get("#password")
          .should("not.be.disabled")
          .type(changed, { log: false });
        cy.contains("button", "Sign in").click();
        cy.location("pathname").should("eq", "/tickets");
      });
    });
  });
});
