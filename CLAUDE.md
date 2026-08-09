# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Early-stage scaffold for a browser-based multiplayer game. Single ASP.NET Core (C#) project
combining a REST API, a SignalR hub, and EF Core/Postgres persistence, plus a static
`wwwroot/index.html` client that exercises all three (REST fetch, SignalR chat, and a
WebGL canvas via PixiJS). Nothing here is game logic yet — it's the "hello world" plumbing
proving the stack talks to itself end to end.

## Commands

Primary path — everything in Docker:
```
docker compose up --build       # api + postgres + redis
docker compose down             # stop (pgdata volume persists)
```

Local dev in VS Code (run/debug the C# project directly instead of the `api` container):
```
docker compose up postgres redis -d   # start just the dependencies
```
Then run/debug `src/GameServer` from VS Code. `Properties/launchSettings.json` pins this to
`http://localhost:5080` (not 8080 — that port is only bound inside the `api` container).
`appsettings.Development.json` points EF Core/Redis at `localhost` for this mode, relying on
the `postgres`/`redis` containers' host port mappings (5432/6379).

**Note:** at time of writing, the dev machine used for this repo has no .NET SDK on PATH —
changes were verified by building/running through `docker compose`, not `dotnet build`/`dotnet run`,
directly. If the SDK still isn't available locally, prefer `docker compose up --build` to validate changes.

No test suite, linter, or migrations tooling is set up yet.

Reaching this from outside your own network (a real domain, permanently) is a separate path —
see "Production / external access" below. It uses different compose files; the two commands
above are unaffected by it.

## Architecture

- **`src/GameServer/Program.cs`** — single minimal-API entry point wiring up EF Core, Redis,
  SignalR, static files, and all HTTP endpoints. There's no controller layer; everything is
  inline minimal-API route handlers.
- **`Data/GameDbContext.cs`, `Data/PingLog.cs`** — EF Core + Npgsql. Schema is created via
  `Database.EnsureCreated()` at startup (see `Program.cs`), not migrations. Switch to
  `dotnet ef migrations` once the schema needs to evolve past this scaffold.
- **`Hubs/GameHub.cs`** — SignalR hub mapped at `/hubs/game`. This is where real-time
  game-state broadcast will eventually live; currently just an echo (`SendMessage` ->
  `ReceiveMessage`). If/when this needs to scale independently of the REST API, it's the
  piece to split into its own project/process.
- **`wwwroot/index.html`** — plain JS client, no build step. Loads SignalR JS client and
  PixiJS from CDN via `<script>` tags. Serves as the manual integration test for the whole
  stack (buttons for each REST endpoint, a SignalR chat box, a bouncing WebGL sprite).
- **`docker-compose.yml`** — three services: `api`, `postgres`, `redis`. `api` depends on the
  other two via `condition: service_healthy` — this matters because Postgres/Redis take a
  moment to become ready on first boot, and `EnsureCreated()` at startup crashes the app
  immediately if it connects before Postgres is actually accepting connections. Don't drop
  the healthchecks in favor of plain `depends_on`. On its own this file publishes no host
  ports — `docker-compose.override.yml` (auto-loaded, dev only) or `docker-compose.prod.yml`
  (explicit `-f`, see below) decide what's actually exposed.
- Connection strings differ by context: `appsettings.json` uses the Docker Compose service
  names (`postgres`, `redis`) for the containerized `api`; `appsettings.Development.json`
  overrides both to `localhost` for running the project directly outside Docker.

## Production / external access

For making this reachable from outside the local network permanently (not just testing),
the stack runs on a VPS behind a Caddy reverse proxy that owns TLS for a real domain:

```
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

- `docker-compose.prod.yml` adds the `caddy` service (publishes 80/443, terminates TLS via
  Let's Encrypt using `Caddyfile`) and stops `api`/`postgres`/`redis` from publishing any host
  ports directly — Caddy is the only public entrypoint. Note this explicit `-f` invocation does
  **not** auto-load `docker-compose.override.yml`, so dev port publishing doesn't apply here.
- Requires a `.env` file on the host (copy `.env.example`) with `DOMAIN` (must already have an
  A record pointed at the VPS) and a real `POSTGRES_PASSWORD` — don't reuse the local dev
  `gamepass`.
- `Program.cs` trusts Caddy's `X-Forwarded-*` headers via `UseForwardedHeaders` so the app sees
  the real client IP/scheme; this is safe only because Caddy is the sole thing that can reach
  `api` on the compose network.
- **No auth exists on any endpoint or the SignalR hub.** Once this is on a public domain,
  anyone with the link can read/write ping logs, bump the Redis counter, and join the chat hub.
  Fine for a scaffold, but add auth before relying on this being private in practice.

## Open items / in-progress debugging

- A Firefox-vs-Chrome discrepancy is being investigated: Chrome loads the app fine, Firefox
  shows an Express/Node-style "Cannot GET ..." response (not something ASP.NET Core itself
  produces), which points to Firefox hitting a different port/server than Chrome — most
  likely diverged address-bar autocomplete/history between the two browsers rather than a
  real server bug. Next step was to compare the exact URLs each browser is loading.
- No auth, no real game logic, no tests yet — this is intentionally just the scaffold.
