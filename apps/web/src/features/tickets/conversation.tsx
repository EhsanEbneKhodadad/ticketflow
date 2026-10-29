"use client";
import { useState, type ReactNode } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { LockKeyhole, Send, MessageSquare } from "lucide-react";
import { Button } from "@/components/ui/button";
import { sessionQueryKey, type User } from "@/features/auth/auth-provider";
import { ApiError } from "@/lib/api";
import { useErrorMessage } from "@/lib/use-error-message";
import {
  ticketsApi,
  ticketKeys,
  initials,
  timeLabel,
  type Ticket,
} from "./api";

export function Conversation({
  ticket,
  staff,
  renderAttachments,
}: {
  ticket: Ticket;
  staff: boolean;
  renderAttachments?: (message: Ticket["messages"][number]) => ReactNode;
}) {
  const locale = useLocale();
  const t = useTranslations("Tickets");
  const errorMessage = useErrorMessage();
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
    onMutate: () => ({
      principal: client.getQueryData<User>(sessionQueryKey())?.id,
    }),
    mutationFn: (input: { body: string }) =>
      ticketsApi.reply(
        ticket.id,
        {
          body: input.body,
          internal,
          expectedVersion: ticket.version,
        },
        client.getQueryData<User>(sessionQueryKey())?.id,
      ),
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
      reset({ body: "" });
      setNotice(internal ? t("internalAdded") : t("replySent"));
    },
    onError: (error, _variables, context) => {
      setNotice("");
      if (error instanceof ApiError && error.status === 409)
        void client.invalidateQueries({
          queryKey: ticketKeys.detail(ticket.id, context?.principal),
        });
    },
  });
  const messages = ticket.messages.filter(
    (message) => staff || !message.internal,
  );
  return (
    <section className="card" aria-labelledby="conversation-title">
      <div className="detail-card-heading">
        <h2 id="conversation-title">
          {t("conversation")}{" "}
          <span className="count-pill">
            {new Intl.NumberFormat(locale).format(messages.length)}
          </span>
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
                  {t("teamOnly")}
                </div>
              )}
              <div className="message-top">
                <span className="avatar">{initials(message.authorName)}</span>
                <div>
                  <strong>{message.authorName}</strong>
                  <small>
                    {message.authorRole === "Customer"
                      ? t("customerRole")
                      : t("supportTeam")}
                  </small>
                </div>
                <time dateTime={message.createdAt}>
                  {timeLabel(message.createdAt, locale)}
                </time>
              </div>
              <p className="message-body">{message.body}</p>
              {renderAttachments?.(message)}
            </article>
          ))}
        </div>
      ) : (
        <div className="page-state" style={{ padding: "32px 22px" }}>
          <p>{t("noReplies")}</p>
        </div>
      )}
      <form
        className="conversation-form"
        onSubmit={handleSubmit((input) => mutation.mutate(input))}
        noValidate
      >
        {staff && (
          <div className="reply-tabs" role="group" aria-label={t("visibility")}>
            <button
              type="button"
              aria-pressed={!internal}
              disabled={mutation.isPending}
              onClick={() => {
                setInternal(false);
                setNotice("");
              }}
            >
              {t("publicReply")}
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
              {t("internalNote")}
            </button>
          </div>
        )}
        {ticket.status === "closed" && (
          <p className="field-hint" style={{ marginBottom: 15 }}>
            {t("closedHint")}
          </p>
        )}
        <div className="form-field">
          <label htmlFor="reply-body">{t("message")}</label>
          <textarea
            id="reply-body"
            rows={4}
            disabled={ticket.status === "closed"}
            placeholder={
              internal ? t("internalPlaceholder") : t("replyPlaceholder")
            }
            {...register("body", {
              validate: (value) => value.trim().length > 0 || t("writeMessage"),
              maxLength: {
                value: 10000,
                message: t("messageMax"),
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
              " " + t("draftPreserved")}
          </div>
        )}
        {notice && (
          <p role="status" className="inline-success">
            {notice}
          </p>
        )}
        <div className="reply-bottom">
          <span>{internal ? t("internalAudience") : t("publicAudience")}</span>
          <Button
            type="submit"
            disabled={mutation.isPending || ticket.status === "closed"}
          >
            {mutation.isPending
              ? t("sending")
              : internal
                ? t("addInternal")
                : t("sendReply")}
            <Send size={13} />
          </Button>
        </div>
      </form>
    </section>
  );
}
