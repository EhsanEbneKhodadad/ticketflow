"use client";
import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Pencil, Clock, Tag, UserRound } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/features/auth/auth-provider";
import { ApiError, errorMessage } from "@/lib/api";
import {
  ticketsApi,
  statusLabels,
  priorityLabels,
  initials,
  timeLabel,
} from "./api";
import { Conversation } from "./conversation";
import { TicketManagement } from "./ticket-management";
import { TicketForm } from "./ticket-form";

export function TicketDetail({ id }: { id: string }) {
  const { user } = useAuth();
  const query = useQuery({
    queryKey: ["ticket", id, user?.id],
    queryFn: () => ticketsApi.get(id),
    enabled: !!user,
  });
  const [editing, setEditing] = useState(false);
  const [managementError, setManagementError] = useState("");
  if (query.isPending)
    return (
      <div className="page-state" role="status">
        <span className="spinner" />
        Loading ticket…
      </div>
    );
  if (query.error)
    return (
      <div className="card page-state" role="alert">
        <h2>
          {query.error instanceof ApiError && query.error.status === 404
            ? "Ticket not found."
            : "We couldn't load this ticket."}
        </h2>
        <p>
          {query.error instanceof ApiError && query.error.status === 404
            ? "The ticket may not exist, or it isn't available to your account."
            : errorMessage(query.error)}
        </p>
        <Button asChild variant="outline">
          <Link href="/tickets">Back to tickets</Link>
        </Button>
      </div>
    );
  const ticket = query.data;
  if (!ticket) return null;
  const staff = user?.role !== "Customer";
  if (editing)
    return <TicketForm ticket={ticket} onCancel={() => setEditing(false)} />;
  const editable = !["resolved", "closed"].includes(ticket.status);
  return (
    <section>
      <Link href="/tickets" className="back-link">
        <ArrowLeft size={13} />
        All tickets
      </Link>
      <div className="page-heading ticket-detail-heading">
        <div>
          <div className="ticket-number">
            {ticket.number}{" "}
            <span style={{ color: "#a6b497" }}> / {ticket.category}</span>
          </div>
          <h1>{ticket.title}</h1>
          <div className="ticket-heading-badges">
            <span className={"status-badge status-" + ticket.status}>
              {statusLabels[ticket.status]}
            </span>
            <span className={"priority-badge priority-" + ticket.priority}>
              {priorityLabels[ticket.priority]} priority
            </span>
            <span>Opened {timeLabel(ticket.createdAt)}</span>
          </div>
        </div>
        {editable && (
          <Button
            variant="outline"
            size="small"
            onClick={() => setEditing(true)}
          >
            <Pencil size={12} />
            Edit ticket
          </Button>
        )}
      </div>
      <div className="ticket-detail-grid">
        <div className="ticket-main-column">
          <section className="card">
            <div className="detail-card-heading">
              <h2>Original request</h2>
              <span>{ticket.number}</span>
            </div>
            <p className="ticket-description">{ticket.description}</p>
            <div className="ticket-author-line">
              <span
                className="avatar"
                style={{ width: 23, height: 23, fontSize: 8 }}
              >
                {initials(ticket.requesterName)}
              </span>
              Submitted by {ticket.requesterName}
            </div>
          </section>
          <Conversation ticket={ticket} staff={staff} />
        </div>
        <aside className="ticket-sidebar" aria-label="Ticket information">
          <div>
            {managementError && (
              <div
                role="alert"
                className="error-box"
                style={{ marginBottom: 14 }}
              >
                {managementError}
              </div>
            )}
            <TicketManagement
              key={String(ticket.version)}
              ticket={ticket}
              staff={staff}
              onError={setManagementError}
            />
          </div>
          <section className="card side-card">
            <h2>
              <UserRound size={13} />
              People & details
            </h2>
            <dl>
              <div className="side-field">
                <dt>Requested by</dt>
                <dd className="assignee-cell">
                  <span className="avatar">
                    {initials(ticket.requesterName)}
                  </span>
                  {ticket.requesterName}
                </dd>
              </div>
              <div className="side-field">
                <dt>Assigned to</dt>
                <dd>{ticket.agentName ?? "Waiting for an agent"}</dd>
              </div>
              <div className="side-field">
                <dt>Category</dt>
                <dd>{ticket.category}</dd>
              </div>
              <div className="side-field">
                <dt>Tags</dt>
                <dd className="tags-row">
                  {ticket.tags.length ? (
                    ticket.tags.map((tag) => (
                      <span key={tag} className="tag-chip">
                        <Tag
                          size={8}
                          style={{ display: "inline", marginRight: 3 }}
                        />
                        {tag}
                      </span>
                    ))
                  ) : (
                    <span>No tags</span>
                  )}
                </dd>
              </div>
            </dl>
          </section>
          <section className="card side-card" aria-labelledby="activity-title">
            <h2 id="activity-title">
              <Clock size={13} />
              Activity history
            </h2>
            <ol className="activity-list">
              {[...ticket.audit].reverse().map((event) => (
                <li key={event.id}>
                  <strong>{event.actorName}</strong>
                  <div>
                    {event.summary
                      .replaceAll("InProgress", "In progress")
                      .replaceAll("WaitingForCustomer", "Waiting for customer")}
                  </div>
                  <time dateTime={event.createdAt}>
                    {timeLabel(event.createdAt)}
                  </time>
                </li>
              ))}
            </ol>
          </section>
        </aside>
      </div>
    </section>
  );
}
