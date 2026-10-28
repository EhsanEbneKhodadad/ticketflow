"use client";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { LockKeyhole, Send, MessageSquare } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { User } from "@/features/auth/auth-provider";
import { ApiError, errorMessage } from "@/lib/api";
import { ticketsApi, initials, timeLabel, type Ticket } from "./api";

export function Conversation({
  ticket,
  staff,
}: {
  ticket: Ticket;
  staff: boolean;
}) {
  const client = useQueryClient();
  const [internal, setInternal] = useState(false);
  const [notice, setNotice] = useState("");
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm({ defaultValues: { body: "" } });
  const mutation = useMutation({
    onMutate: () => ({ principal: client.getQueryData<User>(["session"])?.id }),
    mutationFn: (input: { body: string }) =>
      ticketsApi.reply(ticket.id, {
        body: input.body,
        internal,
        expectedVersion: ticket.version,
      }),
    onSuccess: (updated, _variables, context) => {
      if (context?.principal !== client.getQueryData<User>(["session"])?.id)
        return;
      client.setQueryData(["ticket", ticket.id, context?.principal], updated);
      void client.invalidateQueries({ queryKey: ["tickets"] });
      reset({ body: "" });
      setNotice(internal ? "Internal note added." : "Reply sent.");
    },
    onError: (error) => {
      setNotice("");
      if (error instanceof ApiError && error.status === 409)
        void client.invalidateQueries({ queryKey: ["ticket", ticket.id] });
    },
  });
  const messages = ticket.messages.filter(
    (message) => staff || !message.internal,
  );
  return (
    <section className="card" aria-labelledby="conversation-title">
      <div className="detail-card-heading">
        <h2 id="conversation-title">
          Conversation <span className="count-pill">{messages.length}</span>
        </h2>
        <MessageSquare size={14} color="#9aac87" />
      </div>
      {messages.length ? (
        <div className="conversation-messages" aria-live="polite">
          {messages.map((message) => (
            <article
              key={message.id}
              className={
                "conversation-message" +
                (message.internal ? " internal-message" : "")
              }
            >
              {message.internal && (
                <div className="internal-note-label">
                  <LockKeyhole size={12} />
                  Internal note · Team only
                </div>
              )}
              <div className="message-top">
                <span className="avatar">{initials(message.authorName)}</span>
                <div>
                  <strong>{message.authorName}</strong>
                  <small>
                    {message.authorRole === "Customer"
                      ? "Customer"
                      : "Support team"}
                  </small>
                </div>
                <time dateTime={message.createdAt}>
                  {timeLabel(message.createdAt)}
                </time>
              </div>
              <p className="message-body">{message.body}</p>
            </article>
          ))}
        </div>
      ) : (
        <div className="page-state" style={{ padding: "32px 22px" }}>
          <p>No replies yet. Add a message to keep the conversation moving.</p>
        </div>
      )}
      <form
        className="conversation-form"
        onSubmit={handleSubmit((input) => mutation.mutate(input))}
        noValidate
      >
        {staff && (
          <div
            className="reply-tabs"
            role="group"
            aria-label="Reply visibility"
          >
            <button
              type="button"
              aria-pressed={!internal}
              disabled={mutation.isPending}
              onClick={() => {
                setInternal(false);
                setNotice("");
              }}
            >
              Public reply
            </button>
            <button
              type="button"
              aria-pressed={internal}
              disabled={mutation.isPending}
              onClick={() => {
                setInternal(true);
                setNotice("");
              }}
            >
              Internal note
            </button>
          </div>
        )}
        {ticket.status === "closed" && (
          <p className="field-hint" style={{ marginBottom: 15 }}>
            This ticket is closed. Reopen it to continue the conversation.
          </p>
        )}
        <div className="form-field">
          <label htmlFor="reply-body">Message</label>
          <textarea
            id="reply-body"
            rows={4}
            disabled={ticket.status === "closed"}
            placeholder={
              internal
                ? "Share context with your support team…"
                : "Add your reply to the conversation…"
            }
            {...register("body", {
              validate: (value) =>
                value.trim().length > 0 || "Write a message before sending.",
              maxLength: {
                value: 10000,
                message: "Keep your message under 10,000 characters.",
              },
            })}
            aria-invalid={!!errors.body}
            aria-describedby={errors.body ? "reply-error" : undefined}
          />
          {errors.body && (
            <p id="reply-error" className="field-error">
              {errors.body.message}
            </p>
          )}
        </div>
        {mutation.error && (
          <div className="error-box" role="alert" style={{ marginTop: 14 }}>
            {errorMessage(mutation.error)}
            {mutation.error instanceof ApiError &&
              mutation.error.status === 409 &&
              " Your draft is preserved."}
          </div>
        )}
        {notice && (
          <p role="status" className="inline-success">
            {notice}
          </p>
        )}
        <div className="reply-bottom">
          <span>
            {internal
              ? "Only agents and administrators can see this note."
              : "Your reply is visible to the customer and support team."}
          </span>
          <Button
            type="submit"
            disabled={mutation.isPending || ticket.status === "closed"}
          >
            {mutation.isPending
              ? "Sending…"
              : internal
                ? "Add internal note"
                : "Send reply"}
            <Send size={13} />
          </Button>
        </div>
      </form>
    </section>
  );
}
