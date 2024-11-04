using System.Runtime.CompilerServices;

namespace QuanTAlib;

public interface ITBar
{
    DateTime Time { get; }
    double Open { get; }
    double High { get; }
    double Low { get; }
    double Close { get; }
    double Volume { get; }
    bool IsNew { get; }
}

[SkipLocalsInit]
public readonly record struct TBar(DateTime Time, double Open, double High, double Low, double Close, double Volume, bool IsNew = true) : ITBar
{
    public DateTime Time { get; init; } = Time;
    public double Open { get; init; } = Open;
    public double High { get; init; } = High;
    public double Low { get; init; } = Low;
    public double Close { get; init; } = Close;
    public double Volume { get; init; } = Volume;
    public bool IsNew { get; init; } = IsNew;

    public double HL2 => (High + Low) * 0.5;
    public double OC2 => (Open + Close) * 0.5;
    public double OHL3 => (Open + High + Low) / 3;
    public double HLC3 => (High + Low + Close) / 3;
    public double OHLC4 => (Open + High + Low + Close) * 0.25;
    public double HLCC4 => (High + Low + Close + Close) * 0.25;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar() : this(DateTime.UtcNow, 0, 0, 0, 0, 0) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar(double Open, double High, double Low, double Close, double Volume, bool IsNew = true)
        : this(DateTime.UtcNow, Open, High, Low, Close, Volume, IsNew) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar(double value)
        : this(Time: DateTime.UtcNow, Open: value, High: value, Low: value, Close: value, Volume: value, IsNew: true) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar(TValue value)
        : this(Time: value.Time, Open: value.Value, High: value.Value, Low: value.Value, Close: value.Value, Volume: value.Value, IsNew: value.IsNew) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar(TBar v)
        : this(Time: v.Time, Open: v.Open, High: v.High, Low: v.Low, Close: v.Close, Volume: v.Volume, IsNew: true) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(TBar bar) => bar.Close;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator DateTime(TBar tv) => tv.Time;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => $"[{Time:yyyy-MM-dd HH:mm:ss}: O={Open:F2}, H={High:F2}, L={Low:F2}, C={Close:F2}, V={Volume:F2}]";
}
