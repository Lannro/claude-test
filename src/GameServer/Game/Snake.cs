namespace GameServer.Game;

public readonly record struct Point(double X, double Y);

public class Snake
{
    public required string ConnectionId { get; init; }
    public required string Name { get; init; }
    public required string Color { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Angle { get; set; }
    public double TargetAngle { get; set; }
    public int SegmentCount { get; set; }
    public List<Point> Trail { get; } = [];
    public bool Alive { get; set; } = true;
}
