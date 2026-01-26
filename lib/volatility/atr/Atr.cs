using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// ATR: Average True Range
/// </summary>
/// <remarks>
/// ATR measures the volatility of an asset.
/// It is the moving average (typically RMA/Wilder's) of the True Range.
///
/// Calculation:
/// 1. True Range (TR) = Max(High - Low, |High - PrevClose|, |Low - PrevClose|)
///    - For the first bar, TR = High - Low
/// 2. ATR = RMA(TR)
///
/// Sources:
/// "New Concepts in Technical Trading Systems" by J. Welles Wilder
/// </remarks>
[SkipLocalsInit]
public sealed class Atr : AbstractBase
{
    private readonly Rma _rma;
    private readonly TValuePublishedHandler _handler;
    private TBar _prevBar;
    private TBar _p_prevBar;
    private bool _isInitialized;
    private bool _p_isInitialized;

    /// <summary>
    /// Creates ATR with specified period.
    /// </summary>
    /// <param name="period">Period for ATR calculation (must be > 0)</param>
    public Atr(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _rma = new Rma(period);
        Name = $"Atr({period})";
        WarmupPeriod = _rma.WarmupPeriod;
        _isInitialized = false;
        _handler = Handle;
    }

    /// <summary>
    /// Creates ATR with specified source and period.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for ATR calculation</param>
    public Atr(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += _handler;
    }

    /// <summary>
    /// Creates ATR with specified source and period.
    /// </summary>
    public Atr(TBarSeries source, int period) : this(period)
    {
        var tr = CalculateTrueRange(source);
        _rma.Prime(tr.Values);
        Last = _rma.Last;

        // Set internal state for subsequent Update(TBar) calls
        if (source.Count > 0)
        {
            _prevBar = source.Last;
            _isInitialized = true;
        }
        // We can't automatically subscribe to TBarSeries updates via this constructor
        // because AbstractBase doesn't enforce TBarSeries subscription structure,
        // but we can rely on manual updates or the user subscribing.
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the ATR has warmed up and is providing valid results.
    /// </summary>
    public override bool IsHot => _rma.IsHot;

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// Note: ATR needs OHLCV data to calculate TR properly.
    /// This Prime method expects pre-calculated TR values or handles basic priming
    /// if the user erroneously passes non-TR data. Ideally, use Batched TBarSeries.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        _rma.Prime(source);
        Last = _rma.Last;
    }

    /// <summary>
    /// Resets the ATR state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _rma.Reset();
        _prevBar = default;
        _p_prevBar = default;
        _isInitialized = false;
        _p_isInitialized = false;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        // Snapshot/restore for bar correction
        if (isNew)
        {
            _p_prevBar = _prevBar;
            _p_isInitialized = _isInitialized;
        }
        else
        {
            _prevBar = _p_prevBar;
            _isInitialized = _p_isInitialized;
        }

        double tr;
        if (!_isInitialized)
        {
            // For the very first bar, Wilder defines TR as High - Low
            tr = input.High - input.Low;
        }
        else
        {
            // Calculate TR
            double hl = input.High - input.Low;
            double hpc = Math.Abs(input.High - _prevBar.Close);
            double lpc = Math.Abs(input.Low - _prevBar.Close);
            tr = Math.Max(hl, Math.Max(hpc, lpc));
        }

        if (isNew)
        {
            _prevBar = input;
            _isInitialized = true;
        }

        // Smooth TR using RMA
        TValue result = _rma.Update(new TValue(input.Time, tr), isNew);

        Last = result;
        PubEvent(Last, isNew);
        return result;
    }

    /// <summary>
    /// Update for TValue input (not recommended for ATR as it needs OHLC).
    /// This treats the input value as the TR itself.
    /// </summary>
    public override TValue Update(TValue input, bool isNew = true)
    {
        // If user passes a single value, we assume it IS the True Range
        TValue result = _rma.Update(input, isNew);
        Last = result;
        PubEvent(Last, isNew);
        return result;
    }

    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        // 1. Calculate TR series
        TSeries trSeries = CalculateTrueRange(source);

        // 2. Run RMA on TR
        var result = _rma.Update(trSeries);
        Last = _rma.Last;

        // 3. Synchronize state for subsequent updates
        _prevBar = source.Last;
        _isInitialized = true;

        return result;
    }

    // AbstractBase.Update(TSeries)
    public override TSeries Update(TSeries source)
    {
        // Assumes source is already TR
        return _rma.Update(source);
    }

    private static TSeries CalculateTrueRange(TBarSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        if (source.Count == 0)
        {
            return new TSeries(t, v);
        }

        // First bar TR = H - L
        t.Add(source[0].Time);
        v.Add(source[0].High - source[0].Low);

        for (int i = 1; i < source.Count; i++)
        {
            var bar = source[i];
            var prevBar = source[i - 1];

            double hl = bar.High - bar.Low;
            double hpc = Math.Abs(bar.High - prevBar.Close);
            double lpc = Math.Abs(bar.Low - prevBar.Close);
            double tr = Math.Max(hl, Math.Max(hpc, lpc));

            t.Add(bar.Time);
            v.Add(tr);
        }
        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates ATR for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period)
    {
        var atr = new Atr(period);
        return atr.Update(source);
    }
}
