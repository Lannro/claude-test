using GameServer.Game;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Hubs;

public class GameHub(GameWorld world) : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    public void Join(string name) => world.AddPlayer(Context.ConnectionId, name);

    public void SetDirection(double angle) => world.SetDirection(Context.ConnectionId, angle);

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        world.RemovePlayer(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
