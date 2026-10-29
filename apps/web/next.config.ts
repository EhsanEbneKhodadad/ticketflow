import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";
const config: NextConfig = {
  output: "standalone",
  transpilePackages: ["@ticketflow/api-client"],
  poweredByHeader: false,
  devIndicators: false,
  experimental: { proxyClientMaxBodySize: 10485760 + 65536 },
  async rewrites() {
    const origin = process.env.API_INTERNAL_URL ?? "http://localhost:5080";
    return [
      { source: "/api/v1/:path*", destination: `${origin}/api/v1/:path*` },
    ];
  },
};
const withNextIntl = createNextIntlPlugin("./src/i18n/request.ts");
export default withNextIntl(config);
