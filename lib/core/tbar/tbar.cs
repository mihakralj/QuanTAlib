using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// A lightweight struct representing an OHLCV bar.
/// Pure data type: 48 bytes (long + 5 doubles).
/// </summary>
[SkipLocalsInit]
public readonly record struct TBar(long Time, double Open, double High, double Low, double Close, double Volume)
{
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
    public TBar(DateTime time, double open, double high, double low, double close, double volume)
        : this(time.Kind == DateTimeKind.Utc ? time.Ticks : time.ToUniversalTime().Ticks, open, high, low, close, volume)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(TBar bar) => bar.Close;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TValue(TBar bar) => new(bar.Time, bar.Close);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator DateTime(TBar bar) => new(bar.Time, DateTimeKind.Utc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => $"[{AsDateTime:yyyy-MM-dd HH:mm:ss}: O={Open:F2}, H={High:F2}, L={Low:F2}, C={Close:F2}, V={Volume:F2}]";
}
