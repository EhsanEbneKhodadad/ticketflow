"use client";
import Link from "next/link";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, ArrowRight, MessageSquare } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { User } from "@/features/auth/auth-provider";
import { ApiError, errorMessage } from "@/lib/api";
import { ticketsApi, priorityLabels, type Ticket } from "./api";

const schema = z.object({
  title: z
    .string()
    .trim()
    .min(5, "Use at least 5 characters.")
    .max(160, "Keep the title under 160 characters."),
  description: z
    .string()
    .trim()
    .min(10, "Add a little more detail (at least 10 characters).")
    .max(10000),
  categoryId: z.string().min(1, "Choose a category."),
  tags: z.string().max(150),
  priority: z.enum(["low", "medium", "high", "urgent"]),
});

export function TicketForm({
  ticket,
  onCancel,
}: {
  ticket?: Ticket;
  onCancel?: () => void;
}) {
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
          Back to ticket
        </button>
      ) : (
        <Link className="back-link" href="/tickets">
          <ArrowLeft size={13} />
          All tickets
        </Link>
      )}
      <div className="page-heading">
        <div>
          <div className="page-eyebrow">
            {ticket ? ticket.number : "LET'S GET THIS SORTED"}
          </div>
          <h1>{ticket ? "Edit ticket" : "How can we help?"}</h1>
          <p>
            {ticket
              ? "Update the details so everyone has the right context."
              : "Tell us what's happening. The right details help us find the right answer."}
          </p>
        </div>
      </div>
      <div className="card ticket-form-card">
        <div className="form-intro">
          <span className="avatar">
            <MessageSquare size={17} />
          </span>
          <div>
            <strong>
              {ticket
                ? "Keep your request up to date"
                : "A new conversation starts here"}
            </strong>
            <p>
              Be specific, share context, and leave out passwords or private
              credentials.
            </p>
          </div>
        </div>
        <form
          className="ticket-form"
          onSubmit={handleSubmit((values) => mutation.mutate(values))}
          noValidate
        >
          <div className="form-field">
            <label htmlFor="ticket-title">Subject</label>
            <input
              id="ticket-title"
              placeholder="A short summary of the issue"
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
            <label htmlFor="ticket-description">What happened?</label>
            <textarea
              id="ticket-description"
              rows={6}
              placeholder="Describe the issue, what you expected, and anything you've already tried…"
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
              <label htmlFor="ticket-category">Category</label>
              <select
                id="ticket-category"
                {...register("categoryId")}
                disabled={!categories.data}
                aria-invalid={!!errors.categoryId}
              >
                <option value="">
                  {categories.isPending
                    ? "Loading categories…"
                    : "Choose a category"}
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
                  Categories could not be loaded.{" "}
                  <button
                    type="button"
                    onClick={() => void categories.refetch()}
                  >
                    Try again
                  </button>
                </div>
              )}
            </div>
            {!ticket && (
              <div className="form-field">
                <label htmlFor="ticket-priority">Priority</label>
                <select id="ticket-priority" {...register("priority")}>
                  {Object.entries(priorityLabels).map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
                <p className="field-hint">
                  Choose the urgency that best fits your request.
                </p>
              </div>
            )}
          </div>
          <div className="form-field">
            <label htmlFor="ticket-tags">
              Tags <span className="field-hint">(optional)</span>
            </label>
            <input
              id="ticket-tags"
              placeholder="e.g. account, login"
              {...register("tags")}
            />
            <p className="field-hint">
              Up to 5 tags, separated by commas. Letters, numbers, and hyphens
              only.
            </p>
          </div>
          {mutation.error && (
            <div role="alert" className="error-box">
              {errorMessage(mutation.error)}
              {mutation.error instanceof ApiError &&
                mutation.error.status === 409 &&
                " Your draft is preserved. Review the latest ticket before trying again."}
            </div>
          )}
          <div className="form-actions">
            {onCancel ? (
              <Button type="button" variant="outline" onClick={onCancel}>
                Cancel
              </Button>
            ) : (
              <Button asChild variant="outline">
                <Link href="/tickets">Cancel</Link>
              </Button>
            )}
            <Button
              type="submit"
              disabled={mutation.isPending || !categories.data}
            >
              {mutation.isPending
                ? "Saving…"
                : ticket
                  ? "Save changes"
                  : "Create ticket"}
              <ArrowRight size={14} />
            </Button>
          </div>
        </form>
      </div>
    </section>
  );
}
