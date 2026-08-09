using GameServer.Data;
using GameServer.Hubs;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

var app = builder.Build();

// Hello-world scaffold: EnsureCreated is fine for now. Switch to EF Core
// migrations (dotnet ef migrations add) once the schema needs to evolve.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    db.Database.EnsureCreated();
}

// Trusts Caddy's X-Forwarded-* headers so the app sees the real scheme/client
// IP when running behind the prod reverse proxy. Caddy reaches this container
// only over the internal compose network (not loopback), so the default
// KnownNetworks/KnownProxies trust list has to be cleared for this to apply;
// safe here since Caddy is the sole entrypoint into that network.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownNetworks = { },
    KnownProxies = { }
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/hello", () =>
    Results.Ok(new { message = "Hello from the game server!", timestamp = DateTimeOffset.UtcNow }));

app.MapGet("/api/ping", async (GameDbContext db) =>
{
    var pings = await db.PingLogs
        .OrderByDescending(p => p.CreatedAt)
        .Take(10)
        .ToListAsync();
    return Results.Ok(pings);
});

app.MapPost("/api/ping", async (GameDbContext db) =>
{
    var ping = new PingLog { CreatedAt = DateTimeOffset.UtcNow };
    db.PingLogs.Add(ping);
    await db.SaveChangesAsync();
    return Results.Ok(ping);
});

app.MapGet("/api/counter", async (IConnectionMultiplexer redis) =>
{
    var redisDb = redis.GetDatabase();
    var count = await redisDb.StringIncrementAsync("hello-counter");
    return Results.Ok(new { counter = count });
});

app.MapHub<GameHub>("/hubs/game");

app.Run();
