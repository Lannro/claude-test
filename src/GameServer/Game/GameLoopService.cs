using GameServer.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Game;

public class GameLoopService(GameWorld world, IHubContext<GameHub> hub) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(50); // 20 Hz

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            world.Tick(TickInterval.TotalSeconds);

            foreach (var connectionId in world.DrainDeaths())
            {
                await hub.Clients.Client(connectionId).SendAsync("Died", cancellationToken: stoppingToken);
            }

            await hub.Clients.All.SendAsync("StateUpdate", world.GetSnapshot(), cancellationToken: stoppingToken);
        }
    }
}
