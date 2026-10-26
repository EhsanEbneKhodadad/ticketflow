FROM node:24.21.0-alpine AS build
WORKDIR /source
RUN corepack enable && corepack prepare pnpm@10.14.0 --activate
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml .npmrc ./
COPY apps/web/package.json apps/web/
COPY packages/api-client/ packages/api-client/
RUN pnpm install --frozen-lockfile
COPY apps/web/ apps/web/
COPY packages/api-client/ packages/api-client/
ARG API_INTERNAL_URL=http://api:8080
ENV API_INTERNAL_URL=$API_INTERNAL_URL
ENV NEXT_TELEMETRY_DISABLED=1
RUN pnpm build
FROM node:24.21.0-alpine
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME=0.0.0.0
ENV NEXT_TELEMETRY_DISABLED=1
COPY --from=build --chown=node:node /source/apps/web/.next/standalone ./
COPY --from=build --chown=node:node /source/apps/web/.next/static ./apps/web/.next/static
COPY --from=build --chown=node:node /source/apps/web/public ./apps/web/public
USER node
EXPOSE 3000
CMD ["node", "apps/web/server.js"]
