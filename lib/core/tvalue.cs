using System.Runtime.CompilerServices;

namespace QuanTAlib;

public interface ITValue
{
    DateTime Time { get; }
    double Value { get; }
    bool IsNew { get; }
    bool IsHot { get; }
}

[SkipLocalsInit]
public readonly record struct TValue(DateTime Time, double Value, bool IsNew = true, bool IsHot = true) : ITValue
{
    public DateTime Time { get; init; } = Time;
    public double Value { get; init; } = Value;
    public bool IsNew { get; init; } = IsNew;
    public bool IsHot { get; init; } = IsHot;
    public DateTime t => Time;
    public double v => Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue() : this(DateTime.UtcNow, 0) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue(double value, bool isNew = true, bool isHot = true)
        : this(DateTime.UtcNow, value, IsNew: isNew, IsHot: isHot) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(TValue tv) => tv.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator DateTime(TValue tv) => tv.Time;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TValue(double value) => new TValue(DateTime.UtcNow, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => $"[{Time:yyyy-MM-dd HH:mm:ss}, {Value:F2}, IsNew: {IsNew}, IsHot: {IsHot}]";
}
