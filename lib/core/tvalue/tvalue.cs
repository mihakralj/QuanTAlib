using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// A lightweight struct representing a time-value pair.
/// Pure data type: 16 bytes (long + double).
/// </summary>
[SkipLocalsInit]
public readonly struct TValue : IEquatable<TValue>
{
    /// <summary>
    /// Time in ticks (UTC).
    /// </summary>
    public readonly long Time;

    /// <summary>
    /// The value.
    /// </summary>
    public readonly double Value;

    /// <summary>
    /// Convenience property to get DateTime from Ticks.
    /// </summary>
    public DateTime AsDateTime => new(Time, DateTimeKind.Utc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue(long time, double value)
    {
        Time = time;
        Value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue(DateTime time, double value)
    {
        Time = time.Ticks;
        Value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(TValue tv) => tv.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator DateTime(TValue tv) => new(tv.Time, DateTimeKind.Utc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => $"[{AsDateTime:yyyy-MM-dd HH:mm:ss}, {Value:F2}]";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TValue other) => Time == other.Time && Value == other.Value;

    public override bool Equals(object? obj) => obj is TValue other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Time, Value);
    public static bool operator ==(TValue left, TValue right) => left.Equals(right);
    public static bool operator !=(TValue left, TValue right) => !left.Equals(right);
}
