import { createTicket, registerCustomer, signIn } from "../helpers/api";

describe("Dashboard and administration", () => {
  it("shows a customer's current ticket count and denies admin access", () => {
    registerCustomer("dashboard");
    createTicket("Dashboard request for help");
    cy.visit("/dashboard");
    cy.contains("h1", "Dashboard").should("be.visible");
    cy.contains("article", "Your tickets").should("contain.text", "1");
    cy.visit("/admin");
    cy.contains("Administrator access required.").should("be.visible");
    cy.request({ url: "/api/v1/admin/users", failOnStatusCode: false })
      .its("status")
      .should("eq", 403);
  });
  it("lets administrators create a category on a narrow viewport", () => {
    const slug = "e2e-" + Date.now();
    signIn("admin@ticketflow.local");
    cy.viewport(390, 844);
    cy.visit("/admin");
    cy.get("#name-new").type("E2E category");
    cy.get("#slug-new").type(slug);
    cy.contains("button", "Create category").click();
    cy.contains("Category created.").should("be.visible");
    cy.get("input")
      .filter((_, element) => (element as HTMLInputElement).value === slug)
      .should("have.length", 1);
    cy.contains("Users and roles").should("be.visible");
  });
});
