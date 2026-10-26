import { render, screen } from "@testing-library/react";
import Home from "./page";
describe("foundation landing page", () => {
  it("identifies the support workspace and its current stage", () => {
    render(<Home />);
    expect(
      screen.getByRole("heading", {
        name: "A clear path from question to resolution.",
      }),
    ).toBeInTheDocument();
    expect(screen.getByText("Milestone 1 · Foundation")).toBeInTheDocument();
  });
  it("links to the workflow and explains the architecture", () => {
    render(<Home />);
    expect(
      screen.getByRole("link", { name: "Explore the workflow" }),
    ).toHaveAttribute("href", "#workflow");
    expect(document.getElementById("workflow")).toBeInTheDocument();
    expect(document.getElementById("architecture")).toBeInTheDocument();
  });
});
