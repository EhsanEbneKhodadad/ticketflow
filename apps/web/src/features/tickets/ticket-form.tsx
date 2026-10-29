"use client";
import { Link } from "@/i18n/navigation";
import { useState } from "react";
import { useRouter } from "@/i18n/navigation";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, ArrowRight, MessageSquare } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { User } from "@/features/auth/auth-provider";
import { ApiError } from "@/lib/api";
import { useErrorMessage } from "@/lib/use-error-message";
import { ticketsApi, ticketPriorities, type Ticket } from "./api";

export function TicketForm({
  ticket,
  onCancel,
}: {
  ticket?: Ticket;
  onCancel?: () => void;
}) {
  const t = useTranslations("Tickets");
  const errorMessage = useErrorMessage();
  const priorityText = useTranslations("Priority");
  const schema = z.object({
    title: z
      .string()
      .trim()
      .min(5, t("errorTitle"))
      .max(160, t("errorTitleMax")),
    description: z.string().trim().min(10, t("errorDescription")).max(10000),
    categoryId: z.string().min(1, t("errorCategory")),
    tags: z.string().max(150),
    priority: z.enum(["low", "medium", "high", "urgent"]),
  });
  const [baseVersion] = useState(ticket?.version);
  const router = useRouter();
  const client = useQueryClient();
  const categories = useQuery({
    queryKey: ["categories"],
    queryFn: ticketsApi.categories,
  });
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: {
      title: ticket?.title ?? "",
      description: ticket?.description ?? "",
      categoryId: ticket?.categoryId ?? "",
      tags: ticket?.tags.join(", ") ?? "",
      priority: ticket?.priority ?? "medium",
    },
  });
  const mutation = useMutation({
    onMutate: () => ({ principal: client.getQueryData<User>(["session"])?.id }),
    mutationFn: (values: z.infer<typeof schema>) => {
      const input = {
        title: values.title,
        description: values.description,
        categoryId: values.categoryId,
        tags: values.tags
          .split(",")
          .map((t) => t.trim())
          .filter(Boolean),
      };
      return ticket
        ? ticketsApi.edit(ticket.id, {
            ...input,
            expectedVersion: baseVersion!,
          })
        : ticketsApi.create({ ...input, priority: values.priority });
    },
    onSuccess: (saved, _variables, context) => {
      if (context?.principal !== client.getQueryData<User>(["session"])?.id)
        return;
      client.setQueryData(["ticket", saved.id, context?.principal], saved);
      void client.invalidateQueries({ queryKey: ["tickets"] });
      if (onCancel) onCancel();
      else router.push("/tickets/" + saved.id);
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409 && ticket)
        void client.invalidateQueries({ queryKey: ["ticket", ticket.id] });
    },
  });
  return (
    <section>
      {onCancel ? (
        <button className="back-link" onClick={onCancel}>
          <ArrowLeft size={13} />
          {t("backToTicket")}
        </button>
      ) : (
        <Link className="back-link" href="/tickets">
          <ArrowLeft size={13} />
          {t("allTickets")}
        </Link>
      )}
      <div className="page-heading">
        <div>
          <div className="page-eyebrow">
            {ticket ? ticket.number : t("formKicker")}
          </div>
          <h1>{ticket ? t("editTitle") : t("newTitle")}</h1>
          <p>{ticket ? t("editDescription") : t("newDescription")}</p>
        </div>
      </div>
      <div className="card ticket-form-card">
        <div className="form-intro">
          <span className="avatar">
            <MessageSquare size={17} />
          </span>
          <div>
            <strong>{ticket ? t("keepUpdated") : t("newConversation")}</strong>
            <p>{t("privacyHint")}</p>
          </div>
        </div>
        <form
          className="ticket-form"
          onSubmit={handleSubmit((values) => mutation.mutate(values))}
          noValidate
        >
          <div className="form-field">
            <label htmlFor="ticket-title">{t("subject")}</label>
            <input
              id="ticket-title"
              placeholder={t("subjectPlaceholder")}
              {...register("title")}
              aria-invalid={!!errors.title}
              aria-describedby={errors.title ? "title-error" : undefined}
            />
            {errors.title && (
              <p className="field-error" id="title-error">
                {errors.title.message}
              </p>
            )}
          </div>
          <div className="form-field">
            <label htmlFor="ticket-description">{t("whatHappened")}</label>
            <textarea
              id="ticket-description"
              rows={6}
              placeholder={t("descriptionPlaceholder")}
              {...register("description")}
              aria-invalid={!!errors.description}
              aria-describedby={
                errors.description ? "description-error" : undefined
              }
            />
            {errors.description && (
              <p className="field-error" id="description-error">
                {errors.description.message}
              </p>
            )}
          </div>
          <div className="form-columns">
            <div className="form-field">
              <label htmlFor="ticket-category">{t("category")}</label>
              <select
                id="ticket-category"
                {...register("categoryId")}
                disabled={!categories.data}
                aria-invalid={!!errors.categoryId}
              >
                <option value="">
                  {categories.isPending
                    ? t("loadingCategories")
                    : t("chooseCategory")}
                </option>
                {categories.data?.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </select>
              {errors.categoryId && (
                <p className="field-error">{errors.categoryId.message}</p>
              )}
              {categories.error && (
                <div className="field-error" role="alert">
                  {t("categoriesError")}{" "}
                  <button
                    type="button"
                    onClick={() => void categories.refetch()}
                  >
                    {t("retry")}
                  </button>
                </div>
              )}
            </div>
            {!ticket && (
              <div className="form-field">
                <label htmlFor="ticket-priority">{t("priority")}</label>
                <select id="ticket-priority" {...register("priority")}>
                  {ticketPriorities.map((value) => (
                    <option key={value} value={value}>
                      {priorityText(value)}
                    </option>
                  ))}
                </select>
                <p className="field-hint">{t("urgencyHint")}</p>
              </div>
            )}
          </div>
          <div className="form-field">
            <label htmlFor="ticket-tags">
              {t("tags")} <span className="field-hint">{t("optional")}</span>
            </label>
            <input
              id="ticket-tags"
              placeholder={t("tagPlaceholder")}
              {...register("tags")}
            />
            <p className="field-hint">{t("tagHint")}</p>
          </div>
          {mutation.error && (
            <div role="alert" className="error-box">
              {errorMessage(mutation.error)}
              {mutation.error instanceof ApiError &&
                mutation.error.status === 409 &&
                " " + t("conflict")}
            </div>
          )}
          <div className="form-actions">
            {onCancel ? (
              <Button type="button" variant="outline" onClick={onCancel}>
                {t("cancel")}
              </Button>
            ) : (
              <Button asChild variant="outline">
                <Link href="/tickets">{t("cancel")}</Link>
              </Button>
            )}
            <Button
              type="submit"
              disabled={mutation.isPending || !categories.data}
            >
              {mutation.isPending
                ? t("saving")
                : ticket
                  ? t("saveChanges")
                  : t("createTicket")}
              <ArrowRight size={14} />
            </Button>
          </div>
        </form>
      </div>
    </section>
  );
}
