using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Numerics;

namespace QuanTAlib;

/// <summary>
/// BOP: Balance of Power
/// </summary>
/// <remarks>
/// BOP measures the strength of buyers vs sellers by comparing the close price to the open price,
/// relative to the high-low range.
///
/// Formula:
/// BOP = (Close - Open) / (High - Low)
///
/// Key characteristics:
/// - Oscillates between -1 and 1
/// - 1 indicates buyers dominated (Close = High, Open = Low)
/// - -1 indicates sellers dominated (Close = Low, Open = High)
/// - 0 indicates balance (Close = Open)
/// - Often smoothed with an SMA (though this implementation provides the raw value)
///
/// Sources:
/// https://www.investopedia.com/terms/b/bop.asp
/// </remarks>
[SkipLocalsInit]
public sealed class Bop : ITValuePublisher
{
    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public static string Name => "Bop";

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current BOP value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has a valid value (always true for BOP as it has no warmup).
    /// </summary>
    public static bool IsHot => true;

    /// <summary>
    /// The number of bars required for the indicator to warm up.
    /// </summary>
    public static int WarmupPeriod => 0;

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        Last = default;
    }

    /// <summary>
    /// Updates the indicator with a new bar.
    /// </summary>
    /// <param name="input">The input bar.</param>
    /// <param name="isNew">Whether this is a new bar or an update to the current one.</param>
    /// <returns>The updated BOP value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        double range = input.High - input.Low;
        double bop = 0;

        if (range > double.Epsilon)
        {
            bop = (input.Close - input.Open) / range;
        }

        Last = new TValue(input.Time, bop);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a new value (not supported for BOP as it requires OHLC).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        // BOP requires OHLC, so we can't calculate it from a single value.
        // We'll treat the input value as Close, and assume Open=Close, High=Close, Low=Close,
        // which results in 0/0 -> 0.
        // Or we could throw NotSupportedException.
        // Given the interface contract, returning 0 is safer than crashing.
        Last = new TValue(input.Time, 0);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a series of bars.
    /// </summary>
    public static TSeries Update(TBarSeries source)
    {
        return Batch(source);
    }

    /// <summary>
    /// Calculates BOP for a series of bars.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> destination)
    {
        int len = Math.Min(open.Length, Math.Min(high.Length, Math.Min(low.Length, close.Length)));
        if (destination.Length < len)
            len = destination.Length;

        int i = 0;
        if (Vector.IsHardwareAccelerated && len >= Vector<double>.Count)
        {
            var epsilon = new Vector<double>(double.Epsilon);
            ref var oRef = ref MemoryMarshal.GetReference(open);
            ref var hRef = ref MemoryMarshal.GetReference(high);
            ref var lRef = ref MemoryMarshal.GetReference(low);
            ref var cRef = ref MemoryMarshal.GetReference(close);
            ref var dRef = ref MemoryMarshal.GetReference(destination);

            while (i <= len - Vector<double>.Count)
            {
                var o = Vector.LoadUnsafe(ref oRef, (nuint)i);
                var h = Vector.LoadUnsafe(ref hRef, (nuint)i);
                var l = Vector.LoadUnsafe(ref lRef, (nuint)i);
                var c = Vector.LoadUnsafe(ref cRef, (nuint)i);

                var range = h - l;
                var body = c - o;

                // Create a mask where range > Epsilon
                var mask = Vector.GreaterThan(range, epsilon);

                // Perform division (results in NaN/Inf if range is 0, but we'll mask it out)
                var div = body / range;

                // Select div where mask is true, otherwise 0
                var result = Vector.ConditionalSelect(mask, div, Vector<double>.Zero);

                result.StoreUnsafe(ref dRef, (nuint)i);

                i += Vector<double>.Count;
            }
        }

        for (; i < len; i++)
        {
            double range = high[i] - low[i];
            destination[i] = range > double.Epsilon ? (close[i] - open[i]) / range : 0;
        }
    }

    /// <summary>
    /// Calculates BOP for a TBarSeries.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSeries Batch(TBarSeries source)
    {
        if (source.Count == 0) return new TSeries([], []);

        var len = source.Count;

        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        source.Open.Times.CopyTo(tSpan);
        Calculate(source.Open.Values, source.High.Values, source.Low.Values, source.Close.Values, vSpan);

        return new TSeries(t, v);
    }
}
