# GameServer scaffold

Minimal starting point for a web-based multiplayer game: ASP.NET Core (REST + SignalR),
Postgres (via EF Core), Redis, and a browser client with a PixiJS/WebGL canvas.

## Run it

```
docker compose up --build
```

Then open http://localhost:8080

- **REST**: `/api/hello`, `/api/ping` (GET/POST, backed by Postgres), `/api/counter` (backed by Redis)
- **SignalR**: hub at `/hubs/game`, `SendMessage` -> broadcasts `ReceiveMessage` to all connected clients
- **WebGL**: a bouncing box rendered via PixiJS, proving the rendering pipeline works end to end

## Layout

```
src/GameServer/
  Program.cs          - app startup, endpoints, hub mapping
  Data/                - EF Core DbContext + entities (Postgres)
  Hubs/GameHub.cs      - SignalR hub
  wwwroot/index.html   - browser client (REST calls, SignalR chat, PixiJS canvas)
docker-compose.yml     - api + postgres + redis
```

## Notes / next steps

- The DB schema is created via `EnsureCreated()` for now. Once the schema needs to evolve,
  switch to EF Core migrations (`dotnet ef migrations add <Name>`), which requires the
  .NET SDK installed locally (or run `dotnet ef` inside a throwaway SDK container).
- The game server (SignalR hub / tick loop) currently lives in the same process as the
  REST API. Split it into its own project once it needs to scale independently.
- No auth yet.
