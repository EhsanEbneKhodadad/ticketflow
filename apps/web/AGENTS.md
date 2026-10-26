# Frontend rules

- Use TypeScript strict mode and Next.js App Router.
- Keep src/app route files thin; put product behavior under src/features/<feature>.
- Keep reusable visual primitives under src/components/ui and transport utilities under src/lib.
- Default to Server Components. Add "use client" only where browser interaction requires it.
- Use semantic HTML, visible keyboard focus, descriptive labels, and responsive layouts.
- Include loading, empty, error, and permission-denied states when adding data-driven screens.
- Consume types from @ticketflow/api-client; do not copy API DTOs into the frontend.
- API authorization is mandatory even when UI controls are hidden.
- Never place tokens in localStorage or expose server secrets through NEXT_PUBLIC variables.
- Use TanStack Query for interactive remote state and React Hook Form/Zod for forms when those features arrive.
- Test observable user behavior using Testing Library; add Cypress journeys with the ticket workflow.
- Keep the foundation page honest: no fabricated metrics or working-feature claims.
- Run root pnpm lint, typecheck, test, build, and format:check.

<!-- BEGIN:nextjs-agent-rules -->

## This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->
