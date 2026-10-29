import { TicketSearch } from "@/features/search/ticket-search";
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const source = await searchParams;
  const params = Object.fromEntries(
    Object.entries(source).filter(
      (entry): entry is [string, string] => typeof entry[1] === "string",
    ),
  );
  return (
    <TicketSearch
      key={new URLSearchParams(params).toString()}
      params={params}
    />
  );
}
