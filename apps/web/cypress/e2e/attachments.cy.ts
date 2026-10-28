import {
  createTicket,
  getTicket,
  registerCustomer,
  reply,
  signIn,
  write,
  type Schemas,
} from "../helpers/api";
const select = (
  selector: string,
  name: string,
  contents: string | ReturnType<typeof Cypress.Buffer.alloc>,
) =>
  cy.get(selector).selectFile({
    contents:
      typeof contents === "string" ? Cypress.Buffer.from(contents) : contents,
    fileName: name,
    mimeType: "text/plain",
  });
describe("Milestone 3 private attachments", () => {
  it("uploads and downloads original files, handles conflicts and protects internal reply metadata", () => {
    let owner = "",
      id = "",
      publicId = "",
      internalId = "";
    registerCustomer("attachments").then((email) => {
      owner = email;
    });
    createTicket("Share billing evidence securely").then((ticket) => {
      id = ticket.id;
      cy.visit("/tickets/" + id);
    });
    cy.contains("h1", "Share billing evidence securely").should("be.visible");
    select(
      'section[aria-label="Ticket attachments"] input[type=file]',
      "billing-notes.txt",
      "Original billing evidence\n",
    );
    cy.intercept("POST", "/api/v1/tickets/*/attachments").as("upload");
    cy.get('section[aria-label="Ticket attachments"]')
      .contains("button", "Upload file")
      .click();
    cy.wait("@upload").then(({ response }) => {
      expect(response!.statusCode).eq(201);
      publicId = response!.body.id;
    });
    cy.get('[aria-label="Download billing-notes.txt"]')
      .should("be.visible")
      .click();
    cy.readFile("cypress/downloads/billing-notes.txt").should(
      "eq",
      "Original billing evidence\n",
    );
    cy.scrollTo("top");
    cy.screenshot("m3-ticket-attachments", { capture: "viewport" });
    cy.get("#reply-body").type(
      "The attached text captures the exact account error.",
    );
    cy.contains("button", "Send reply").click();
    cy.contains("[role=status]", "Reply sent.").should("be.visible");
    cy.get(".conversation-message")
      .last()
      .within(() => {
        select(
          "input[type=file]",
          "reply-evidence.txt",
          "Public reply evidence",
        );
        cy.contains("button", "Upload file").click();
      });
    cy.wait("@upload").its("response.statusCode").should("eq", 201);
    cy.get('[aria-label="Download reply-evidence.txt"]').should("be.visible");
    cy.get('section[aria-label="Reply attachments"]')
      .last()
      .scrollIntoView({ offset: { top: -220, left: 0 } })
      .should("be.visible");
    cy.screenshot("m3-reply-attachments", { capture: "viewport" });
    // Capture a stale ticket version by changing the real ticket while the form remains mounted.
    select(
      'section[aria-label="Ticket attachments"] input[type=file]',
      "preserved.txt",
      "Conflict draft",
    );
    cy.then(() => getTicket(id)).then((ticket) =>
      write("PUT", `/tickets/${id}`, {
        title: ticket.title,
        description: ticket.description,
        categoryId: ticket.categoryId,
        tags: ticket.tags,
        expectedVersion: ticket.version,
      }),
    );
    cy.get('section[aria-label="Ticket attachments"]')
      .contains("button", "Upload file")
      .click();
    cy.wait("@upload").its("response.statusCode").should("eq", 409);
    cy.contains("[role=alert]", "selected file is preserved").should(
      "be.visible",
    );
    cy.contains("Selected: preserved.txt").should("be.visible");
    cy.then(() => signIn("agent@ticketflow.local"));
    cy.then(() => reply(id, "Team-only investigation details", true));
    cy.then(() => cy.visit("/tickets/" + id));
    cy.contains(".internal-message", "Team-only investigation details").within(
      () => {
        select(
          "input[type=file]",
          "staff-investigation.txt",
          "Private investigation",
        );
        cy.contains("button", "Upload file").click();
      },
    );
    cy.wait("@upload").then(({ response }) => {
      expect(response!.statusCode).eq(201);
      internalId = response!.body.id;
    });
    cy.get('[aria-label="Download staff-investigation.txt"]').should(
      "be.visible",
    );
    cy.then(() => signIn(owner));
    cy.then(() => cy.visit("/tickets/" + id));
    cy.contains("h1", "Share billing evidence securely").should("be.visible");
    cy.contains("staff-investigation.txt").should("not.exist");
    cy.contains("Team-only investigation details").should("not.exist");
    cy.then(() =>
      cy.request<Schemas["AttachmentView"][]>({
        url: `/api/v1/tickets/${id}/attachments`,
        log: false,
      }),
    ).then(({ body }) => expect(body.some((file) => file.internal)).eq(false));
    cy.then(() =>
      cy.request({
        url: `/api/v1/tickets/${id}/attachments/${internalId}/download`,
        failOnStatusCode: false,
        log: false,
      }),
    )
      .its("status")
      .should("eq", 404);
    cy.get('[aria-label="Download billing-notes.txt"]').should("be.visible");
    registerCustomer("foreign-file");
    cy.then(() =>
      cy.request({
        url: `/api/v1/tickets/${id}/attachments/${publicId}/download`,
        failOnStatusCode: false,
        log: false,
      }),
    )
      .its("status")
      .should("eq", 404);
  });
  it("accepts the full 10 MiB through the real proxy and disables closed-ticket uploads", () => {
    let id = "";
    registerCustomer("file-boundary");
    createTicket("Full-size diagnostic text").then((ticket) => {
      id = ticket.id;
      cy.visit("/tickets/" + id);
    });
    cy.contains("h1", "Full-size diagnostic text").should("be.visible");
    select(
      'section[aria-label="Ticket attachments"] input[type=file]',
      "large-diagnostic.txt",
      Cypress.Buffer.alloc(10485760, 65),
    );
    cy.intercept("POST", "/api/v1/tickets/*/attachments").as("boundary");
    cy.get('section[aria-label="Ticket attachments"]')
      .contains("button", "Upload file")
      .click();
    cy.wait("@boundary", { timeout: 60000 })
      .its("response.statusCode")
      .should("eq", 201);
    cy.get('[aria-label="Download large-diagnostic.txt"]').should("be.visible");
    cy.then(() => signIn("agent@ticketflow.local"));
    cy.then(() => getTicket(id)).then((ticket) =>
      write("PATCH", `/tickets/${id}/status`, {
        status: "resolved",
        expectedVersion: ticket.version,
      }),
    );
    cy.then(() => getTicket(id)).then((ticket) =>
      write("PATCH", `/tickets/${id}/status`, {
        status: "closed",
        expectedVersion: ticket.version,
      }),
    );
    cy.then(() => cy.visit("/tickets/" + id));
    cy.get('section[aria-label="Ticket attachments"] input[type=file]').should(
      "be.disabled",
    );
    cy.get('section[aria-label="Ticket attachments"]')
      .contains("button", "Upload file")
      .should("be.disabled");
    cy.get('[aria-label="Download large-diagnostic.txt"]').should("be.enabled");
  });
});
