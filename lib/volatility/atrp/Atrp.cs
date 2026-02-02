using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ATRP: Average True Range Percent
/// </summary>
/// <remarks>
/// ATR as percentage of closing price for cross-asset volatility comparison.
/// Higher values indicate greater relative volatility; typical range 0-10%.
///
/// Calculation: <c>ATRP = (ATR / Close) × 100</c>.
/// </remarks>
/// <seealso href="Atrp.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Atrp : AbstractBase
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

    private State _state;
    private State _p_state;

    /// <summary>
    /// Creates ATRP with specified period.
    /// </summary>
    /// <param name="period">Period for ATR calculation (must be > 0)</param>
    public Atrp(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _alpha = 1.0 / period;
        _decay = 1.0 - _alpha;

        Name = $"Atrp({period})";
        // Warmup based on RMA convergence: ln(0.05) / ln(1 - alpha)
        WarmupPeriod = (int)Math.Ceiling(Math.Log(0.05) / Math.Log(_decay));
        _state = new State(0, 1.0, double.NaN, double.NaN, double.NaN, double.NaN, false);
        _p_state = _state;
    }

    /// <summary>
    /// Creates ATRP with specified source and period.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for ATRP calculation</param>
    public Atrp(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates ATRP from a TBarSeries.
    /// </summary>
    /// <param name="source">Bar series source</param>
    /// <param name="period">Period for ATRP calculation</param>
    public Atrp(TBarSeries source, int period) : this(period)
    {
        var result = Update(source);
        if (result.Count > 0)
        {
            Last = result.Last;
        }
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the ATRP has warmed up and is providing valid results.
    /// </summary>
    public override bool IsHot => _state.E <= 0.05;

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// Note: ATRP needs OHLCV data. This Prime method expects pre-calculated TR values.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            double tr = source[i];
            _state.RawRma = Math.FusedMultiplyAdd(_state.RawRma, _decay, _alpha * tr);
            _state.E *= _decay;
        }

        if (source.Length > 0)
        {
            double atr = _state.E > ConvergenceThreshold ? _state.RawRma / (1.0 - _state.E) : _state.RawRma;
            // Without close price, we can't calculate ATRP percentage
            Last = new TValue(DateTime.UtcNow, atr);
        }
        _p_state = _state;
    }

    /// <summary>
    /// Resets the ATRP state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _state = new State(0, 1.0, double.NaN, double.NaN, double.NaN, double.NaN, false);
        _p_state = _state;
        Last = default;
    }

    /// <summary>
    /// Updates ATRP with a new bar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        // Get valid values with last-value substitution
        double high = input.High;
        double low = input.Low;
        double close = input.Close;

        if (double.IsFinite(high))
        {
            _state.LastValidHigh = high;
        }
        else
        {
            high = _state.LastValidHigh;
        }

        if (double.IsFinite(low))
        {
            _state.LastValidLow = low;
        }
        else
        {
            low = _state.LastValidLow;
        }

        if (double.IsFinite(close))
        {
            _state.LastValidClose = close;
        }
        else
        {
            close = _state.LastValidClose;
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
        if (!_state.IsInitialized || double.IsNaN(_state.PrevClose))
        {
            // First bar: TR = High - Low
            tr = high - low;
        }
        else
        {
            double hl = high - low;
            double hpc = Math.Abs(high - _state.PrevClose);
            double lpc = Math.Abs(low - _state.PrevClose);
            tr = Math.Max(hl, Math.Max(hpc, lpc));
        }

        // Calculate ATR using RMA with warmup compensation
        _state.RawRma = Math.FusedMultiplyAdd(_state.RawRma, _decay, _alpha * tr);
        _state.E *= _decay;

        double atr = _state.E > ConvergenceThreshold ? _state.RawRma / (1.0 - _state.E) : _state.RawRma;

        // Calculate ATRP: (ATR / Close) * 100
        double atrp = Math.Abs(close) > 0 ? (atr / close) * 100.0 : double.NaN;

        // Update state
        if (isNew)
        {
            _state.PrevClose = close;
            _state.IsInitialized = true;
        }

        TValue result = new(input.Time, atrp);
        Last = result;
        PubEvent(Last, isNew);
        return result;
    }

    /// <summary>
    /// Updates ATRP with a TValue input.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// ATRP requires OHLC bar data to calculate the percentage (ATR/Close * 100).
    /// Use Update(TBar) instead.
    /// </exception>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException(
            "ATRP requires OHLC bar data to calculate the percentage (ATR/Close * 100). " +
            "Use Update(TBar) instead.");
    }

    /// <summary>
    /// Updates ATRP from a TBarSeries.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        for (int i = 0; i < source.Count; i++)
        {
            TValue result = Update(source[i], true);
            t.Add(result.Time);
            v.Add(result.Value);
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Updates ATRP from a TSeries.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// ATRP requires OHLC bar data to calculate the percentage (ATR/Close * 100).
    /// Use Update(TBarSeries) instead.
    /// </exception>
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException(
            "ATRP requires OHLC bar data to calculate the percentage (ATR/Close * 100). " +
            "Use Update(TBarSeries) instead.");
    }

    /// <summary>
    /// Calculates ATRP for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period)
    {
        var atrp = new Atrp(period);
        return atrp.Update(source);
    }
}
