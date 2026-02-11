// Rogers-Satchell Volatility (RSV) Indicator
// A drift-adjusted OHLC volatility estimator using SMA smoothing

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RSV: Rogers-Satchell Volatility
/// A drift-adjusted volatility estimator that uses all four OHLC prices,
/// providing more accurate estimates in trending markets than range-based methods.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>Calculate log ratios: term1=ln(H/O), term2=ln(H/C), term3=ln(L/O), term4=ln(L/C)</item>
/// <item>rsVariance = (term1 × term2) + (term3 × term4)</item>
/// <item>Smooth using Simple Moving Average (SMA)</item>
/// <item>volatility = √(max(0, smoothedVariance))</item>
/// <item>If annualize: volatility × √(annualPeriods)</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>Uses all OHLC data for drift adjustment</item>
/// <item>SMA smoothing for stability</item>
/// <item>Optional annualization (default 252 trading days)</item>
/// <item>Handles trending markets better than Parkinson/GK</item>
/// </list>
///
/// <b>Sources:</b>
/// Rogers, L.C.G. and Satchell, S.E. (1991). "Estimating Variance from High, Low and Closing Prices."
/// Annals of Applied Probability, 1(4), 504-512.
/// </remarks>
[SkipLocalsInit]
public sealed class Rsv : AbstractBase
{
    private const double Epsilon = 1e-10;

    private readonly int _period;
    private readonly bool _annualize;
    private readonly int _annualPeriods;
    private readonly double _annualFactor;

    // Circular buffer for SMA
    private readonly double[] _buffer;
    private readonly double[] _bufferSnapshot;

    // Event source for disposal
    private readonly ITValuePublisher? _source;
    private bool _disposed;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Sum,
        double LastValidRsVar,
        double LastValue,
        int Count,
        int BufferIdx
    );
    private State _s;
    private State _ps;

    /// <summary>
    /// Initializes a new instance of the Rsv class.
    /// </summary>
    /// <param name="period">The smoothing period (default 20).</param>
    /// <param name="annualize">Whether to annualize the volatility (default true).</param>
    /// <param name="annualPeriods">Number of periods per year (default 252).</param>
    /// <exception cref="ArgumentException">
    /// Thrown when period is less than 1, or annualPeriods is less than 1 when annualizing.
    /// </exception>
    public Rsv(int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (annualize && annualPeriods <= 0)
        {
            throw new ArgumentException("Annual periods must be greater than 0 when annualizing", nameof(annualPeriods));
        }
        _period = period;
        _annualize = annualize;
        _annualPeriods = annualPeriods;
        _annualFactor = annualize ? Math.Sqrt(annualPeriods) : 1.0;
        _buffer = new double[period];
        _bufferSnapshot = new double[period];
        WarmupPeriod = period;
        Name = $"Rsv({period})";
        _s = new State(0, 0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Initializes a new instance of the Rsv class with a source.
    /// </summary>
    /// <param name="source">The data source for chaining.</param>
    /// <param name="period">The smoothing period (default 20).</param>
    /// <param name="annualize">Whether to annualize the volatility (default true).</param>
    /// <param name="annualPeriods">Number of periods per year (default 252).</param>
    public Rsv(ITValuePublisher source, int period = 20, bool annualize = true, int annualPeriods = 252)
        : this(period, annualize, annualPeriods)
    {
        _source = source;
        _source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>
    /// The smoothing period.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Whether volatility is annualized.
    /// </summary>
    public bool Annualize => _annualize;

    /// <summary>
    /// Number of periods per year for annualization.
    /// </summary>
    public int AnnualPeriods => _annualPeriods;

    /// <summary>
    /// Computes the Rogers-Satchell variance for a single bar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeRsVariance(double open, double high, double low, double close)
    {
        // Protect against zero/negative prices
        double o = Math.Max(open, Epsilon);
        double h = Math.Max(high, Epsilon);
        double l = Math.Max(low, Epsilon);
        double c = Math.Max(close, Epsilon);

        double term1 = Math.Log(h / o);
        double term2 = Math.Log(h / c);
        double term3 = Math.Log(l / o);
        double term4 = Math.Log(l / c);

        // rs_variance = (term1 * term2) + (term3 * term4)
        return Math.FusedMultiplyAdd(term1, term2, term3 * term4);
    }

    /// <summary>
    /// Updates the indicator with a TValue input.
    /// For RSV, this treats the value as a pre-computed RS variance.
    /// Prefer Update(TBar) for standard OHLC data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return UpdateCore(input.Time, input.Value, isNew);
    }

    /// <summary>
    /// Updates the indicator with a new bar (preferred method).
    /// </summary>
    /// <param name="bar">The input bar.</param>
    /// <param name="isNew">Whether this is a new bar or an update.</param>
    /// <returns>The calculated volatility value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        // Handle invalid OHLC data
        if (!double.IsFinite(bar.Open) || !double.IsFinite(bar.High) ||
            !double.IsFinite(bar.Low) || !double.IsFinite(bar.Close) ||
            bar.Open <= 0 || bar.High <= 0 || bar.Low <= 0 || bar.Close <= 0)
        {
            // Pass NaN to trigger last-valid-value substitution
            return UpdateCore(bar.Time, double.NaN, isNew);
        }

        double rsVariance = ComputeRsVariance(bar.Open, bar.High, bar.Low, bar.Close);
        return UpdateCore(bar.Time, rsVariance, isNew);
    }

    /// <summary>
    /// Updates the indicator with a bar series.
    /// </summary>
    /// <param name="source">The source bar series.</param>
    /// <returns>A TSeries containing the volatility values.</returns>
    public TSeries Update(TBarSeries source)
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

        // Extract OHLC data
        Span<double> opens = len <= 128 ? stackalloc double[len] : new double[len];
        Span<double> highs = len <= 128 ? stackalloc double[len] : new double[len];
        Span<double> lows = len <= 128 ? stackalloc double[len] : new double[len];
        Span<double> closes = len <= 128 ? stackalloc double[len] : new double[len];

        for (int i = 0; i < len; i++)
        {
            opens[i] = source[i].Open;
            highs[i] = source[i].High;
            lows[i] = source[i].Low;
            closes[i] = source[i].Close;
            tSpan[i] = source[i].Time;
        }

        Batch(opens, highs, lows, closes, vSpan, _period, _annualize, _annualPeriods);

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(t, v);
    }

    /// <inheritdoc/>
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

        // Treat source values as pre-computed RS variances
        BatchFromVariances(source.Values, vSpan, _period, _annualize, _annualPeriods);
        source.Times.CopyTo(tSpan);

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(long timeTicks, double rsVariance, bool isNew)
    {
        if (isNew)
        {
            _ps = _s;
            // Snapshot buffer state for potential rollback
            Array.Copy(_buffer, _bufferSnapshot, _period);
        }
        else
        {
            _s = _ps;
            // Restore buffer from snapshot
            Array.Copy(_bufferSnapshot, _buffer, _period);
        }

        var s = _s;

        // Handle non-finite variance - use last valid value
        if (!double.IsFinite(rsVariance))
        {
            rsVariance = s.LastValidRsVar;
        }
        else
        {
            s.LastValidRsVar = rsVariance;
        }

        // SMA with circular buffer
        double sum = s.Sum;
        int bufferIdx = s.BufferIdx;
        int count = s.Count;

        // Both isNew=true and isNew=false follow the same calculation logic after state restore:
        // - If count >= period, remove the old value at bufferIdx from sum
        // - Add new value to sum
        // - Write new value to buffer[bufferIdx]
        // - Increment bufferIdx and count
        // The only difference: isNew=true also saves state to _ps before processing

        if (count >= _period)
        {
            // Remove oldest value from sum (the value at current bufferIdx position)
            sum -= _buffer[bufferIdx];
        }

        // Add new value to sum and buffer
        sum += rsVariance;
        _buffer[bufferIdx] = rsVariance;

        // Always advance the buffer position and count
        bufferIdx = (bufferIdx + 1) % _period;
        count++;

        // Calculate SMA
        int effectiveCount = Math.Min(count, _period);
        double smaVariance = effectiveCount > 0 ? sum / effectiveCount : 0;

        // Calculate volatility: sqrt(max(0, smaVariance))
        double volatility = smaVariance > 0 ? Math.Sqrt(smaVariance) * _annualFactor : 0;

        if (!double.IsFinite(volatility))
        {
            volatility = s.LastValue;
        }

        // Update state - always update _s with the new values
        s.Sum = sum;
        s.BufferIdx = bufferIdx;
        s.Count = count;
        s.LastValue = volatility;

        _s = s;

        Last = new TValue(timeTicks, volatility);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _s = new State(0, 0, 0, 0, 0);
        _ps = _s;
        Array.Clear(_buffer);
        Array.Clear(_bufferSnapshot);
        Last = default;
    }

    /// <summary>
    /// Releases resources and unsubscribes from the event source.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source is not null)
            {
                _source.Pub -= Handle;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Calculates Rogers-Satchell Volatility for a bar series (static).
    /// </summary>
    /// <param name="source">The source bar series.</param>
    /// <param name="period">The smoothing period.</param>
    /// <param name="annualize">Whether to annualize.</param>
    /// <param name="annualPeriods">Periods per year.</param>
    /// <returns>A TSeries containing the volatility values.</returns>
    public static TSeries Batch(TBarSeries source, int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        var rsv = new Rsv(period, annualize, annualPeriods);
        return rsv.Update(source);
    }

    /// <summary>
    /// Calculates RSV for a TSeries (treats values as pre-computed RS variances).
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (annualize && annualPeriods <= 0)
        {
            throw new ArgumentException("Annual periods must be greater than 0 when annualizing", nameof(annualPeriods));
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        BatchFromVariances(source.Values, vSpan, period, annualize, annualPeriods);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch calculation using spans for OHLC data.
    /// </summary>
    /// <param name="open">Open prices.</param>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output volatility values.</param>
    /// <param name="period">The smoothing period.</param>
    /// <param name="annualize">Whether to annualize.</param>
    /// <param name="annualPeriods">Periods per year.</param>
    public static void Batch(
        ReadOnlySpan<double> open,
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int period = 20,
        bool annualize = true,
        int annualPeriods = 252)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (annualize && annualPeriods <= 0)
        {
            throw new ArgumentException("Annual periods must be greater than 0 when annualizing", nameof(annualPeriods));
        }

        int len = open.Length;
        if (high.Length != len || low.Length != len || close.Length != len)
        {
            throw new ArgumentException("All OHLC spans must have the same length", nameof(close));
        }
        if (output.Length < len)
        {
            throw new ArgumentException("Output span must be at least as long as input spans", nameof(output));
        }

        if (len == 0)
        {
            return;
        }

        double annualFactor = annualize ? Math.Sqrt(annualPeriods) : 1.0;

        // SMA circular buffer
        Span<double> buffer = period <= 256 ? stackalloc double[period] : new double[period];
        double sum = 0;
        int bufferIdx = 0;
        double lastValidRsVar = 0;
        double lastValue = 0;

        for (int i = 0; i < len; i++)
        {
            double o = open[i];
            double h = high[i];
            double l = low[i];
            double c = close[i];

            double rsVariance;

            // Handle invalid data
            if (!double.IsFinite(o) || !double.IsFinite(h) ||
                !double.IsFinite(l) || !double.IsFinite(c) ||
                o <= 0 || h <= 0 || l <= 0 || c <= 0)
            {
                rsVariance = lastValidRsVar;
            }
            else
            {
                rsVariance = ComputeRsVariance(o, h, l, c);
                if (!double.IsFinite(rsVariance))
                {
                    rsVariance = lastValidRsVar;
                }
                else
                {
                    lastValidRsVar = rsVariance;
                }
            }

            // SMA update
            if (i >= period)
            {
                sum -= buffer[bufferIdx];
            }
            sum += rsVariance;
            buffer[bufferIdx] = rsVariance;
            bufferIdx = (bufferIdx + 1) % period;

            int effectiveCount = Math.Min(i + 1, period);
            double smaVariance = sum / effectiveCount;

            double volatility = smaVariance > 0 ? Math.Sqrt(smaVariance) * annualFactor : 0;

            if (!double.IsFinite(volatility))
            {
                volatility = lastValue;
            }
            else
            {
                lastValue = volatility;
            }

            output[i] = volatility;
        }
    }

    public static (TSeries Results, Rsv Indicator) Calculate(TBarSeries source, int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        var indicator = new Rsv(period, annualize, annualPeriods);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }


    /// <summary>
    /// Batch calculation from pre-computed RS variances.
    /// </summary>
    private static void BatchFromVariances(
        ReadOnlySpan<double> variances,
        Span<double> output,
        int period,
        bool annualize,
        int annualPeriods)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (variances.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        int len = variances.Length;
        if (len == 0)
        {
            return;
        }

        double annualFactor = annualize ? Math.Sqrt(annualPeriods) : 1.0;

        // SMA circular buffer
        Span<double> buffer = period <= 256 ? stackalloc double[period] : new double[period];
        double sum = 0;
        int bufferIdx = 0;
        double lastValidRsVar = 0;
        double lastValue = 0;

        for (int i = 0; i < len; i++)
        {
            double rsVariance = variances[i];

            if (!double.IsFinite(rsVariance))
            {
                rsVariance = lastValidRsVar;
            }
            else
            {
                lastValidRsVar = rsVariance;
            }

            // SMA update
            if (i >= period)
            {
                sum -= buffer[bufferIdx];
            }
            sum += rsVariance;
            buffer[bufferIdx] = rsVariance;
            bufferIdx = (bufferIdx + 1) % period;

            int effectiveCount = Math.Min(i + 1, period);
            double smaVariance = sum / effectiveCount;

            double volatility = smaVariance > 0 ? Math.Sqrt(smaVariance) * annualFactor : 0;

            if (!double.IsFinite(volatility))
            {
                volatility = lastValue;
            }
            else
            {
                lastValue = volatility;
            }

            output[i] = volatility;
        }
    }
}
