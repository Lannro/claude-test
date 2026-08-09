namespace GameServer.Game;

public record GameSnapshot(
    double ArenaWidth,
    double ArenaHeight,
    SnakeSnapshot[] Snakes,
    FoodSnapshot[] Food,
    LeaderboardEntry[] Leaderboard);

public record SnakeSnapshot(string Id, string Name, string Color, Point[] Trail, int Length);

public record FoodSnapshot(double X, double Y);

public record LeaderboardEntry(string Name, int Length);
