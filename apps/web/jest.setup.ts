import "@testing-library/jest-dom";
import {
  Headers as EdgeHeaders,
  Request as EdgeRequest,
  Response as EdgeResponse,
} from "next/dist/compiled/@edge-runtime/primitives";
import mockEnglishMessages from "./messages/en.json";

Object.assign(globalThis, {
  Headers: EdgeHeaders,
  Request: EdgeRequest,
  Response: EdgeResponse,
});

jest.mock("next-intl", () => {
  const React = jest.requireActual<typeof import("react")>("react");
  return {
    useLocale: () => "en",
    NextIntlClientProvider: ({ children }: { children: React.ReactNode }) =>
      React.createElement(React.Fragment, null, children),
    useTranslations: (namespace?: string) => (key: string) => {
      const value = namespace
        ? namespace
            .split(".")
            .concat(key.split("."))
            .reduce(
              (current: Record<string, unknown>, part) =>
                (current[part] as Record<string, unknown>) ?? {},
              mockEnglishMessages as Record<string, unknown>,
            )
        : key;
      return typeof value === "string" ? value : key;
    },
  };
});

jest.mock("next-intl/navigation", () => {
  const React = jest.requireActual<typeof import("react")>("react");
  return {
    createNavigation: () => ({
      Link: (props: Record<string, unknown>) =>
        React.createElement(
          "a",
          { ...props, href: typeof props.href === "string" ? props.href : "/" },
          props.children as React.ReactNode,
        ),
      usePathname: () => "/",
      useRouter: () => ({
        push: jest.fn(),
        replace: jest.fn(),
        refresh: jest.fn(),
        back: jest.fn(),
        forward: jest.fn(),
        prefetch: jest.fn(),
      }),
      getPathname: jest.fn(),
      redirect: jest.fn(),
    }),
  };
});

jest.mock("next-intl/routing", () => ({
  defineRouting: (config: unknown) => config,
}));
