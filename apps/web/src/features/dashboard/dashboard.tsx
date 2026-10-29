"use client";
import { Link } from "@/i18n/navigation";
import { useTranslations } from "next-intl";
import { useQuery } from "@tanstack/react-query";
import { getApiV1DashboardOptions } from "@ticketflow/api-client";
import { useAuth } from "@/features/auth/auth-provider";
import "@/lib/api";
import { useErrorMessage } from "@/lib/use-error-message";
import { Button } from "@/components/ui/button";

export function Dashboard() {
  const t = useTranslations("Dashboard");
  const errorMessage = useErrorMessage();
  const { user } = useAuth();
  const principal = user?.id;
  const query = useQuery({
    ...getApiV1DashboardOptions({
      headers: principal ? { "X-TicketFlow-User": principal } : undefined,
    }),
    enabled: !!principal,
    retry: false,
  });
  const staff = user?.role !== "Customer";
  const cards: Array<[string, number | string]> = query.data
    ? [
        [staff ? t("allTickets") : t("yourTickets"), query.data.total],
        [t("open"), query.data.open],
        [t("inProgress"), query.data.inProgress],
        [t("waiting"), query.data.waitingForCustomer],
        [t("resolved"), query.data.resolved],
        [t("closed"), query.data.closed],
        [t("unassigned"), query.data.unassigned],
        [t("highUrgent"), query.data.highPriority],
      ]
    : [];
  return (
    <section>
      <div className="page-heading">
        <div>
          <p className="page-eyebrow">{t("eyebrow")}</p>
          <h1>{t("title")}</h1>
          <p>{staff ? t("staffDescription") : t("customerDescription")}</p>
        </div>
        <Link href="/tickets">{t("viewTickets")}</Link>
      </div>
      {query.isPending && <p role="status">{t("loading")}</p>}
      {query.isError && (
        <div className="card page-state">
          <p role="alert">{errorMessage(query.error)}</p>
          <Button onClick={() => void query.refetch()}>{t("retry")}</Button>
        </div>
      )}
      {query.data && (
        <>
          <p className="dashboard-caption">{t("countsNote")}</p>
          <div className="dashboard-grid">
            {cards.map(([label, count]) => (
              <article className="card dashboard-card" key={label}>
                <h2>{label}</h2>
                <p>{count}</p>
              </article>
            ))}
          </div>
          {Number(query.data.total) === 0 && (
            <p className="dashboard-empty">
              {t("empty")} <Link href="/tickets/new">{t("create")}</Link>
            </p>
          )}
        </>
      )}
    </section>
  );
}
