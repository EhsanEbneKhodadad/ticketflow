"use client";
import { Link } from "@/i18n/navigation";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Search, Plus, Inbox, ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/features/auth/auth-provider";
import { useErrorMessage } from "@/lib/use-error-message";
import {
  ticketsApi,
  ticketStatuses,
  ticketPriorities,
  initials,
  dateLabel,
} from "./api";

export function TicketList({ view = "all" }: { view?: string }) {
  const locale = useLocale();
  const t = useTranslations("Tickets");
  const errorMessage = useErrorMessage();
  const statusText = useTranslations("TicketStatus");
  const priorityText = useTranslations("Priority");
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
    ? t("ownTitle")
    : view === "mine"
      ? t("assignedTitle")
      : view === "unassigned"
        ? t("unassignedTitle")
        : t("inboxTitle");
  const tabs: { value: string; label: string }[] = [
    { value: "", label: t("allStatuses") },
    { value: "open", label: statusText("open") },
    { value: "inProgress", label: statusText("inProgress") },
    { value: "resolved", label: statusText("resolved") },
  ];
  return (
    <section>
      <div className="page-heading">
        <div>
          <div className="page-eyebrow">
            {staff ? t("staffEyebrow") : t("customerEyebrow")}
          </div>
          <h1>{title}</h1>
          <p>{staff ? t("staffDescription") : t("customerDescription")}</p>
        </div>
        <Button asChild>
          <Link href="/tickets/new">
            <Plus size={15} />
            {t("newTicket")}
          </Link>
        </Button>
      </div>
      <div className="card">
        <div className="inbox-tabs" role="group" aria-label={t("filterGroup")}>
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
              aria-label={t("searchLabel")}
              placeholder={t("searchPlaceholder")}
              value={input}
              onChange={(e) => setInput(e.target.value)}
            />
            <Button size="small" variant="outline" type="submit">
              {t("searchButton")}
            </Button>
          </form>
          <div className="toolbar-filters">
            <select
              className="filter-control"
              aria-label={t("filterStatus")}
              value={status}
              onChange={(e) => {
                setStatus(e.target.value);
                setPage(1);
              }}
            >
              <option value="">{t("allStatuses")}</option>
              {ticketStatuses.map((key) => (
                <option key={key} value={key}>
                  {statusText(key)}
                </option>
              ))}
            </select>
            <select
              className="filter-control"
              aria-label={t("filterPriority")}
              value={priority}
              onChange={(e) => {
                setPriority(e.target.value);
                setPage(1);
              }}
            >
              <option value="">{t("allPriorities")}</option>
              {ticketPriorities.map((key) => (
                <option key={key} value={key}>
                  {priorityText(key)}
                </option>
              ))}
            </select>
          </div>
        </div>
        {query.isPending ? (
          <div className="page-state" role="status">
            <span className="spinner" />
            {t("loading")}
          </div>
        ) : query.error ? (
          <div className="page-state" role="alert">
            <h2>{t("loadError")}</h2>
            <p>{errorMessage(query.error)}</p>
            <Button variant="outline" onClick={() => void query.refetch()}>
              {t("retry")}
            </Button>
          </div>
        ) : !query.data?.items.length ? (
          <div className="page-state">
            <Inbox size={32} strokeWidth={1} />
            <h2>
              {search || status || priority || view !== "all"
                ? t("noMatches")
                : t("noTickets")}
            </h2>
            <p>
              {search || status || priority ? t("tryFilters") : t("startHint")}
            </p>
            <Button asChild variant="outline">
              <Link href="/tickets/new">{t("create")}</Link>
            </Button>
          </div>
        ) : (
          <div className="table-scroll">
            <table className="ticket-table">
              <caption className="sr-only">{title}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("tableTicket")}</th>
                  <th scope="col">{t("tableStatus")}</th>
                  <th scope="col">{t("tablePriority")}</th>
                  <th scope="col" className="column-assignee">
                    {t("tableAssignee")}
                  </th>
                  <th scope="col" className="column-date">
                    {t("tableOpened")}
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
                        {statusText(ticket.status)}
                      </span>
                    </td>
                    <td>
                      <span
                        className={"priority-badge priority-" + ticket.priority}
                      >
                        {priorityText(ticket.priority)}
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
                        <span className="unassigned">{t("unassigned")}</span>
                      )}
                    </td>
                    <td className="column-date">
                      {dateLabel(ticket.createdAt, locale)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <div className="inbox-footer">
          <span>
            {new Intl.NumberFormat(locale).format(total)} {t("matching")}{" "}
            {total === 1 ? t("ticket") : t("tickets")}
            {total > 0
              ? " · " +
                t("page") +
                " " +
                new Intl.NumberFormat(locale).format(page)
              : ""}
          </span>
          <div className="pagination">
            <Button
              size="small"
              variant="outline"
              aria-label={t("previous")}
              disabled={page === 1 || query.isFetching}
              onClick={() => setPage((p) => p - 1)}
            >
              <ChevronLeft size={13} />
            </Button>
            <Button
              size="small"
              variant="outline"
              aria-label={t("next")}
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
