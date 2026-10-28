import { TicketDetail } from "@/features/tickets/ticket-detail";
export default async function Page({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <TicketDetail id={id} />;
}
