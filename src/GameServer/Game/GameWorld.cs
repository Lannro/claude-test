using System.Collections.Concurrent;

namespace GameServer.Game;

// Server-authoritative, in-memory, single shared arena. Not persisted (Postgres/Redis
// deliberately untouched here) - state resets on restart, which is fine for a prototype.
public class GameWorld
{
    public const double ArenaWidth = 1000;
    public const double ArenaHeight = 700;

    private const double SnakeSpeed = 140; // px/sec
    private const double MaxTurnRatePerSec = Math.PI * 1.5;
    private const double CollisionRadius = 9;
    private const double FoodPickupRadius = 12;
    private const int PointsPerSegment = 3;
    private const int InitialSegments = 6;
    private const int FoodCount = 40;

    private static readonly string[] SnakeColors =
    [
        "#4cc9f0", "#f72585", "#4ade80", "#fbbf24", "#a78bfa", "#fb923c"
    ];

    private readonly ConcurrentDictionary<string, Snake> _snakes = new();
    private readonly ConcurrentDictionary<int, Food> _food = new();
    private readonly ConcurrentQueue<string> _deaths = new();
    private readonly Random _random = new();

    private int _nextFoodId;
    private int _colorIndex;

    public GameWorld()
    {
        for (var i = 0; i < FoodCount; i++)
        {
            SpawnFood();
        }
    }

    public void AddPlayer(string connectionId, string name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? "Snake" : name.Trim();
        var (x, y) = RandomPointInArena(margin: 100);

        var snake = new Snake
        {
            ConnectionId = connectionId,
            Name = trimmed[..Math.Min(trimmed.Length, 16)],
            Color = NextColor(),
            X = x,
            Y = y,
            SegmentCount = InitialSegments,
        };
        snake.Angle = _random.NextDouble() * Math.PI * 2;
        snake.TargetAngle = snake.Angle;
        snake.Trail.Add(new Point(x, y));

        _snakes[connectionId] = snake;
    }

    public void RemovePlayer(string connectionId) => _snakes.TryRemove(connectionId, out _);

    public void SetDirection(string connectionId, double angle)
    {
        if (_snakes.TryGetValue(connectionId, out var snake))
        {
            snake.TargetAngle = angle;
        }
    }

    public void Tick(double deltaSeconds)
    {
        foreach (var snake in _snakes.Values)
        {
            if (snake.Alive)
            {
                StepSnake(snake, deltaSeconds);
            }
        }

        CheckCollisions();
        CheckFoodPickups();
    }

    public List<string> DrainDeaths()
    {
        var deaths = new List<string>();
        while (_deaths.TryDequeue(out var connectionId))
        {
            deaths.Add(connectionId);
        }
        return deaths;
    }

    public GameSnapshot GetSnapshot()
    {
        var snakes = _snakes.Values
            .Select(s => new SnakeSnapshot(s.ConnectionId, s.Name, s.Color, s.Trail.ToArray(), s.SegmentCount))
            .ToArray();

        var leaderboard = snakes
            .OrderByDescending(s => s.Length)
            .Take(5)
            .Select(s => new LeaderboardEntry(s.Name, s.Length))
            .ToArray();

        var food = _food.Values
            .Select(f => new FoodSnapshot(f.X, f.Y))
            .ToArray();

        return new GameSnapshot(ArenaWidth, ArenaHeight, snakes, food, leaderboard);
    }

    private void StepSnake(Snake snake, double deltaSeconds)
    {
        var diff = NormalizeAngle(snake.TargetAngle - snake.Angle);
        var maxDelta = MaxTurnRatePerSec * deltaSeconds;
        snake.Angle = NormalizeAngle(snake.Angle + Math.Clamp(diff, -maxDelta, maxDelta));

        snake.X += Math.Cos(snake.Angle) * SnakeSpeed * deltaSeconds;
        snake.Y += Math.Sin(snake.Angle) * SnakeSpeed * deltaSeconds;

        snake.Trail.Insert(0, new Point(snake.X, snake.Y));
        var maxPoints = snake.SegmentCount * PointsPerSegment;
        if (snake.Trail.Count > maxPoints)
        {
            snake.Trail.RemoveRange(maxPoints, snake.Trail.Count - maxPoints);
        }
    }

    // No self-collision by design - matches slither.io, where your own tail
    // curling next to your head is not a death condition.
    private void CheckCollisions()
    {
        foreach (var snake in _snakes.Values)
        {
            if (!snake.Alive)
            {
                continue;
            }

            if (snake.X < 0 || snake.X > ArenaWidth || snake.Y < 0 || snake.Y > ArenaHeight)
            {
                Kill(snake);
                continue;
            }

            foreach (var other in _snakes.Values)
            {
                if (other.ConnectionId == snake.ConnectionId || !other.Alive)
                {
                    continue;
                }

                foreach (var point in other.Trail)
                {
                    var dx = snake.X - point.X;
                    var dy = snake.Y - point.Y;
                    if (dx * dx + dy * dy < CollisionRadius * CollisionRadius)
                    {
                        Kill(snake);
                        break;
                    }
                }

                if (!snake.Alive)
                {
                    break;
                }
            }
        }
    }

    private void Kill(Snake snake)
    {
        snake.Alive = false;
        _snakes.TryRemove(snake.ConnectionId, out _);
        _deaths.Enqueue(snake.ConnectionId);
    }

    private void CheckFoodPickups()
    {
        foreach (var snake in _snakes.Values)
        {
            if (!snake.Alive)
            {
                continue;
            }

            foreach (var food in _food.Values)
            {
                var dx = snake.X - food.X;
                var dy = snake.Y - food.Y;
                if (dx * dx + dy * dy >= FoodPickupRadius * FoodPickupRadius)
                {
                    continue;
                }

                if (_food.TryRemove(food.Id, out _))
                {
                    snake.SegmentCount++;
                    SpawnFood();
                }
            }
        }
    }

    private void SpawnFood()
    {
        var id = ++_nextFoodId;
        var (x, y) = RandomPointInArena(margin: 20);
        _food[id] = new Food { Id = id, X = x, Y = y };
    }

    private string NextColor() => SnakeColors[_colorIndex++ % SnakeColors.Length];

    private (double X, double Y) RandomPointInArena(double margin)
    {
        lock (_random)
        {
            var x = margin + _random.NextDouble() * (ArenaWidth - margin * 2);
            var y = margin + _random.NextDouble() * (ArenaHeight - margin * 2);
            return (x, y);
        }
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI)
        {
            angle -= 2 * Math.PI;
        }
        while (angle < -Math.PI)
        {
            angle += 2 * Math.PI;
        }
        return angle;
    }
}
