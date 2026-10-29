"use client";
import Link from "next/link";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import type { components } from "@ticketflow/api-client";
import { useAuth, type User } from "@/features/auth/auth-provider";
import { api, errorMessage } from "@/lib/api";
import { Button } from "@/components/ui/button";

type DashboardView = components["schemas"]["DashboardView"];
export function Dashboard() {
  const { user } = useAuth();
  const client = useQueryClient();
  const principal = user?.id;
  const query = useQuery({
    queryKey: ["dashboard", principal],
    enabled: !!principal,
    retry: false,
    queryFn: ({ signal }) =>
      api<DashboardView>("/dashboard", {
        signal,
        expectedPrincipalId: principal,
        isCurrent: () =>
          !!principal &&
          client.getQueryData<User>(["session"])?.id === principal,
      }),
  });
  const staff = user?.role !== "Customer";
  const cards: Array<[string, number | string]> = query.data
    ? [
        [staff ? "All tickets" : "Your tickets", query.data.total],
        ["Open", query.data.open],
        ["In progress", query.data.inProgress],
        ["Waiting for customer", query.data.waitingForCustomer],
        ["Resolved", query.data.resolved],
        ["Closed", query.data.closed],
        ["Unassigned", query.data.unassigned],
        ["High or urgent priority", query.data.highPriority],
      ]
    : [];
  return (
    <section>
      <div className="page-heading">
        <div>
          <p className="page-eyebrow">YOUR WORKSPACE</p>
          <h1>Dashboard</h1>
          <p>
            {staff
              ? "Current ticket counts across the support workspace."
              : "Current counts for your tickets."}
          </p>
        </div>
        <Link href="/tickets">View tickets</Link>
      </div>
      {query.isPending && <p role="status">Loading dashboard…</p>}
      {query.isError && (
        <div className="card page-state">
          <p role="alert">{errorMessage(query.error)}</p>
          <Button onClick={() => void query.refetch()}>Try again</Button>
        </div>
      )}
      {query.data && (
        <>
          <p className="dashboard-caption">
            Counts include all ticket creation dates. Unassigned includes every
            status.
          </p>
          <div className="dashboard-grid">
            {cards.map(([label, count]) => (
              <article className="card dashboard-card" key={label}>
                <h2>{label}</h2>
                <p>{count}</p>
              </article>
            ))}
          </div>
          {Number(query.data.total) === 0 && (
            <p className="dashboard-empty">
              No tickets yet. <Link href="/tickets/new">Create a ticket</Link>
            </p>
          )}
        </>
      )}
    </section>
  );
}
