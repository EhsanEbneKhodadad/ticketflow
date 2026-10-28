"use client";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { SlidersHorizontal } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { User } from "@/features/auth/auth-provider";
import { ApiError, errorMessage } from "@/lib/api";
import {
  ticketsApi,
  statusLabels,
  priorityLabels,
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
  const client = useQueryClient();
  const [status, setStatus] = useState(ticket.status);
  const [priority, setPriority] = useState(ticket.priority);
  const [agent, setAgent] = useState(ticket.agentId ?? "");
  const agents = useQuery({
    queryKey: ["agents", client.getQueryData<User>(["session"])?.id],
    queryFn: ticketsApi.agents,
    enabled: staff,
  });
  const mutation = useMutation({
    onMutate: () => ({ principal: client.getQueryData<User>(["session"])?.id }),
    mutationFn: (change: Change) => {
      onError("");
      if (change.kind === "status")
        return ticketsApi.status(ticket.id, {
          status: change.value,
          expectedVersion: ticket.version,
        });
      if (change.kind === "priority")
        return ticketsApi.priority(ticket.id, {
          priority: change.value,
          expectedVersion: ticket.version,
        });
      return ticketsApi.assign(ticket.id, {
        agentId: change.value || null,
        expectedVersion: ticket.version,
      });
    },
    onSuccess: (updated, _variables, context) => {
      if (context?.principal !== client.getQueryData<User>(["session"])?.id)
        return;
      client.setQueryData(["ticket", ticket.id, context?.principal], updated);
      void client.invalidateQueries({ queryKey: ["tickets"] });
    },
    onError: (error) => {
      onError(errorMessage(error));
      if (error instanceof ApiError && error.status === 409)
        void client.invalidateQueries({ queryKey: ["ticket", ticket.id] });
    },
  });
  return (
    <section className="card side-card" aria-labelledby="management-title">
      <h2 id="management-title">
        <SlidersHorizontal size={13} />
        {staff ? "Ticket management" : "Ticket status"}
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
            <label htmlFor="manage-status">Status</label>
            <div className="side-control">
              <select
                id="manage-status"
                value={status}
                onChange={(e) => setStatus(e.target.value as Status)}
              >
                <option value={ticket.status}>
                  {statusLabels[ticket.status]}
                </option>
                {transitions[ticket.status].map((value) => (
                  <option key={value} value={value}>
                    {statusLabels[value]}
                  </option>
                ))}
              </select>
              <Button
                type="submit"
                size="small"
                variant="outline"
                aria-label="Update status"
                disabled={mutation.isPending || status === ticket.status}
              >
                Save
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
            <label htmlFor="manage-priority">Priority</label>
            <div className="side-control">
              <select
                id="manage-priority"
                value={priority}
                onChange={(e) => setPriority(e.target.value as Priority)}
              >
                {Object.entries(priorityLabels).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
              <Button
                type="submit"
                size="small"
                variant="outline"
                aria-label="Update priority"
                disabled={mutation.isPending || priority === ticket.priority}
              >
                Save
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
            <label htmlFor="manage-assignment">Assignee</label>
            <div className="side-control">
              <select
                id="manage-assignment"
                value={agent}
                onChange={(e) => setAgent(e.target.value)}
                disabled={!agents.data}
              >
                <option value="">Unassigned</option>
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
                aria-label="Update assignee"
                disabled={
                  mutation.isPending ||
                  !agents.data ||
                  agent === (ticket.agentId ?? "")
                }
              >
                Save
              </Button>
            </div>
            {agents.error && (
              <p role="alert">
                Agents could not be loaded.{" "}
                <button type="button" onClick={() => void agents.refetch()}>
                  Try again
                </button>
              </p>
            )}
          </form>
        </>
      ) : (
        <div className="side-field">
          <span className={"status-badge status-" + ticket.status}>
            {statusLabels[ticket.status]}
          </span>
          {ticket.status === "resolved" && (
            <>
              <p>
                All sorted? Close the ticket, or reopen it if you need another
                hand.
              </p>
              <div style={{ display: "flex", gap: 7, marginTop: 12 }}>
                <Button
                  size="small"
                  disabled={mutation.isPending}
                  onClick={() =>
                    mutation.mutate({ kind: "status", value: "closed" })
                  }
                >
                  Close ticket
                </Button>
                <Button
                  size="small"
                  variant="outline"
                  disabled={mutation.isPending}
                  onClick={() =>
                    mutation.mutate({ kind: "status", value: "open" })
                  }
                >
                  Reopen ticket
                </Button>
              </div>
            </>
          )}
          {ticket.status === "closed" && (
            <>
              <p>Need more help with this request? Reopen the conversation.</p>
              <Button
                style={{ marginTop: 12 }}
                size="small"
                variant="outline"
                disabled={mutation.isPending}
                onClick={() =>
                  mutation.mutate({ kind: "status", value: "open" })
                }
              >
                Reopen ticket
              </Button>
            </>
          )}
          {!["resolved", "closed"].includes(ticket.status) && (
            <p>Your support team will update the status as work progresses.</p>
          )}
        </div>
      )}
    </section>
  );
}
