import { defineConfig } from "cypress";
export default defineConfig({
  e2e: {
    setupNodeEvents(on) {
      on("before:browser:launch", (browser, options) => {
        if (browser.name === "electron" && browser.isHeadless) {
          options.preferences.width = 1600;
          options.preferences.height = 1600;
        }
        return options;
      });
    },
    baseUrl: "http://localhost:3000",
    supportFile: false,
    specPattern: "cypress/e2e/**/*.cy.ts",
  },
  viewportWidth: 1440,
  viewportHeight: 1000,
  defaultCommandTimeout: 15000,
  requestTimeout: 15000,
  video: false,
  screenshotOnRunFailure: true,
  expose: {
    demoPassword: "TicketFlowDemo!2026",
    mailpitUrl: process.env.TICKETFLOW_MAILPIT_URL ?? "http://localhost:8025",
  },
});
