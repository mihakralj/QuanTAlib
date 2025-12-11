using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DWMA: Double Weighted Moving Average
/// </summary>
/// <remarks>
/// DWMA applies a Weighted Moving Average (WMA) twice.
/// It provides a smoother curve than a standard WMA but with slightly more lag.
///
/// Formula:
/// DWMA = WMA(WMA(source, period), period)
/// </remarks>
[SkipLocalsInit]
public sealed class Dwma : ITValuePublisher
{
    private readonly int _period;
    private readonly Wma _wma1;
    private readonly Wma _wma2;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Current DWMA value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has enough data to produce valid results.
    /// </summary>
    public bool IsHot => _wma1.IsHot && _wma2.IsHot;

    public event Action<TValue>? Pub;

    /// <summary>
    /// Creates DWMA with specified period.
    /// </summary>
    /// <param name="period">Window size (must be > 0)</param>
    public Dwma(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _wma1 = new Wma(period);
        _wma2 = new Wma(period);
        Name = $"Dwma({period})";
    }

    public Dwma(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += (item) => Update(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        TValue wma1Result = _wma1.Update(input, isNew);
        Last = _wma2.Update(wma1Result, isNew);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        source.Times.CopyTo(tSpan);
        Calculate(source.Values, vSpan, _period);

        // Restore state
        // We need to replay the last part to restore the internal WMAs state
        // Since DWMA is WMA(WMA), the effective lookback is roughly 2*Period
        // But to be safe and simple, we can just reset and replay the last 2*Period bars.
        
        _wma1.Reset();
        _wma2.Reset();
        
        int warmup = _period * 2; // Approximate warmup needed
        int startIndex = Math.Max(0, len - warmup);
        
        for (int i = startIndex; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]));
        }

        return new TSeries(t, v);
    }

    public static TSeries Calculate(TSeries source, int period)
    {
        var dwma = new Dwma(period);
        return dwma.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");

        // We need a temporary buffer for the first WMA pass
        // Use stackalloc for small sizes, heap for large
        if (source.Length <= 1024)
        {
            Span<double> temp = stackalloc double[source.Length];
            Wma.Calculate(source, temp, period);
            Wma.Calculate(temp, output, period);
        }
        else
        {
            double[] temp = new double[source.Length];
            Wma.Calculate(source, temp, period);
            Wma.Calculate(temp, output, period);
        }
    }
    
    public void Reset()
    {
        _wma1.Reset();
        _wma2.Reset();
        Last = default;
    }
}
