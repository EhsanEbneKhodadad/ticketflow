export function GET() {
  return Response.json(
    { status: "healthy", service: "ticketflow-web" },
    {
      headers: { "Cache-Control": "no-store" },
    },
  );
}
