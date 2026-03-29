namespace GRPCRemote.Drivers;

public sealed record KeyboardReport(byte Modifier, IReadOnlyList<byte> Keys, uint TimeoutMilliseconds);

public readonly record struct RelativeMouseReport(byte Buttons, sbyte X, sbyte Y);

public readonly record struct AbsoluteMouseReport(byte Buttons, ushort X, ushort Y);

public sealed record DriverEvent
{
    public required string Kind { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public byte Modifier { get; init; }

    public byte[] Keys { get; init; } = [];

    public byte Buttons { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public bool Relative { get; init; }
}
