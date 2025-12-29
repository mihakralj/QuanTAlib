using System.Runtime.CompilerServices;
using System.Buffers;

namespace QuanTAlib;

/// <summary>
/// MACD: Moving Average Convergence Divergence
/// </summary>
/// <remarks>
/// MACD is a trend-following momentum indicator that shows the relationship between
/// two moving averages of a security's price.
///
/// Calculation:
/// MACD Line = Fast EMA - Slow EMA
/// Signal Line = EMA(MACD Line)
/// Histogram = MACD Line - Signal Line
///
/// Standard parameters: 12, 26, 9
/// </remarks>
[SkipLocalsInit]
public sealed class Macd : ITValuePublisher
{
    private readonly Ema _fastEma;
    private readonly Ema _slowEma;
    private readonly Ema _signalEma;
    private readonly TValuePublishedHandler _handler;

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
        WarmupPeriod = Math.Max(fastPeriod, slowPeriod) + signalPeriod;
    }

    public Macd(ITValuePublisher source, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
        : this(fastPeriod, slowPeriod, signalPeriod)
    {
        source.Pub += _handler;
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
        if (source.Count == 0) return [];

        var len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);

        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], true);
            t.Add(source[i].Time);
            v.Add(Last.Value);
        }

        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    /// <summary>
    /// Calculates the MACD Line (Fast EMA - Slow EMA).
    /// Does not calculate Signal or Histogram.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> destination, int fastPeriod = 12, int slowPeriod = 26)
    {
        if (source.Length != destination.Length)
            throw new ArgumentException("Source and destination must be same length", nameof(destination));

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
}
