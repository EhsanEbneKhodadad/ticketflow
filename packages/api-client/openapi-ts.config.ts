import type { UserConfig } from "@hey-api/openapi-ts";

const config: UserConfig = {
  input: "http://localhost:5080/openapi/v1.json",
  output: "src/generated",
  plugins: [
    "@hey-api/typescript",
    "@hey-api/sdk",
    "@hey-api/client-fetch",
    {
      name: "@tanstack/react-query",
      queryOptions: true,
      queryKeys: { tags: true },
      mutationOptions: true,
      mutationKeys: false,
    },
  ],
};

export default config;
