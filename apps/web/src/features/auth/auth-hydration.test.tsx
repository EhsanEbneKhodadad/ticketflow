/** @jest-environment node */
import { renderToString } from "react-dom/server";
import { AppProviders } from "@/components/providers";
import { AuthScreen } from "./auth-screen";
jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: jest.fn(), replace: jest.fn() }),
}));
it("prevents unhydrated authentication forms from sending credentials in a GET URL", () => {
  const html = renderToString(
    <AppProviders>
      <AuthScreen mode="login" />
    </AppProviders>,
  );
  expect(html).toMatch(/<form[^>]*method="post"/);
  expect(html).toMatch(/<fieldset[^>]*disabled/);
});
