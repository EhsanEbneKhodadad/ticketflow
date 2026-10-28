"use client";
import Link from "next/link";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useAuth, type User } from "@/features/auth/auth-provider";
import {
  statusLabels,
  priorityLabels,
  dateLabel,
} from "@/features/tickets/api";
import { api, ApiError, errorMessage } from "@/lib/api";
import type { components } from "@ticketflow/api-client";
import {
  searchParameters,
  searchTickets,
  type SearchSegment,
  type SearchSort,
} from "./api";

export function SearchSnippet({ segments }: { segments: SearchSegment[] }) {
  return (
    <p className="search-snippet">
      {segments.map((segment, index) =>
        segment.highlighted ? (
          <mark key={index}>{segment.text}</mark>
        ) : (
          <span key={index}>{segment.text}</span>
        ),
      )}
    </p>
  );
}
export function TicketSearch({ params }: { params: Record<string, string> }) {
  const { user } = useAuth();
  const client = useQueryClient();
  const router = useRouter();
  const [validation, setValidation] = useState("");
  const queryParams = searchParameters(params);
  const serialized = queryParams.toString();
  const page = Number(queryParams.get("page"));
  const principal = user?.id;
  const isCurrent = () =>
    !!principal && client.getQueryData<User>(["session"])?.id === principal;
  const { register, handleSubmit } = useForm<Record<string, string>>({
    defaultValues: { ...params, sort: params.sort ?? "relevance" },
  });
  const query = useQuery({
    queryKey: ["search", principal, serialized],
    queryFn: ({ signal }) =>
      searchTickets(queryParams, signal, principal!, isCurrent),
    enabled: !!user,
    retry: false,
  });
  const categories = useQuery({
    queryKey: ["categories", principal],
    queryFn: ({ signal }) =>
      api<components["schemas"]["CategoryView"][]>("/categories", {
        signal,
        isCurrent,
        expectedPrincipalId: principal,
      }),
    enabled: !!user,
  });
  const agents = useQuery({
    queryKey: ["agents", principal],
    queryFn: ({ signal }) =>
      api<components["schemas"]["AgentView"][]>("/agents", {
        signal,
        isCurrent,
        expectedPrincipalId: principal,
      }),
    enabled: !!user && user.role !== "Customer",
  });
  const sortLabels: Record<SearchSort, string> = {
    relevance: "Relevance",
    newest: "Newest first",
  };
  const navigatePage = (value: number) => {
    const next = new URLSearchParams(queryParams);
    next.set("page", String(value));
    router.push("/search?" + next);
  };
  return (
    <section>
      <div className="page-heading">
        <div>
          <p className="page-eyebrow">FIND THE CONVERSATION</p>
          <h1>Search tickets</h1>
          <p>Search ticket numbers, requests, tags and public replies.</p>
        </div>
        <Search size={25} color="#718568" />
      </div>
      <form
        className="card search-form"
        onSubmit={handleSubmit((input) => {
          setValidation("");
          if (input.from && input.to && input.from > input.to) {
            setValidation("Choose an end date on or after the start date.");
            return;
          }
          router.push("/search?" + searchParameters({ ...input, page: "1" }));
        })}
      >
        <div className="form-field search-query">
          <label htmlFor="search-query">Keywords or ticket number</label>
          <input
            id="search-query"
            placeholder="Search public conversations…"
            maxLength={160}
            {...register("q")}
          />
          <p className="field-hint">
            Leave keywords blank to search using filters only.
          </p>
        </div>
        <div className="search-filter-grid">
          <div className="form-field">
            <label htmlFor="search-status">Status</label>
            <select id="search-status" {...register("status")}>
              <option value="">All statuses</option>
              {Object.entries(statusLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label htmlFor="search-priority">Priority</label>
            <select id="search-priority" {...register("priority")}>
              <option value="">All priorities</option>
              {Object.entries(priorityLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label htmlFor="search-category">Category</label>
            <select id="search-category" {...register("categoryId")}>
              <option value="">All categories</option>
              {categories.data?.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </div>
          {user?.role !== "Customer" && (
            <div className="form-field">
              <label htmlFor="search-agent">Assigned agent</label>
              <select id="search-agent" {...register("agentId")}>
                <option value="">Any agent</option>
                {agents.data?.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
              </select>
            </div>
          )}
          <div className="form-field">
            <label htmlFor="search-from">Created from (UTC)</label>
            <input id="search-from" type="date" {...register("from")} />
          </div>
          <div className="form-field">
            <label htmlFor="search-to">Created through (UTC)</label>
            <input id="search-to" type="date" {...register("to")} />
          </div>
          <div className="form-field">
            <label htmlFor="search-sort">Sort</label>
            <select id="search-sort" {...register("sort")}>
              {Object.entries(sortLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </div>
        </div>
        {(categories.error || agents.error) && (
          <p role="alert" className="field-error">
            Some filter choices could not be loaded.{" "}
            <button
              type="button"
              onClick={() => {
                void categories.refetch();
                if (user?.role !== "Customer") void agents.refetch();
              }}
            >
              Retry filter choices
            </button>
          </p>
        )}
        {validation && (
          <p role="alert" className="error-box">
            {validation}
          </p>
        )}
        <div className="search-form-actions">
          <Button type="submit">Search public content</Button>
          <Link href="/search">Clear filters</Link>
        </div>
      </form>
      <p className="search-explanation">
        Results can take a short time to reflect changes. Only public content is
        searched; internal notes and files are excluded. Results cover up to
        1,000 matching candidates available to your account.
      </p>
      <div
        className="card search-results"
        aria-label="Search results"
        aria-busy={query.isFetching}
      >
        {query.isPending ? (
          <div className="page-state" role="status">
            <span className="spinner" />
            Searching tickets…
          </div>
        ) : query.error ? (
          <div className="page-state" role="alert">
            <h2>
              {query.error instanceof ApiError && query.error.status === 503
                ? "Search is temporarily unavailable."
                : query.error instanceof ApiError && query.error.status === 401
                  ? "Sign in to search your tickets."
                  : "Search could not be completed."}
            </h2>
            <p>{errorMessage(query.error)}</p>
            <div className="search-form-actions">
              <Button variant="outline" onClick={() => void query.refetch()}>
                Retry search
              </Button>
              <Link href="/tickets">Browse tickets</Link>
            </div>
          </div>
        ) : !query.data?.items.length ? (
          <div className="page-state">
            <h2>No tickets match your search.</h2>
            <p>Try another phrase or adjust the filters.</p>
          </div>
        ) : (
          <>
            <div className="search-results-heading">
              {query.data.total} matching{" "}
              {query.data.total === 1 ? "ticket" : "tickets"} · Page {page}
            </div>
            <ol className="search-result-list">
              {query.data.items.map(({ ticket, snippet }) => (
                <li key={ticket.id}>
                  <div className="search-result-top">
                    <span className="ticket-number">{ticket.number}</span>
                    <span className={"status-badge status-" + ticket.status}>
                      {statusLabels[ticket.status]}
                    </span>
                    <span
                      className={"priority-badge priority-" + ticket.priority}
                    >
                      {priorityLabels[ticket.priority]}
                    </span>
                  </div>
                  <h2>
                    <Link href={"/tickets/" + ticket.id}>{ticket.title}</Link>
                  </h2>
                  <SearchSnippet segments={snippet} />
                  <p className="ticket-row-meta">
                    {ticket.category} · {ticket.agentName ?? "Unassigned"} ·
                    Opened {dateLabel(ticket.createdAt)}
                  </p>
                </li>
              ))}
            </ol>
          </>
        )}
        <div className="inbox-footer">
          <span>20 results per page · Maximum 1,000 candidates</span>
          <div className="pagination">
            <Button
              size="small"
              variant="outline"
              aria-label="Previous search page"
              disabled={page <= 1 || query.isFetching}
              onClick={() => navigatePage(page - 1)}
            >
              Previous
            </Button>
            <Button
              size="small"
              variant="outline"
              aria-label="Next search page"
              disabled={
                page >= 50 ||
                page * 20 >= Number(query.data?.total ?? 0) ||
                query.isFetching
              }
              onClick={() => navigatePage(page + 1)}
            >
              Next
            </Button>
          </div>
        </div>
      </div>
    </section>
  );
}
