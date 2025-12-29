using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// A lightweight struct representing a time-value pair.
/// Pure data type: 16 bytes (long + double).
/// </summary>
[SkipLocalsInit]
[StructLayout(LayoutKind.Auto)]
public readonly record struct TValue(long Time, double Value)
{
    public DateTime AsDateTime => new(Time, DateTimeKind.Utc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue(DateTime time, double value)
        : this(time.Kind == DateTimeKind.Utc ? time.Ticks : time.ToUniversalTime().Ticks, value)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(TValue tv) => tv.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator DateTime(TValue tv) => new(tv.Time, DateTimeKind.Utc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => $"[{AsDateTime:yyyy-MM-dd HH:mm:ss}, {Value:F2}]";
}
