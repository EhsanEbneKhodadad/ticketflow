import type { NextConfig } from "next";
const config: NextConfig = {
  output: "standalone",
  poweredByHeader: false,
  async rewrites() {
    const origin = process.env.API_INTERNAL_URL ?? "http://localhost:5080";
    return [
      { source: "/api/v1/:path*", destination: `${origin}/api/v1/:path*` },
    ];
  },
};
export default config;
