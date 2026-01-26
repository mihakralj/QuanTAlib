using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ATRN: Average True Range Normalized
/// </summary>
/// <remarks>
/// ATRN normalizes the ATR to a [0,1] range using min-max scaling over a lookback window.
/// This makes volatility comparable across different price scales and time periods.
///
/// Calculation:
/// 1. Calculate ATR using RMA smoothing
/// 2. Find min/max ATR over lookback window (10 * period)
/// 3. Normalize: (ATR - minATR) / (maxATR - minATR)
/// 4. If maxATR equals minATR, return 0.5
///
/// Sources:
/// Derived from ATR by J. Welles Wilder, normalized for cross-asset comparison.
/// </remarks>
[SkipLocalsInit]
public sealed class Atrn : AbstractBase
{
    private readonly int _lookbackWindow;
    private readonly Rma _rma;
    private readonly RingBuffer _atrBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        TBar PrevBar,
        bool IsInitialized,
        double LastValidTr,
        double LastValidAtr);

    private State _state;
    private State _p_state;

    /// <summary>
    /// Creates ATRN with specified period.
    /// </summary>
    /// <param name="period">Period for ATR calculation (must be > 0)</param>
    public Atrn(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _lookbackWindow = 10 * period;
        _rma = new Rma(period);
        _atrBuffer = new RingBuffer(_lookbackWindow);

        Name = $"Atrn({period})";
        WarmupPeriod = _rma.WarmupPeriod + _lookbackWindow;
        _state = new State(PrevBar: default, IsInitialized: false, LastValidTr: 0.0, LastValidAtr: 0.0);
        _p_state = _state;
    }

    /// <summary>
    /// Creates ATRN with specified source and period.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for ATR calculation</param>
    public Atrn(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates ATRN from a TBarSeries.
    /// </summary>
    /// <param name="source">Bar series source</param>
    /// <param name="period">Period for ATR calculation</param>
    public Atrn(TBarSeries source, int period) : this(period)
    {
        var result = Update(source);
        if (result.Count > 0)
        {
            Last = result.Last;
        }
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the ATRN has warmed up and is providing valid results.
    /// </summary>
    public override bool IsHot => _rma.IsHot && _atrBuffer.Count >= _lookbackWindow;

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// Note: ATRN needs OHLCV data. This Prime method expects pre-calculated TR values.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            double tr = source[i];
            TValue atr = _rma.Update(new TValue(DateTime.UtcNow.AddMinutes(i), tr), isNew: true);
            _atrBuffer.Add(atr.Value);
        }

        if (_atrBuffer.Count > 0)
        {
            double currentAtr = _atrBuffer[^1];
            double maxAtr = GetMax();
            double minAtr = GetMin();
            double normalized = minAtr < maxAtr ? (currentAtr - minAtr) / (maxAtr - minAtr) : 0.5;
            Last = new TValue(DateTime.UtcNow, normalized);
        }
    }

    /// <summary>
    /// Resets the ATRN state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _rma.Reset();
        _atrBuffer.Clear();
        _state = new State(PrevBar: default, IsInitialized: false, LastValidTr: 0.0, LastValidAtr: 0.0);
        _p_state = _state;
        Last = default;
    }

    /// <summary>
    /// Updates ATRN with a new bar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _atrBuffer.Snapshot();
        }
        else
        {
            _state = _p_state;
            _atrBuffer.Restore();
        }

        // Calculate True Range FIRST (before RMA update for bar correction)
        double tr;
        if (!_state.IsInitialized)
        {
            // First bar: TR = High - Low
            tr = input.High - input.Low;
        }
        else
        {
            double hl = input.High - input.Low;
            double hpc = Math.Abs(input.High - _state.PrevBar.Close);
            double lpc = Math.Abs(input.Low - _state.PrevBar.Close);
            tr = Math.Max(hl, Math.Max(hpc, lpc));
        }

        // Handle non-finite values
        if (!double.IsFinite(tr))
        {
            tr = _state.LastValidTr;
        }

        // Calculate ATR using RMA (now uses freshly computed TR for both new and correction paths)
        TValue atrResult = _rma.Update(new TValue(input.Time, tr), isNew);
        double currentAtr = atrResult.Value;

        // Handle non-finite ATR
        if (!double.IsFinite(currentAtr))
        {
            currentAtr = _state.LastValidAtr;
        }

        // Add to buffer for min-max calculation
        _atrBuffer.Add(currentAtr);

        // Calculate normalized value
        double maxAtr = GetMax();
        double minAtr = GetMin();
        double normalized = minAtr < maxAtr ? (currentAtr - minAtr) / (maxAtr - minAtr) : 0.5;

        // Update state
        _state = isNew
            ? new State(PrevBar: input, IsInitialized: true, LastValidTr: tr, LastValidAtr: currentAtr)
            : _state with { LastValidTr = tr, LastValidAtr = currentAtr };

        TValue result = new(input.Time, normalized);
        Last = result;
        PubEvent(Last, isNew);
        return result;
    }

    /// <summary>
    /// Updates ATRN with a TValue input.
    /// This treats the input value as the TR itself.
    /// </summary>
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _atrBuffer.Snapshot();
        }
        else
        {
            _state = _p_state;
            _atrBuffer.Restore();
        }

        double tr = input.Value;
        if (!double.IsFinite(tr))
        {
            tr = _state.LastValidTr;
        }

        TValue atrResult = _rma.Update(new TValue(input.Time, tr), isNew);
        double currentAtr = atrResult.Value;

        if (!double.IsFinite(currentAtr))
        {
            currentAtr = _state.LastValidAtr;
        }

        _atrBuffer.Add(currentAtr);

        double maxAtr = GetMax();
        double minAtr = GetMin();
        double normalized = minAtr < maxAtr ? (currentAtr - minAtr) / (maxAtr - minAtr) : 0.5;

        _state = _state with { LastValidTr = tr, LastValidAtr = currentAtr };

        TValue result = new(input.Time, normalized);
        Last = result;
        PubEvent(Last, isNew);
        return result;
    }

    /// <summary>
    /// Updates ATRN from a TBarSeries.
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
            TValue result = Update(source[i], isNew: true);
            t.Add(result.Time);
            v.Add(result.Value);
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Updates ATRN from a TSeries (assumes values are already TR).
    /// </summary>
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        for (int i = 0; i < source.Count; i++)
        {
            TValue result = Update(source[i], isNew: true);
            t.Add(source[i].Time);
            v.Add(result.Value);
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates ATRN for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period)
    {
        var atrn = new Atrn(period);
        return atrn.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetMax()
    {
        ReadOnlySpan<double> span = _atrBuffer.GetSpan();
        if (span.IsEmpty)
        {
            return 0;
        }

        double max = double.MinValue;
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] > max)
            {
                max = span[i];
            }
        }
        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetMin()
    {
        ReadOnlySpan<double> span = _atrBuffer.GetSpan();
        if (span.IsEmpty)
        {
            return 0;
        }

        double min = double.MaxValue;
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] < min)
            {
                min = span[i];
            }
        }
        return min;
    }
}
