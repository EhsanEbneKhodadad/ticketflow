import type { Metadata } from "next";
import "./globals.css";
export const metadata: Metadata = {
  title: "TicketFlow | Support workspace",
  description: "A thoughtfully built support ticket platform.",
};
export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
