using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// ADR: Average Daily Range
/// </summary>
/// <remarks>
/// Smoothed average of High-Low ranges; simpler than ATR (no gap accounting).
/// Supports SMA/EMA/WMA smoothing methods.
///
/// Calculation: <c>ADR = MA(High - Low, period)</c>.
/// </remarks>
/// <seealso href="Adr.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Adr : AbstractBase
{
    private readonly AbstractBase _ma;
    private ITValuePublisher? _source;
    private bool _disposed;

    /// <summary>
    /// Creates ADR with specified period and smoothing method.
    /// </summary>
    /// <param name="period">Period for ADR calculation (must be > 0)</param>
    /// <param name="method">Smoothing method (default: SMA)</param>
    public Adr(int period, AdrMethod method = AdrMethod.Sma)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _ma = method switch
        {
            AdrMethod.Sma => new Sma(period),
            AdrMethod.Ema => new Ema(period),
            AdrMethod.Wma => new Wma(period),
            _ => throw new ArgumentException($"Invalid smoothing method: {method}", nameof(method))
        };

        Name = $"Adr({period},{method})";
        WarmupPeriod = _ma.WarmupPeriod;
    }

    /// <summary>
    /// Creates ADR with specified source, period, and smoothing method.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for ADR calculation</param>
    /// <param name="method">Smoothing method (default: SMA)</param>
    public Adr(ITValuePublisher source, int period, AdrMethod method = AdrMethod.Sma) : this(period, method)
    {
        _source = source;
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates ADR from a TBarSeries.
    /// </summary>
    /// <param name="source">Bar series source</param>
    /// <param name="period">Period for ADR calculation</param>
    /// <param name="method">Smoothing method (default: SMA)</param>
    public Adr(TBarSeries source, int period, AdrMethod method = AdrMethod.Sma) : this(period, method)
    {
        var ranges = CalculateRanges(source);
        _ma.Prime(ranges.Values);
        Last = _ma.Last;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the ADR has warmed up and is providing valid results.
    /// </summary>
    public override bool IsHot => _ma.IsHot;

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// Note: ADR needs OHLCV data to calculate range properly.
    /// This Prime method expects pre-calculated range values.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        _ma.Prime(source);
        Last = _ma.Last;
    }

    /// <summary>
    /// Resets the ADR state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _ma.Reset();
        Last = default;
    }

    /// <summary>
    /// Updates ADR with a new bar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        double range = input.High - input.Low;

        // Handle invalid range values
        if (!double.IsFinite(range) || range < 0)
        {
            range = 0;
        }

        TValue result = _ma.Update(new TValue(input.Time, range), isNew);
        Last = result;
        PubEvent(Last, isNew);
        return result;
    }

    /// <summary>
    /// Updates ADR with a TValue input.
    /// This treats the input value as the range itself.
    /// </summary>
    public override TValue Update(TValue input, bool isNew = true)
    {
        TValue result = _ma.Update(input, isNew);
        Last = result;
        PubEvent(Last, isNew);
        return result;
    }

    /// <summary>
    /// Updates ADR from a TBarSeries.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        // Calculate range series
        TSeries rangeSeries = CalculateRanges(source);

        // Run MA on ranges
        var result = _ma.Update(rangeSeries);
        Last = _ma.Last;

        return result;
    }

    /// <summary>
    /// Updates ADR from a TSeries (assumes values are already ranges).
    /// </summary>
    public override TSeries Update(TSeries source)
    {
        var result = _ma.Update(source);
        Last = _ma.Last;
        return result;
    }

    /// <summary>
    /// Disposes the ADR and unsubscribes from the source.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= Handle;
                _source = null;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Calculates High-Low ranges from bar series.
    /// </summary>
    private static TSeries CalculateRanges(TBarSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        for (int i = 0; i < source.Count; i++)
        {
            var bar = source[i];
            double range = bar.High - bar.Low;

            // Handle invalid values
            if (!double.IsFinite(range) || range < 0)
            {
                range = 0;
            }

            t.Add(bar.Time);
            v.Add(range);
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates ADR for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period, AdrMethod method = AdrMethod.Sma)
    {
        var adr = new Adr(period, method);
        return adr.Update(source);
    }
}

/// <summary>
/// Smoothing method for ADR calculation.
/// </summary>
public enum AdrMethod
{
    /// <summary>Simple Moving Average</summary>
    Sma = 1,
    /// <summary>Exponential Moving Average</summary>
    Ema = 2,
    /// <summary>Weighted Moving Average</summary>
    Wma = 3
}
