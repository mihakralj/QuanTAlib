using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// A lightweight struct representing an OHLCV bar.
/// Pure data type: 48 bytes (long + 5 doubles).
/// </summary>
[SkipLocalsInit]
public readonly struct TBar : IEquatable<TBar>
{
    public readonly long Time;
    public readonly double Open;
    public readonly double High;
    public readonly double Low;
    public readonly double Close;
    public readonly double Volume;

    public DateTime AsDateTime => new(Time, DateTimeKind.Utc);

    // TValue conversions (Zero-copy / lightweight creation)
    public TValue O { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new(Time, Open); }
    public TValue H { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new(Time, High); }
    public TValue L { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new(Time, Low); }
    public TValue C { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new(Time, Close); }
    public TValue V { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new(Time, Volume); }

    // Computed properties (calculated on demand, no storage overhead)
    public double HL2 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (High + Low) * 0.5; }
    public double OC2 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (Open + Close) * 0.5; }
    public double OHL3 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (Open + High + Low) * 0.333333333333333333; }
    public double HLC3 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (High + Low + Close) * 0.333333333333333333; }
    public double OHLC4 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (Open + High + Low + Close) * 0.25; }
    public double HLCC4 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (High + Low + Close + Close) * 0.25; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar(long time, double open, double high, double low, double close, double volume)
    {
        Time = time;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar(DateTime time, double open, double high, double low, double close, double volume)
    {
        Time = time.Ticks;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(TBar bar) => bar.Close;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TValue(TBar bar) => new(bar.Time, bar.Close);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator DateTime(TBar bar) => new(bar.Time, DateTimeKind.Utc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => $"[{AsDateTime:yyyy-MM-dd HH:mm:ss}: O={Open:F2}, H={High:F2}, L={Low:F2}, C={Close:F2}, V={Volume:F2}]";

#pragma warning disable S1244 // Floating point equality is intentional for exact struct comparison
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TBar other) =>
        Time == other.Time &&
        Open == other.Open &&
        High == other.High &&
        Low == other.Low &&
        Close == other.Close &&
        Volume == other.Volume;

    public override bool Equals(object? obj) => obj is TBar other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Time, Open, High, Low, Close, Volume);
    public static bool operator ==(TBar left, TBar right) => left.Equals(right);
    public static bool operator !=(TBar left, TBar right) => !left.Equals(right);
#pragma warning restore S1244
}
