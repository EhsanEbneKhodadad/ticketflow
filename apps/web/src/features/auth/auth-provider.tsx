"use client";
import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "@/i18n/navigation";
import type { components } from "@ticketflow/api-client";
import { api, ApiError } from "@/lib/api";
import { useTranslations } from "next-intl";

export type User = components["schemas"]["UserView"];
type Login = components["schemas"]["LoginRequest"];
type Registration = components["schemas"]["RegisterRequest"];
type AuthContextValue = {
  user: User | null;
  loading: boolean;
  error: Error | null;
  sessionNotice: string;
  signIn: (input: Login) => Promise<void>;
  register: (input: Registration) => Promise<void>;
  signOut: () => Promise<void>;
  clearSession: () => void;
  retry: () => void;
};
const Context = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const errors = useTranslations("Errors");
  const client = useQueryClient();
  const router = useRouter();
  const [sessionNotice, setSessionNotice] = useState("");
  const session = useQuery({
    queryKey: ["session"],
    staleTime: 60000,
    queryFn: async () => {
      try {
        const next = await api<User>("/auth/me");
        const previous = client.getQueryData<User | null>(["session"]);
        if (previous?.id !== next.id) {
          await client.cancelQueries({
            predicate: (query) => query.queryKey[0] !== "session",
          });
          client.removeQueries({
            predicate: (query) => query.queryKey[0] !== "session",
          });
        }
        return next;
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          await client.cancelQueries({
            predicate: (query) => query.queryKey[0] !== "session",
          });
          client.removeQueries({
            predicate: (query) => query.queryKey[0] !== "session",
          });
          return null;
        }
        throw error;
      }
    },
  });
  useEffect(() => {
    const expired = (event: Event) => {
      setSessionNotice(
        (event as CustomEvent<{ code?: string }>).detail?.code ===
          "account_changed"
          ? errors("accountChanged")
          : "",
      );
      client.clear();
      client.setQueryData(["session"], null);
      router.replace("/login");
    };
    window.addEventListener("ticketflow:session-expired", expired);
    return () =>
      window.removeEventListener("ticketflow:session-expired", expired);
  }, [client, errors, router]);

  async function authenticate(path: string, input: Login | Registration) {
    const user = await api<User>(path, {
      method: "POST",
      body: JSON.stringify(input),
    });
    client.clear();
    setSessionNotice("");
    client.setQueryData(["session"], user);
    router.push("/tickets");
  }

  return (
    <Context.Provider
      value={{
        user: session.data ?? null,
        loading: session.isPending,
        error: session.error,
        sessionNotice,
        signIn: (input) => authenticate("/auth/login", input),
        register: (input) => authenticate("/auth/register", input),
        signOut: async () => {
          await api<void>("/auth/logout", { method: "POST", body: "{}" });
          client.clear();
          client.setQueryData(["session"], null);
          router.replace("/login");
        },
        clearSession: () => {
          client.clear();
          client.setQueryData(["session"], null);
        },
        retry: () => {
          void session.refetch();
        },
      }}
    >
      {children}
    </Context.Provider>
  );
}

export function useAuth() {
  const context = useContext(Context);
  if (!context) throw new Error("AuthProvider is required.");
  return context;
}
