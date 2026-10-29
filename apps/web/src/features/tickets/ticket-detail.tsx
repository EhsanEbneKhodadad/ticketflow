"use client";
import { Link } from "@/i18n/navigation";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { getApiV1TicketsByIdOptions } from "@ticketflow/api-client";
import { ArrowLeft, Pencil, Clock, Tag, UserRound } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/features/auth/auth-provider";
import { ApiError } from "@/lib/api";
import { useErrorMessage } from "@/lib/use-error-message";
import { initials, timeLabel, type Ticket } from "./api";
import { Conversation } from "./conversation";
import { TicketManagement } from "./ticket-management";
import { AttachmentPanel } from "@/features/attachments/attachment-panel";
import { TicketForm } from "./ticket-form";

export function TicketDetail({ id }: { id: string }) {
  const locale = useLocale();
  const t = useTranslations("Tickets");
  const errorMessage = useErrorMessage();
  const statusText = useTranslations("TicketStatus");
  const priorityText = useTranslations("Priority");
  const auditSummary = (event: Ticket["audit"][number]) => {
    if (event.kind === "created") return t("auditCreated");
    if (event.kind === "statusChanged") {
      const match =
        /Status changed from (Open|InProgress|WaitingForCustomer|Resolved|Closed) to (Open|InProgress|WaitingForCustomer|Resolved|Closed)/.exec(
          event.summary,
        );
      if (match) {
        const keys = {
          Open: "open",
          InProgress: "inProgress",
          WaitingForCustomer: "waitingForCustomer",
          Resolved: "resolved",
          Closed: "closed",
        } as const;
        return t("auditStatusChanged", {
          from: statusText(keys[match[1] as keyof typeof keys]),
          to: statusText(keys[match[2] as keyof typeof keys]),
        });
      }
    }
    if (event.kind === "priorityChanged") {
      const match = /Priority set to (Low|Medium|High|Urgent)/.exec(
        event.summary,
      );
      if (match) {
        const keys = {
          Low: "low",
          Medium: "medium",
          High: "high",
          Urgent: "urgent",
        } as const;
        return t("auditPriorityChanged", {
          priority: priorityText(keys[match[1] as keyof typeof keys]),
        });
      }
    }
    if (event.kind === "assigned")
      return event.summary.startsWith("Assigned to ")
        ? t("auditAssigned", { name: event.summary.slice(12) })
        : t("auditAssignmentRemoved");
    if (event.kind === "edited") return t("auditEdited");
    if (event.kind === "internalNoteAdded") return t("auditInternalNote");
    if (event.kind === "replyAdded") return t("auditReplyAdded");
    if (event.kind === "attachmentAdded") return t("auditAttachmentAdded");
    return t("auditUpdate");
  };
  const { user } = useAuth();
  const query = useQuery({
    ...getApiV1TicketsByIdOptions({
      path: { id },
      headers: user ? { "X-TicketFlow-User": user.id } : undefined,
    }),
    enabled: !!user,
  });
  const [editing, setEditing] = useState(false);
  const [managementError, setManagementError] = useState("");
  if (query.isPending)
    return (
      <div className="page-state" role="status">
        <span className="spinner" />
        {t("loadingTicket")}
      </div>
    );
  if (query.error)
    return (
      <div className="card page-state" role="alert">
        <h2>
          {query.error instanceof ApiError && query.error.status === 404
            ? t("notFound")
            : t("loadTicketError")}
        </h2>
        <p>
          {query.error instanceof ApiError && query.error.status === 404
            ? t("notVisible")
            : errorMessage(query.error)}
        </p>
        <Button asChild variant="outline">
          <Link href="/tickets">{t("backToTickets")}</Link>
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
        {t("allTickets")}
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
              {statusText(ticket.status)}
            </span>
            <span className={"priority-badge priority-" + ticket.priority}>
              {priorityText(ticket.priority)}
            </span>
            <span>
              {t("opened")} {timeLabel(ticket.createdAt, locale)}
            </span>
          </div>
        </div>
        {editable && (
          <Button
            variant="outline"
            size="small"
            onClick={() => setEditing(true)}
          >
            <Pencil size={12} />
            {t("editTicket")}
          </Button>
        )}
      </div>
      <div className="ticket-detail-grid">
        <div className="ticket-main-column">
          <section className="card">
            <div className="detail-card-heading">
              <h2>{t("originalRequest")}</h2>
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
              {t("submittedBy")} {ticket.requesterName}
            </div>
          </section>
          {user && (
            <section className="card">
              <AttachmentPanel
                ticketId={id}
                version={ticket.version}
                closed={ticket.status === "closed"}
                principalId={user.id}
                staff={staff}
              />
            </section>
          )}
          <Conversation
            ticket={ticket}
            staff={staff}
            renderAttachments={(message) =>
              user && (
                <AttachmentPanel
                  ticketId={id}
                  version={ticket.version}
                  closed={ticket.status === "closed"}
                  principalId={user.id}
                  staff={staff}
                  messageId={message.id}
                  internal={message.internal}
                />
              )
            }
          />
        </div>
        <aside className="ticket-sidebar" aria-label={t("ticketInformation")}>
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
              {t("peopleDetails")}
            </h2>
            <dl>
              <div className="side-field">
                <dt>{t("requestedBy")}</dt>
                <dd className="assignee-cell">
                  <span className="avatar">
                    {initials(ticket.requesterName)}
                  </span>
                  {ticket.requesterName}
                </dd>
              </div>
              <div className="side-field">
                <dt>{t("assignedTo")}</dt>
                <dd>{ticket.agentName ?? t("waitingAgent")}</dd>
              </div>
              <div className="side-field">
                <dt>{t("category")}</dt>
                <dd>{ticket.category}</dd>
              </div>
              <div className="side-field">
                <dt>{t("tags")}</dt>
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
                    <span>{t("noTags")}</span>
                  )}
                </dd>
              </div>
            </dl>
          </section>
          <section className="card side-card" aria-labelledby="activity-title">
            <h2 id="activity-title">
              <Clock size={13} />
              {t("activity")}
            </h2>
            <ol className="activity-list">
              {[...ticket.audit].reverse().map((event) => (
                <li key={event.id}>
                  <strong>{event.actorName}</strong>
                  <div>{auditSummary(event)}</div>
                  <time dateTime={event.createdAt}>
                    {timeLabel(event.createdAt, locale)}
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
