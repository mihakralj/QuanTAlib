using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// NYQMA: Nyquist Moving Average
/// </summary>
/// <remarks>
/// Dr. Manfred G. Dürschner's lag-compensated filter applying the Nyquist-Shannon
/// sampling theorem to cascaded LWMAs. A single-smoothed LWMA and a double-smoothed
/// LWMA are combined via extrapolation to cancel lag while preventing aliasing.
///
/// Calculation: <c>NYQMA = (1 + α) × MA1 − α × MA2</c> where <c>α = N2 / (N1 − N2)</c>.
/// <c>MA1 = WMA(src, N1)</c>, <c>MA2 = WMA(MA1, N2)</c>.
/// Nyquist constraint: <c>N2 ≤ floor(N1/2)</c>.
/// O(1) per bar via composed Wma instances.
/// </remarks>
/// <seealso href="Nyqma.md">Detailed documentation</seealso>
/// <seealso href="nyqma.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Nyqma : AbstractBase
{
    private readonly int _period;
    private readonly int _nyquistPeriod;
    private readonly double _alpha;
    private readonly Wma _wma1;
    private readonly Wma _wma2;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _handler;
    private bool _disposed;
    private int _sampleCount;

    public override bool IsHot => _sampleCount >= WarmupPeriod;

    /// <summary>
    /// Creates NYQMA with specified periods.
    /// </summary>
    /// <param name="period">Primary LWMA period N1 (must be ≥ 3, default: 89)</param>
    /// <param name="nyquistPeriod">Secondary LWMA period N2 (clamped to ≤ floor(N1/2), default: 21)</param>
    public Nyqma(int period = 89, int nyquistPeriod = 21)
    {
        if (period < 3)
        {
            throw new ArgumentException("Period must be at least 3", nameof(period));
        }

        _period = period;
        _nyquistPeriod = Math.Clamp(nyquistPeriod, 1, period / 2);
        _alpha = (double)_nyquistPeriod / (_period - _nyquistPeriod);
        _wma1 = new Wma(_period);
        _wma2 = new Wma(_nyquistPeriod);
        Name = $"Nyqma({_period},{_nyquistPeriod})";
        WarmupPeriod = _period + _nyquistPeriod - 1;
    }

    /// <summary>
    /// Creates NYQMA subscribed to a source publisher.
    /// </summary>
    public Nyqma(ITValuePublisher source, int period = 89, int nyquistPeriod = 21) : this(period, nyquistPeriod)
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

        // NYQMA = (1 + α) × MA1 − α × MA2
        double nyqma = Math.FusedMultiplyAdd(1.0 + _alpha, w1, -_alpha * w2);

        Last = new TValue(input.Time, nyqma);
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
        Batch(source.Values, vSpan, _period, _nyquistPeriod);

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

    public static TSeries Batch(TSeries source, int period, int nyquistPeriod)
    {
        var nyqma = new Nyqma(period, nyquistPeriod);
        return nyqma.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, int nyquistPeriod)
    {
        if (period < 3)
        {
            throw new ArgumentException("Period must be at least 3", nameof(period));
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

        int n2 = Math.Clamp(nyquistPeriod, 1, period / 2);
        double alpha = (double)n2 / (period - n2);

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
            Wma.Batch(wma1, wma2, n2);

            // NYQMA = (1 + α) × MA1 − α × MA2
            double onePlusAlpha = 1.0 + alpha;
            for (int i = 0; i < len; i++)
            {
                output[i] = Math.FusedMultiplyAdd(onePlusAlpha, wma1[i], -alpha * wma2[i]);
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

    public static (TSeries Results, Nyqma Indicator) Calculate(TSeries source, int period, int nyquistPeriod)
    {
        var indicator = new Nyqma(period, nyquistPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _wma1.Reset();
        _wma2.Reset();
        _sampleCount = 0;
        Last = default;
    }
}
