"use client";
import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Search, Plus, Inbox, ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/features/auth/auth-provider";
import { errorMessage } from "@/lib/api";
import {
  ticketsApi,
  statusLabels,
  priorityLabels,
  initials,
  dateLabel,
  type Status,
} from "./api";

export function TicketList({ view = "all" }: { view?: string }) {
  const { user } = useAuth();
  const staff = user?.role !== "Customer";
  const [status, setStatus] = useState("");
  const [priority, setPriority] = useState("");
  const [input, setInput] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const query = useQuery({
    queryKey: ["tickets", user?.id, view, status, priority, search, page],
    enabled: !!user,
    queryFn: () =>
      ticketsApi.list({
        page: String(page),
        pageSize: "10",
        ...(status ? { status } : {}),
        ...(priority ? { priority } : {}),
        ...(search ? { q: search } : {}),
        ...(staff && view === "mine" && user ? { agentId: user.id } : {}),
        ...(staff && view === "unassigned" ? { unassigned: "true" } : {}),
      }),
  });
  const total = Number(query.data?.total ?? 0);
  const title = !staff
    ? "Your tickets"
    : view === "mine"
      ? "Assigned to you"
      : view === "unassigned"
        ? "Unassigned tickets"
        : "Support inbox";
  const tabs: { value: string; label: string }[] = [
    { value: "", label: "All tickets" },
    { value: "open", label: "Open" },
    { value: "inProgress", label: "In progress" },
    { value: "resolved", label: "Resolved" },
  ];
  return (
    <section>
      <div className="page-heading">
        <div>
          <div className="page-eyebrow">
            {staff ? "EVERY CONVERSATION, IN ONE PLACE" : "WE'RE HERE TO HELP"}
          </div>
          <h1>{title}</h1>
          <p>
            {staff
              ? "Pick up a request. Start a conversation. Make someone's day a little easier."
              : "Follow your requests and keep the conversation moving."}
          </p>
        </div>
        <Button asChild>
          <Link href="/tickets/new">
            <Plus size={15} />
            New ticket
          </Link>
        </Button>
      </div>
      <div className="card">
        <div
          className="inbox-tabs"
          role="group"
          aria-label="Ticket status filters"
        >
          {tabs.map((tab) => (
            <button
              key={tab.value}
              aria-pressed={status === tab.value}
              onClick={() => {
                setStatus(tab.value);
                setPage(1);
              }}
            >
              {tab.label}
              {status === tab.value && query.data && (
                <span className="count-pill">{total}</span>
              )}
            </button>
          ))}
        </div>
        <div className="inbox-toolbar">
          <form
            className="search-field"
            onSubmit={(e) => {
              e.preventDefault();
              setSearch(input.trim());
              setPage(1);
            }}
          >
            <Search size={15} />
            <input
              aria-label="Search tickets"
              placeholder="Search by title or ticket number…"
              value={input}
              onChange={(e) => setInput(e.target.value)}
            />
            <Button size="small" variant="outline" type="submit">
              Search
            </Button>
          </form>
          <div className="toolbar-filters">
            <select
              className="filter-control"
              aria-label="Filter status"
              value={status}
              onChange={(e) => {
                setStatus(e.target.value);
                setPage(1);
              }}
            >
              <option value="">All statuses</option>
              {Object.entries(statusLabels).map(([key, label]) => (
                <option key={key} value={key}>
                  {label}
                </option>
              ))}
            </select>
            <select
              className="filter-control"
              aria-label="Filter priority"
              value={priority}
              onChange={(e) => {
                setPriority(e.target.value);
                setPage(1);
              }}
            >
              <option value="">All priorities</option>
              {Object.entries(priorityLabels).map(([key, label]) => (
                <option key={key} value={key}>
                  {label}
                </option>
              ))}
            </select>
          </div>
        </div>
        {query.isPending ? (
          <div className="page-state" role="status">
            <span className="spinner" />
            Loading tickets…
          </div>
        ) : query.error ? (
          <div className="page-state" role="alert">
            <h2>Tickets could not be loaded.</h2>
            <p>{errorMessage(query.error)}</p>
            <Button variant="outline" onClick={() => void query.refetch()}>
              Try again
            </Button>
          </div>
        ) : !query.data?.items.length ? (
          <div className="page-state">
            <Inbox size={32} strokeWidth={1} />
            <h2>
              {search || status || priority || view !== "all"
                ? "No tickets match this view."
                : "A fresh start for your support."}
            </h2>
            <p>
              {search || status || priority
                ? "Try another search or adjust the filters."
                : "Create a ticket when you need a hand. We'll keep every update here."}
            </p>
            <Button asChild variant="outline">
              <Link href="/tickets/new">Create a ticket</Link>
            </Button>
          </div>
        ) : (
          <div className="table-scroll">
            <table className="ticket-table">
              <caption className="sr-only">{title}</caption>
              <thead>
                <tr>
                  <th scope="col">TICKET</th>
                  <th scope="col">STATUS</th>
                  <th scope="col">PRIORITY</th>
                  <th scope="col" className="column-assignee">
                    ASSIGNEE
                  </th>
                  <th scope="col" className="column-date">
                    OPENED
                  </th>
                </tr>
              </thead>
              <tbody>
                {query.data.items.map((ticket) => (
                  <tr key={ticket.id}>
                    <td>
                      <Link
                        className="ticket-title-link"
                        href={"/tickets/" + ticket.id}
                      >
                        {ticket.title}
                      </Link>
                      <div className="ticket-row-meta">
                        <span className="ticket-number">{ticket.number}</span>
                        <span>·</span>
                        <span>
                          {staff ? ticket.requesterName : ticket.category}
                        </span>
                      </div>
                    </td>
                    <td>
                      <span className={"status-badge status-" + ticket.status}>
                        {statusLabels[ticket.status as Status]}
                      </span>
                    </td>
                    <td>
                      <span
                        className={"priority-badge priority-" + ticket.priority}
                      >
                        {priorityLabels[ticket.priority]}
                      </span>
                    </td>
                    <td className="column-assignee">
                      {ticket.agentName ? (
                        <div className="assignee-cell">
                          <span className="avatar">
                            {initials(ticket.agentName)}
                          </span>
                          {ticket.agentName}
                        </div>
                      ) : (
                        <span className="unassigned">Unassigned</span>
                      )}
                    </td>
                    <td className="column-date">
                      {dateLabel(ticket.createdAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <div className="inbox-footer">
          <span>
            {total} matching {total === 1 ? "ticket" : "tickets"}
            {total > 0 ? " · Page " + page : ""}
          </span>
          <div className="pagination">
            <Button
              size="small"
              variant="outline"
              aria-label="Previous page"
              disabled={page === 1 || query.isFetching}
              onClick={() => setPage((p) => p - 1)}
            >
              <ChevronLeft size={13} />
            </Button>
            <Button
              size="small"
              variant="outline"
              aria-label="Next page"
              disabled={page * 10 >= total || query.isFetching}
              onClick={() => setPage((p) => p + 1)}
            >
              <ChevronRight size={13} />
            </Button>
          </div>
        </div>
      </div>
    </section>
  );
}
