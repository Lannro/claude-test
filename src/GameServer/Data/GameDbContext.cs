using Microsoft.EntityFrameworkCore;

namespace GameServer.Data;

public class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<PingLog> PingLogs => Set<PingLog>();
}
