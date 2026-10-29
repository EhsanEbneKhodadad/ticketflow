"use client";
import { useState, type ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider } from "@/features/auth/auth-provider";
import { NextIntlClientProvider, type AbstractIntlMessages } from "next-intl";
import defaultMessages from "../../messages/en.json";

export function AppProviders({
  children,
  locale = "en",
  messages = defaultMessages,
}: {
  children: ReactNode;
  locale?: string;
  messages?: AbstractIntlMessages;
}) {
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30000,
            retry: false,
            refetchOnWindowFocus: true,
          },
          mutations: { retry: false },
        },
      }),
  );
  return (
    <NextIntlClientProvider locale={locale} messages={messages}>
      <QueryClientProvider client={client}>
        <AuthProvider>{children}</AuthProvider>
      </QueryClientProvider>
    </NextIntlClientProvider>
  );
}
