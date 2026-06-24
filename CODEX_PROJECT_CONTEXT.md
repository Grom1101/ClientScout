# ClientScout - Current Working Context

This note is the working context for Codex. It reconciles the original project plan with the current repository state.

## Product Goal

ClientScout is a Telegram Mini App for one user at MVP stage. It has two core workflows:

- Find freelance orders from Telegram chats and Kwork, filter them by profile keywords, and show them as leads.
- Send outreach messages to selected chats/accounts with delays to reduce ban risk.

The long-term direction is internal MVP first, then possible SaaS expansion with more sources, subscriptions, billing, analytics, and team mode.

## Current Stack In Repo

Backend:

- .NET 10 projects: `ClientScout.Api`, `ClientScout.Application`, `ClientScout.Domain`, `ClientScout.Infrastructure`, `ClientScout.Scrapers`.
- ASP.NET Core controllers are present for auth, profiles, sources, leads, and outreach.
- EF Core with PostgreSQL migrations is present.
- Hangfire is configured and scheduled in `Program.cs`.
- JWT auth and Telegram initData validation exist.
- `docker-compose.yml` starts PostgreSQL and Redis, but Redis is not used by app code yet.

Frontend:

- React 19, Vite 8, TypeScript, Zustand, Axios, Recharts, lucide-react.
- Current frontend is mostly a visual prototype using `mockData.ts`.
- Routes: home, search, search chats, mailing, mailing chats.
- Search/settings/exchanges/order details and mailing messages/interval are implemented as modal components.

## Important Differences From Original Plan

- Original plan says React 18 / Vite 6 / .NET 9. The repo currently uses React 19 / Vite 8 / .NET 10.
- Original plan includes SignalR hub `/hubs/leads`; no SignalR hub is currently implemented.
- Original plan describes API modules like `leadsApi.ts`, `profilesApi.ts`, etc.; frontend currently has only `api/client.ts`.
- Original plan describes frontend API integration; current frontend uses mocks and does not authenticate against the backend yet.
- Original plan describes encrypted userbot sessions; current code stores `SessionData` as a plain string and encryption is not implemented.
- Original plan describes real WTelegramClient parsing/sending; current Telegram scraper and outreach sending are stubs.
- Original plan mentions Kwork RSS + HTTP; current Kwork scraper does basic AngleSharp page parsing only.
- Original plan includes tests; no test projects are currently present.
- Project is not currently inside a Git repository from `D:\Projects\ClientScout`.

## Current Build State

- `dotnet build ClientScout.sln` succeeds.
- Backend currently has warnings only:
  - duplicate `using` in `Application/DependencyInjection.cs`;
  - duplicate `using` in `Leads/LeadParsingService.cs`;
  - nullable warning in `Api/Program.cs`.
- `npm.cmd run build` fails because `frontend/src/api/client.ts` expects `useAppStore.getState().token`, but `AppState` has no `token`.
- Running `npm run build` directly in PowerShell may fail due to Windows script execution policy; use `npm.cmd run build`.

## Known Functional Gaps

- Frontend text has mojibake in several files (`Р...` instead of Russian text). Fixing encoding/display text is a separate cleanup task.
- Frontend is not wired to backend API.
- Telegram Mini App SDK/init flow is not wired in current frontend.
- Real Telegram userbot login flow is missing.
- Real Telegram chat scraping is missing.
- Real Telegram outreach sending is missing.
- Kwork parsing is fragile and likely needs RSS or a more reliable parser.
- SignalR live lead updates are missing.
- Hangfire dashboard is unauthenticated in MVP code.
- Secrets/config need hardening before deployment.

## Sensible Task Split With Antigravity

Recommended division:

- Antigravity: continue frontend screens, UX, visual fixes, mock-to-real UI behavior, text/copy cleanup.
- Codex: backend/API, database, auth, WTelegramClient integration, Kwork parser, Hangfire jobs, tests, and frontend API wiring when needed.

## Near-Term Priorities

1. Fix frontend build by aligning `api/client.ts` and `useAppStore`.
2. Fix mojibake Russian text in frontend files.
3. Decide whether frontend should stay mock-only for now or start real backend integration.
4. Implement proper auth state: Telegram initData -> backend JWT -> store token.
5. Add API clients for profiles, sources, leads, outreach.
6. Implement real Telegram userbot/session flow.
7. Replace Telegram scraper stub with real WTelegramClient reading.
8. Improve Kwork scraping.
9. Add SignalR only after basic lead creation works reliably.
10. Add focused tests around services and parsing logic.

## Open Questions

- Should the immediate next phase prioritize frontend polish or real backend functionality?
- Are Telegram `api_id`, `api_hash`, bot token, and a test userbot account already available?
- Should Kwork be implemented via RSS first, HTML first, or both?
- Is the MVP strictly single-user, or should code paths remain multi-user from the beginning?
