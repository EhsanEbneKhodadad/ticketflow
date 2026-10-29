"use client";
import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { hashKey, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "@/i18n/navigation";
import {
  getApiV1AuthMe,
  getApiV1AuthMeQueryKey,
  postApiV1AuthLoginMutation,
  postApiV1AuthRegisterMutation,
  postApiV1AuthLogoutMutation,
  type LoginRequest,
  type RegisterRequest,
  type UserView,
} from "@ticketflow/api-client";
import { ApiError } from "@/lib/api";
import { useTranslations } from "next-intl";

export type User = UserView;
type Login = LoginRequest;
type Registration = RegisterRequest;
export const sessionQueryKey = () => getApiV1AuthMeQueryKey();

function clearPrivateQueries(client: ReturnType<typeof useQueryClient>) {
  const sessionHash = hashKey(sessionQueryKey());
  const isPrivateQuery = (query: { queryHash: string }) =>
    query.queryHash !== sessionHash;
  return client.cancelQueries({ predicate: isPrivateQuery }).then(() => {
    client.removeQueries({ predicate: isPrivateQuery });
  });
}

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
  const session = useQuery<
    User | null,
    Error,
    User | null,
    ReturnType<typeof sessionQueryKey>
  >({
    queryKey: sessionQueryKey(),
    staleTime: 60000,
    queryFn: async ({ signal }) => {
      try {
        const { data: next } = await getApiV1AuthMe({
          signal,
          throwOnError: true,
        });
        const previous = client.getQueryData<User | null>(sessionQueryKey());
        if (previous?.id !== next.id) {
          await clearPrivateQueries(client);
        }
        return next;
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          await clearPrivateQueries(client);
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
      client.setQueryData(sessionQueryKey(), null);
      router.replace("/login");
    };
    window.addEventListener("ticketflow:session-expired", expired);
    return () =>
      window.removeEventListener("ticketflow:session-expired", expired);
  }, [client, errors, router]);

  async function authenticate(path: string, input: Login | Registration) {
    const request =
      path === "/auth/login"
        ? postApiV1AuthLoginMutation().mutationFn!(
            {
              body: input as Login,
              throwOnError: true,
            },
            undefined as never,
          )
        : postApiV1AuthRegisterMutation().mutationFn!(
            {
              body: input as Registration,
              throwOnError: true,
            },
            undefined as never,
          );
    const user = await request;
    client.clear();
    setSessionNotice("");
    client.setQueryData(sessionQueryKey(), user);
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
          await postApiV1AuthLogoutMutation().mutationFn!(
            {
              throwOnError: true,
            },
            undefined as never,
          );
          client.clear();
          client.setQueryData(sessionQueryKey(), null);
          router.replace("/login");
        },
        clearSession: () => {
          client.clear();
          client.setQueryData(sessionQueryKey(), null);
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
