import { TicketList } from "@/features/tickets/ticket-list";
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const view =
    typeof params.view === "string" &&
    ["mine", "unassigned"].includes(params.view)
      ? params.view
      : "all";
  return <TicketList key={view} view={view} />;
}
