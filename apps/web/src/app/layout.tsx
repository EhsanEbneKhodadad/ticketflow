import { AppProviders } from "@/components/providers";
import "./workspace.css";
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
      <body>
        <AppProviders>{children}</AppProviders>
      </body>
    </html>
  );
}
