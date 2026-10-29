"use client";
import { Link } from "@/i18n/navigation";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { useRouter } from "@/i18n/navigation";
import { useQuery } from "@tanstack/react-query";
import {
  getApiV1AgentsOptions,
  getApiV1CategoriesOptions,
  getApiV1SearchTicketsOptions,
} from "@ticketflow/api-client";
import { Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/features/auth/auth-provider";
import {
  ticketStatuses,
  ticketPriorities,
  dateLabel,
} from "@/features/tickets/api";
import { ApiError } from "@/lib/api";
import { useErrorMessage } from "@/lib/use-error-message";
import {
  searchParameters,
  searchRequest,
  type SearchSegment,
  type SearchSort,
} from "./api";

export function SearchSnippet({ segments }: { segments: SearchSegment[] }) {
  return (
    <p className="search-snippet">
      {segments.map((segment, index) =>
        segment.highlighted ? (
          <mark key={index}>{segment.text}</mark>
        ) : (
          <span key={index}>{segment.text}</span>
        ),
      )}
    </p>
  );
}
export function TicketSearch({ params }: { params: Record<string, string> }) {
  const locale = useLocale();
  const t = useTranslations("Search");
  const errorMessage = useErrorMessage();
  const statusText = useTranslations("TicketStatus");
  const priorityText = useTranslations("Priority");
  const { user } = useAuth();
  const router = useRouter();
  const [validation, setValidation] = useState("");
  const queryParams = searchParameters(params);
  const page = Number(queryParams.get("page"));
  const principal = user?.id;
  const { register, handleSubmit, control } = useForm<Record<string, string>>({
    defaultValues: { ...params, sort: params.sort ?? "relevance" },
  });
  const query = useQuery({
    ...getApiV1SearchTicketsOptions({
      query: searchRequest(queryParams),
      headers: principal ? { "X-TicketFlow-User": principal } : undefined,
    }),
    enabled: !!user,
    retry: false,
  });
  const categories = useQuery({
    ...getApiV1CategoriesOptions({
      headers: principal ? { "X-TicketFlow-User": principal } : undefined,
    }),
    enabled: !!user,
  });
  const agents = useQuery({
    ...getApiV1AgentsOptions({
      headers: principal ? { "X-TicketFlow-User": principal } : undefined,
    }),
    enabled: !!user && user.role !== "Customer",
  });
  const sortLabels: Record<SearchSort, string> = {
    relevance: t("relevance"),
    newest: t("newest"),
  };
  const navigatePage = (value: number) => {
    const next = new URLSearchParams(queryParams);
    next.set("page", String(value));
    router.push("/search?" + next);
  };
  return (
    <section>
      <div className="page-heading">
        <div>
          <p className="page-eyebrow">{t("eyebrow")}</p>
          <h1>{t("title")}</h1>
          <p>{t("intro")}</p>
        </div>
        <Search size={25} color="#718568" />
      </div>
      <form
        className="card search-form"
        onSubmit={handleSubmit((input) => {
          setValidation("");
          if (input.from && input.to && input.from > input.to) {
            setValidation(t("dateError"));
            return;
          }
          router.push("/search?" + searchParameters({ ...input, page: "1" }));
        })}
      >
        <div className="form-field search-query">
          <label htmlFor="search-query">{t("keywords")}</label>
          <input
            id="search-query"
            placeholder={t("placeholder")}
            maxLength={160}
            {...register("q")}
          />
          <p className="field-hint">{t("filterHint")}</p>
        </div>
        <div className="search-filter-grid">
          <div className="form-field">
            <label htmlFor="search-status">{t("status")}</label>
            <select id="search-status" {...register("status")}>
              <option value="">{t("allStatuses")}</option>
              {ticketStatuses.map((value) => (
                <option key={value} value={value}>
                  {statusText(value)}
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label htmlFor="search-priority">{t("priority")}</label>
            <select id="search-priority" {...register("priority")}>
              <option value="">{t("allPriorities")}</option>
              {ticketPriorities.map((value) => (
                <option key={value} value={value}>
                  {priorityText(value)}
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label htmlFor="search-category">{t("category")}</label>
            <Controller
              name="categoryId"
              control={control}
              defaultValue={params.categoryId ?? ""}
              render={({ field }) => (
                <select id="search-category" {...field}>
                  <option value="">{t("allCategories")}</option>
                  {categories.data?.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.name}
                    </option>
                  ))}
                </select>
              )}
            />
          </div>
          {user?.role !== "Customer" && (
            <div className="form-field">
              <label htmlFor="search-agent">{t("assignedAgent")}</label>
              <Controller
                name="agentId"
                control={control}
                defaultValue={params.agentId ?? ""}
                render={({ field }) => (
                  <select id="search-agent" {...field}>
                    <option value="">{t("anyAgent")}</option>
                    {agents.data?.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.name}
                      </option>
                    ))}
                  </select>
                )}
              />
            </div>
          )}
          <div className="form-field">
            <label htmlFor="search-from">{t("from")}</label>
            <input id="search-from" type="date" {...register("from")} />
          </div>
          <div className="form-field">
            <label htmlFor="search-to">{t("through")}</label>
            <input id="search-to" type="date" {...register("to")} />
          </div>
          <div className="form-field">
            <label htmlFor="search-sort">{t("sort")}</label>
            <select id="search-sort" {...register("sort")}>
              {Object.entries(sortLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </div>
        </div>
        {(categories.error || agents.error) && (
          <p role="alert" className="field-error">
            {t("filterError")}{" "}
            <button
              type="button"
              onClick={() => {
                void categories.refetch();
                if (user?.role !== "Customer") void agents.refetch();
              }}
            >
              {t("retryFilters")}
            </button>
          </p>
        )}
        {validation && (
          <p role="alert" className="error-box">
            {validation}
          </p>
        )}
        <div className="search-form-actions">
          <Button type="submit">{t("submit")}</Button>
          <Link href="/search">{t("clear")}</Link>
        </div>
      </form>
      <p className="search-explanation">{t("explanation")}</p>
      <div
        className="card search-results"
        aria-label={t("results")}
        aria-busy={query.isFetching}
      >
        {query.isPending ? (
          <div className="page-state" role="status">
            <span className="spinner" />
            {t("loading")}
          </div>
        ) : query.error ? (
          <div className="page-state" role="alert">
            <h2>
              {query.error instanceof ApiError && query.error.status === 503
                ? t("unavailable")
                : query.error instanceof ApiError && query.error.status === 401
                  ? t("signIn")
                  : t("failed")}
            </h2>
            <p>{errorMessage(query.error)}</p>
            <div className="search-form-actions">
              <Button variant="outline" onClick={() => void query.refetch()}>
                {t("retry")}
              </Button>
              <Link href="/tickets">{t("browse")}</Link>
            </div>
          </div>
        ) : !query.data?.items.length ? (
          <div className="page-state">
            <h2>{t("empty")}</h2>
            <p>{t("emptyHint")}</p>
          </div>
        ) : (
          <>
            <div className="search-results-heading">
              {new Intl.NumberFormat(locale).format(Number(query.data.total))}{" "}
              {t("matching")}{" "}
              {Number(query.data.total) === 1 ? t("ticket") : t("tickets")} ·{" "}
              {t("page")} {new Intl.NumberFormat(locale).format(Number(page))}
            </div>
            <ol className="search-result-list">
              {query.data.items.map(({ ticket, snippet }) => (
                <li key={ticket.id}>
                  <div className="search-result-top">
                    <span className="ticket-number">{ticket.number}</span>
                    <span className={"status-badge status-" + ticket.status}>
                      {statusText(ticket.status)}
                    </span>
                    <span
                      className={"priority-badge priority-" + ticket.priority}
                    >
                      {priorityText(ticket.priority)}
                    </span>
                  </div>
                  <h2>
                    <Link href={"/tickets/" + ticket.id}>{ticket.title}</Link>
                  </h2>
                  <SearchSnippet segments={snippet} />
                  <p className="ticket-row-meta">
                    {ticket.category} · {ticket.agentName ?? t("anyAgent")} ·{" "}
                    {t("opened")} {dateLabel(ticket.createdAt, locale)}
                  </p>
                </li>
              ))}
            </ol>
          </>
        )}
        <div className="inbox-footer">
          <span>{t("pageLimit")}</span>
          <div className="pagination">
            <Button
              size="small"
              variant="outline"
              aria-label={t("previous")}
              disabled={page <= 1 || query.isFetching}
              onClick={() => navigatePage(page - 1)}
            >
              {t("previous")}
            </Button>
            <Button
              size="small"
              variant="outline"
              aria-label={t("next")}
              disabled={
                page >= 50 ||
                page * 20 >= Number(query.data?.total ?? 0) ||
                query.isFetching
              }
              onClick={() => navigatePage(page + 1)}
            >
              {t("next")}
            </Button>
          </div>
        </div>
      </div>
    </section>
  );
}
