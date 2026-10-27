import { fireEvent, render, screen } from "@testing-library/react";
import { AppProviders } from "@/components/providers";
import { AuthScreen } from "./auth-screen";

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: jest.fn(), replace: jest.fn() }),
  usePathname: () => "/register",
}));

function response(body: object, status = 200) {
  return Promise.resolve({
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  } as Response);
}

beforeEach(() => {
  global.fetch = jest.fn((input) => {
    const url = String(input);
    if (url.endsWith("/auth/csrf")) return response({ token: "test-token" });
    return response({ detail: "Invalid email or password." }, 401);
  }) as jest.Mock;
});

it("shows field errors before an invalid registration can be submitted", async () => {
  render(
    <AppProviders>
      <AuthScreen mode="register" />
    </AppProviders>,
  );
  fireEvent.click(screen.getByRole("button", { name: "Create account" }));
  expect(await screen.findByText("Enter your name.")).toBeInTheDocument();
  expect(screen.getByText("Enter a valid email.")).toBeInTheDocument();
  expect(screen.getByText("Use at least 12 characters.")).toBeInTheDocument();
});

it("shows the backend sign-in error while keeping the password masked", async () => {
  render(
    <AppProviders>
      <AuthScreen mode="login" />
    </AppProviders>,
  );
  fireEvent.change(screen.getByLabelText("Email address"), {
    target: { value: "person@example.test" },
  });
  fireEvent.change(screen.getByLabelText("Password"), {
    target: { value: "WrongPassword!2026" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Sign in" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "Invalid email or password.",
  );
  expect(screen.getByLabelText("Password")).toHaveAttribute("type", "password");
});
