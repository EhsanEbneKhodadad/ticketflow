"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getApiV1AgentsOptions } from "@ticketflow/api-client";
import { SlidersHorizontal } from "lucide-react";
import { Button } from "@/components/ui/button";
import { sessionQueryKey, type User } from "@/features/auth/auth-provider";
import { ApiError } from "@/lib/api";
import { useErrorMessage } from "@/lib/use-error-message";
import {
  ticketsApi,
  ticketKeys,
  ticketPriorities,
  type Ticket,
  type Status,
  type Priority,
} from "./api";

const transitions: Record<Status, Status[]> = {
  open: ["inProgress", "resolved"],
  inProgress: ["waitingForCustomer", "resolved"],
  waitingForCustomer: ["inProgress", "resolved"],
  resolved: ["closed", "open"],
  closed: ["open"],
};
type Change =
  | { kind: "status"; value: Status }
  | { kind: "priority"; value: Priority }
  | { kind: "assignment"; value: string };

export function TicketManagement({
  ticket,
  staff,
  onError,
}: {
  ticket: Ticket;
  staff: boolean;
  onError: (value: string) => void;
}) {
  const t = useTranslations("Tickets");
  const errorMessage = useErrorMessage();
  const statusText = useTranslations("TicketStatus");
  const priorityText = useTranslations("Priority");
  const client = useQueryClient();
  const principal = client.getQueryData<User>(sessionQueryKey())?.id;
  const [status, setStatus] = useState(ticket.status);
  const [priority, setPriority] = useState(ticket.priority);
  const [agent, setAgent] = useState(ticket.agentId ?? "");
  const agents = useQuery({
    ...getApiV1AgentsOptions({
      headers: principal ? { "X-TicketFlow-User": principal } : undefined,
    }),
    enabled: staff,
  });
  const mutation = useMutation({
    onMutate: () => ({
      principal: client.getQueryData<User>(sessionQueryKey())?.id,
    }),
    mutationFn: (change: Change) => {
      onError("");
      if (change.kind === "status")
        return ticketsApi.status(
          ticket.id,
          {
            status: change.value,
            expectedVersion: ticket.version,
          },
          principal,
        );
      if (change.kind === "priority")
        return ticketsApi.priority(
          ticket.id,
          {
            priority: change.value,
            expectedVersion: ticket.version,
          },
          principal,
        );
      return ticketsApi.assign(
        ticket.id,
        {
          agentId: change.value || null,
          expectedVersion: ticket.version,
        },
        principal,
      );
    },
    onSuccess: (updated, _variables, context) => {
      if (
        context?.principal !== client.getQueryData<User>(sessionQueryKey())?.id
      )
        return;
      client.setQueryData(
        ticketKeys.detail(ticket.id, context?.principal),
        updated,
      );
      void client.invalidateQueries({
        queryKey: ticketKeys.list(context?.principal),
      });
    },
    onError: (error, _variables, context) => {
      onError(errorMessage(error));
      if (error instanceof ApiError && error.status === 409)
        void client.invalidateQueries({
          queryKey: ticketKeys.detail(ticket.id, context?.principal),
        });
    },
  });
  return (
    <section className="card side-card" aria-labelledby="management-title">
      <h2 id="management-title">
        <SlidersHorizontal size={13} />
        {staff ? t("managementStaff") : t("managementCustomer")}
      </h2>
      {staff ? (
        <>
          <form
            className="side-field"
            onSubmit={(e) => {
              e.preventDefault();
              mutation.mutate({ kind: "status", value: status });
            }}
          >
            <label htmlFor="manage-status">{t("status")}</label>
            <div className="side-control">
              <select
                id="manage-status"
                value={status}
                onChange={(e) => setStatus(e.target.value as Status)}
              >
                <option value={ticket.status}>
                  {statusText(ticket.status)}
                </option>
                {transitions[ticket.status].map((value) => (
                  <option key={value} value={value}>
                    {statusText(value)}
                  </option>
                ))}
              </select>
              <Button
                type="submit"
                size="small"
                variant="outline"
                aria-label={t("updateStatus")}
                disabled={mutation.isPending || status === ticket.status}
              >
                {t("save")}
              </Button>
            </div>
          </form>
          <form
            className="side-field"
            onSubmit={(e) => {
              e.preventDefault();
              mutation.mutate({ kind: "priority", value: priority });
            }}
          >
            <label htmlFor="manage-priority">{t("priority")}</label>
            <div className="side-control">
              <select
                id="manage-priority"
                value={priority}
                onChange={(e) => setPriority(e.target.value as Priority)}
              >
                {ticketPriorities.map((value) => (
                  <option key={value} value={value}>
                    {priorityText(value)}
                  </option>
                ))}
              </select>
              <Button
                type="submit"
                size="small"
                variant="outline"
                aria-label={t("updatePriority")}
                disabled={mutation.isPending || priority === ticket.priority}
              >
                {t("save")}
              </Button>
            </div>
          </form>
          <form
            className="side-field"
            onSubmit={(e) => {
              e.preventDefault();
              mutation.mutate({ kind: "assignment", value: agent });
            }}
          >
            <label htmlFor="manage-assignment">{t("assignee")}</label>
            <div className="side-control">
              <select
                id="manage-assignment"
                value={agent}
                onChange={(e) => setAgent(e.target.value)}
                disabled={!agents.data}
              >
                <option value="">{t("unassigned")}</option>
                {agents.data?.map((person) => (
                  <option key={person.id} value={person.id}>
                    {person.name}
                  </option>
                ))}
              </select>
              <Button
                type="submit"
                size="small"
                variant="outline"
                aria-label={t("updateAssignee")}
                disabled={
                  mutation.isPending ||
                  !agents.data ||
                  agent === (ticket.agentId ?? "")
                }
              >
                {t("save")}
              </Button>
            </div>
            {agents.error && (
              <p role="alert">
                {t("agentsError")}{" "}
                <button type="button" onClick={() => void agents.refetch()}>
                  {t("retry")}
                </button>
              </p>
            )}
          </form>
        </>
      ) : (
        <div className="side-field">
          <span className={"status-badge status-" + ticket.status}>
            {statusText(ticket.status)}
          </span>
          {ticket.status === "resolved" && (
            <>
              <p>{t("resolvedNote")}</p>
              <div style={{ display: "flex", gap: 7, marginTop: 12 }}>
                <Button
                  size="small"
                  disabled={mutation.isPending}
                  onClick={() =>
                    mutation.mutate({ kind: "status", value: "closed" })
                  }
                >
                  {t("close")}
                </Button>
                <Button
                  size="small"
                  variant="outline"
                  disabled={mutation.isPending}
                  onClick={() =>
                    mutation.mutate({ kind: "status", value: "open" })
                  }
                >
                  {t("reopen")}
                </Button>
              </div>
            </>
          )}
          {ticket.status === "closed" && (
            <>
              <p>{t("closedNote")}</p>
              <Button
                style={{ marginTop: 12 }}
                size="small"
                variant="outline"
                disabled={mutation.isPending}
                onClick={() =>
                  mutation.mutate({ kind: "status", value: "open" })
                }
              >
                {t("reopen")}
              </Button>
            </>
          )}
          {!["resolved", "closed"].includes(ticket.status) && (
            <p>{t("progressNote")}</p>
          )}
        </div>
      )}
    </section>
  );
}
