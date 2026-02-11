using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// NATR: Normalized Average True Range
/// </summary>
/// <remarks>
/// ATR expressed as percentage of the closing price for cross-asset volatility comparison.
/// NATR enables direct comparison of volatility across instruments with different price levels.
/// This is identical to ATRP (Average True Range Percent) - both are (ATR / Close) × 100.
///
/// Calculation: <c>NATR = (ATR / Close) × 100</c>.
///
/// Key characteristics:
/// - Higher values indicate greater relative volatility
/// - Typical range: 0-10% for stocks, can be higher for crypto/commodities
/// - Enables cross-asset volatility comparison
/// - Uses RMA (Wilder's smoothing) for ATR calculation
/// </remarks>
/// <seealso href="Natr.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Natr : AbstractBase
{
    private readonly double _alpha;
    private readonly double _decay;

    private const double ConvergenceThreshold = 1e-10;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double RawRma,
        double E,
        double PrevClose,
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose,
        bool IsInitialized);

    private State _s;
    private State _ps;

    /// <summary>
    /// Gets the current ATR value (before normalization).
    /// </summary>
    public double Atr { get; private set; }

    /// <summary>
    /// Creates NATR with specified period.
    /// </summary>
    /// <param name="period">Period for ATR calculation (must be > 0, default: 14)</param>
    public Natr(int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 0.");
        }

        _alpha = 1.0 / period;
        _decay = 1.0 - _alpha;

        Name = $"Natr({period})";
        // Warmup based on RMA convergence: ln(0.05) / ln(1 - alpha)
        WarmupPeriod = (int)Math.Ceiling(Math.Log(0.05) / Math.Log(_decay));
        _s = new State(0, 1.0, double.NaN, double.NaN, double.NaN, double.NaN, false);
        _ps = _s;
    }

    /// <summary>
    /// Creates NATR from a TBarSeries.
    /// </summary>
    /// <param name="source">Bar series source</param>
    /// <param name="period">Period for NATR calculation</param>
    public Natr(TBarSeries source, int period = 14) : this(period)
    {
        var result = Update(source);
        if (result.Count > 0)
        {
            Last = result.Last;
        }
    }

    /// <summary>
    /// True if the NATR has warmed up and is providing valid results.
    /// </summary>
    public override bool IsHot => _s.E <= 0.05;

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// Note: NATR needs OHLCV data. This Prime method expects pre-calculated TR values.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            double tr = source[i];
            _s.RawRma = Math.FusedMultiplyAdd(_s.RawRma, _decay, _alpha * tr);
            _s.E *= _decay;
        }

        if (source.Length > 0)
        {
            Atr = _s.E > ConvergenceThreshold ? _s.RawRma / (1.0 - _s.E) : _s.RawRma;
            // Without close price, we can't calculate NATR percentage
            Last = new TValue(DateTime.UtcNow.Ticks, Atr);
        }
        _ps = _s;
    }

    /// <summary>
    /// Resets the NATR state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _s = new State(0, 1.0, double.NaN, double.NaN, double.NaN, double.NaN, false);
        _ps = _s;
        Atr = 0;
        Last = default;
    }

    /// <summary>
    /// Updates NATR with a new bar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        // Get valid values with last-value substitution
        double high = input.High;
        double low = input.Low;
        double close = input.Close;

        if (double.IsFinite(high))
        {
            _s.LastValidHigh = high;
        }
        else
        {
            high = _s.LastValidHigh;
        }

        if (double.IsFinite(low))
        {
            _s.LastValidLow = low;
        }
        else
        {
            low = _s.LastValidLow;
        }

        if (double.IsFinite(close))
        {
            _s.LastValidClose = close;
        }
        else
        {
            close = _s.LastValidClose;
        }

        // Handle case where no valid values yet
        if (double.IsNaN(close))
        {
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Calculate True Range
        double tr;
        if (!_s.IsInitialized || double.IsNaN(_s.PrevClose))
        {
            // First bar: TR = High - Low
            tr = high - low;
        }
        else
        {
            double hl = high - low;
            double hpc = Math.Abs(high - _s.PrevClose);
            double lpc = Math.Abs(low - _s.PrevClose);
            tr = Math.Max(hl, Math.Max(hpc, lpc));
        }

        // Calculate ATR using RMA with warmup compensation
        _s.RawRma = Math.FusedMultiplyAdd(_s.RawRma, _decay, _alpha * tr);
        _s.E *= _decay;

        double atr = _s.E > ConvergenceThreshold ? _s.RawRma / (1.0 - _s.E) : _s.RawRma;
        Atr = atr;

        // Calculate NATR: (ATR / Close) * 100
        double natr = Math.Abs(close) > 0 ? (atr / close) * 100.0 : double.NaN;

        // Update state
        if (isNew)
        {
            _s.PrevClose = close;
            _s.IsInitialized = true;
        }

        TValue result = new(input.Time, natr);
        Last = result;
        PubEvent(Last, isNew);
        return result;
    }

    /// <summary>
    /// Updates NATR with a TValue input.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// NATR requires OHLC bar data to calculate the percentage (ATR/Close * 100).
    /// Use Update(TBar) instead.
    /// </exception>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException(
            "NATR requires OHLC bar data to calculate the percentage (ATR/Close * 100). " +
            "Use Update(TBar) instead.");
    }

    /// <summary>
    /// Updates NATR from a TBarSeries.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();
        for (int i = 0; i < source.Count; i++)
        {
            TValue result = Update(source[i], true);
            t.Add(result.Time);
            v.Add(result.Value);
        }

        _ps = _s;

        return new TSeries(t, v);
    }

    /// <summary>
    /// Updates NATR from a TSeries.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// NATR requires OHLC bar data to calculate the percentage (ATR/Close * 100).
    /// Use Update(TBarSeries) instead.
    /// </exception>
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException(
            "NATR requires OHLC bar data to calculate the percentage (ATR/Close * 100). " +
            "Use Update(TBarSeries) instead.");
    }

    /// <summary>
    /// Calculates NATR for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period = 14)
    {
        var natr = new Natr(period);
        return natr.Update(source);
    }

    public static (TSeries Results, Natr Indicator) Calculate(TBarSeries source, int period = 14)
    {
        var indicator = new Natr(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
