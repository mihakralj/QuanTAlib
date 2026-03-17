using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PMA: Ehlers Predictive Moving Average
/// </summary>
/// <remarks>
/// Ehlers' linear-extrapolation filter using dual WMA cascade.
/// Cancels one WMA lag via extrapolation; Trigger line provides crossover signals.
///
/// Calculation: <c>PMA = 2×WMA(src) − WMA(WMA(src))</c>, <c>Trigger = (4×WMA(src) − WMA(WMA(src))) / 3</c>.
/// O(1) per bar via composed Wma instances.
/// </remarks>
/// <seealso href="Pma.md">Detailed documentation</seealso>
/// <seealso href="pma.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Pma : AbstractBase
{
    private readonly int _period;
    private readonly Wma _wma1;
    private readonly Wma _wma2;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _handler;
    private bool _disposed;
    private int _sampleCount;

    /// <summary>
    /// The Trigger (signal) line value: (4×WMA − WMA(WMA)) / 3.
    /// </summary>
    public TValue Trigger { get; private set; }

    public override bool IsHot => _sampleCount >= WarmupPeriod;

    /// <summary>
    /// Creates PMA with specified period.
    /// </summary>
    /// <param name="period">Window size for WMA smoothing (must be > 0, Ehlers default: 7)</param>
    public Pma(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _wma1 = new Wma(period);
        _wma2 = new Wma(period);
        Name = $"Pma({period})";
        WarmupPeriod = (period * 2) - 1;
    }

    /// <summary>
    /// Creates PMA subscribed to a source publisher.
    /// </summary>
    public Pma(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null && _handler != null)
            {
                _source.Pub -= _handler;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _sampleCount++;
        }

        TValue wma1Result = _wma1.Update(input, isNew);
        TValue wma2Result = _wma2.Update(wma1Result, isNew);

        double w1 = wma1Result.Value;
        double w2 = wma2Result.Value;

        // PMA = 2×WMA(src) − WMA(WMA(src))
        double pma = Math.FusedMultiplyAdd(2.0, w1, -w2);

        // Trigger = (4×WMA(src) − WMA(WMA(src))) / 3
        double trigger = Math.FusedMultiplyAdd(4.0, w1, -w2) / 3.0;

        Last = new TValue(input.Time, pma);
        Trigger = new TValue(input.Time, trigger);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        source.Times.CopyTo(tSpan);
        Batch(source.Values, vSpan, _period);

        Reset();

        int lookback = WarmupPeriod + 10;
        int startIndex = Math.Max(0, len - lookback);

        for (int i = startIndex; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]));
        }

        _sampleCount = len;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        Reset();
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var pma = new Pma(period);
        return pma.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double[]? wma1Array = len > 1024 ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> wma1 = len <= 1024
            ? stackalloc double[len]
            : wma1Array!.AsSpan(0, len);

        double[]? wma2Array = len > 1024 ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> wma2 = len <= 1024
            ? stackalloc double[len]
            : wma2Array!.AsSpan(0, len);

        try
        {
            Wma.Batch(source, wma1, period);
            Wma.Batch(wma1, wma2, period);

            // PMA = 2×WMA1 − WMA2
            for (int i = 0; i < len; i++)
            {
                output[i] = Math.FusedMultiplyAdd(2.0, wma1[i], -wma2[i]);
            }
        }
        finally
        {
            if (wma1Array != null)
            {
                ArrayPool<double>.Shared.Return(wma1Array);
            }
            if (wma2Array != null)
            {
                ArrayPool<double>.Shared.Return(wma2Array);
            }
        }
    }

    /// <summary>
    /// Span-based batch returning both PMA and Trigger lines.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> pmaOutput, Span<double> triggerOutput, int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (source.Length != pmaOutput.Length)
        {
            throw new ArgumentException("Source and pmaOutput must have the same length", nameof(pmaOutput));
        }

        if (source.Length != triggerOutput.Length)
        {
            throw new ArgumentException("Source and triggerOutput must have the same length", nameof(triggerOutput));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double[]? wma1Array = len > 1024 ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> wma1 = len <= 1024
            ? stackalloc double[len]
            : wma1Array!.AsSpan(0, len);

        double[]? wma2Array = len > 1024 ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> wma2 = len <= 1024
            ? stackalloc double[len]
            : wma2Array!.AsSpan(0, len);

        try
        {
            Wma.Batch(source, wma1, period);
            Wma.Batch(wma1, wma2, period);

            for (int i = 0; i < len; i++)
            {
                double w1 = wma1[i];
                double w2 = wma2[i];
                pmaOutput[i] = Math.FusedMultiplyAdd(2.0, w1, -w2);
                triggerOutput[i] = Math.FusedMultiplyAdd(4.0, w1, -w2) / 3.0;
            }
        }
        finally
        {
            if (wma1Array != null)
            {
                ArrayPool<double>.Shared.Return(wma1Array);
            }
            if (wma2Array != null)
            {
                ArrayPool<double>.Shared.Return(wma2Array);
            }
        }
    }

    public static (TSeries Results, Pma Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Pma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _wma1.Reset();
        _wma2.Reset();
        _sampleCount = 0;
        Last = default;
        Trigger = default;
    }
}
