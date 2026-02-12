using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Buffers;

namespace QuanTAlib;

/// <summary>
/// MACD: Moving Average Convergence Divergence
/// </summary>
/// <remarks>
/// Trend-following momentum indicator showing EMA convergence/divergence.
/// Provides three outputs: MACD Line, Signal Line, and Histogram.
///
/// Calculation: <c>MACD = FastEMA - SlowEMA</c>, <c>Signal = EMA(MACD)</c>, <c>Histogram = MACD - Signal</c>.
/// </remarks>
/// <seealso href="Macd.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Macd : ITValuePublisher, IDisposable
{
    private readonly Ema _fastEma;
    private readonly Ema _slowEma;
    private readonly Ema _signalEma;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler _handler;
    private bool _disposed;

    public string Name { get; }
    public bool IsHot => _fastEma.IsHot && _slowEma.IsHot && _signalEma.IsHot;
    public int WarmupPeriod { get; }

    public TValue Last { get; private set; }
    public TValue Signal { get; private set; }
    public TValue Histogram { get; private set; }

    public event TValuePublishedHandler? Pub;

    public Macd(int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        _fastEma = new Ema(fastPeriod);
        _slowEma = new Ema(slowPeriod);
        _signalEma = new Ema(signalPeriod);
        _handler = Handle;

        Name = $"Macd({fastPeriod},{slowPeriod},{signalPeriod})";
        WarmupPeriod = Math.Max(fastPeriod, slowPeriod) + signalPeriod - 2;
    }

    public Macd(ITValuePublisher source, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
        : this(fastPeriod, slowPeriod, signalPeriod)
    {
        _source = source;
        _source.Pub += _handler;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= _handler;
            }
            _disposed = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _fastEma.Reset();
        _slowEma.Reset();
        _signalEma.Reset();
        Last = default;
        Signal = default;
        Histogram = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        var fast = _fastEma.Update(input, isNew);
        var slow = _slowEma.Update(input, isNew);

        double macdValue = fast.Value - slow.Value;
        var macdTValue = new TValue(input.Time, macdValue);

        var signal = _signalEma.Update(macdTValue, isNew);

        double histValue = macdValue - signal.Value;

        Last = macdTValue;
        Signal = signal;
        Histogram = new TValue(input.Time, histValue);

        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
            tSpan[i] = source[i].Time;
            vSpan[i] = Last.Value;
        }

        return new TSeries(t, v);
    }


    /// <summary>
    /// Initializes the indicator state using the provided series history.
    /// </summary>
    /// <param name="source">Historical data.</param>
    public void Prime(TSeries source)
    {
        Reset();
        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(new TValue(new DateTime(source.Times[i], DateTimeKind.Utc), source.Values[i]), isNew: true);
        }
    }

    public static TSeries Batch(TSeries source, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        var indicator = new Macd(fastPeriod, slowPeriod, signalPeriod);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates the MACD Line (Fast EMA - Slow EMA).
    /// Does not calculate Signal or Histogram.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> destination, int fastPeriod = 12, int slowPeriod = 26)
    {
        if (source.Length != destination.Length)
        {
            throw new ArgumentException("Source and destination must be same length", nameof(destination));
        }

        int len = source.Length;
        double[] fastBuffer = ArrayPool<double>.Shared.Rent(len);
        double[] slowBuffer = ArrayPool<double>.Shared.Rent(len);

        try
        {
            Span<double> fastSpan = fastBuffer.AsSpan(0, len);
            Span<double> slowSpan = slowBuffer.AsSpan(0, len);

            Ema.Batch(source, fastSpan, fastPeriod);
            Ema.Batch(source, slowSpan, slowPeriod);

            SimdExtensions.Subtract(fastSpan, slowSpan, destination);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(fastBuffer);
            ArrayPool<double>.Shared.Return(slowBuffer);
        }
    }

    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public static (TSeries Results, Macd Indicator) Calculate(TSeries source, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        var indicator = new Macd(fastPeriod, slowPeriod, signalPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}