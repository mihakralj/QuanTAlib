// TRIX: Triple Exponential Average Oscillator
// Percentage rate of change of triple-smoothed EMA with warmup compensation.
// Formula: TRIX = 100 * (EMA3 - EMA3[1]) / EMA3[1]
// Source: Jack Hutson, "Technical Analysis of Stocks & Commodities" (1983)

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TRIX: Triple Exponential Average Oscillator
/// </summary>
/// <remarks>
/// The TRIX indicator calculates the percentage rate of change of a triple-smoothed
/// exponential moving average. By applying EMA three times and then taking the ROC,
/// TRIX filters out insignificant price movements and highlights the underlying trend.
///
/// Calculation:
/// 1. EMA1 = EMA(source, period) with warmup compensation
/// 2. EMA2 = EMA(EMA1, period) with warmup compensation
/// 3. EMA3 = EMA(EMA2, period) with warmup compensation
/// 4. TRIX = 100 * (EMA3 - EMA3[previous]) / EMA3[previous]
///
/// Key Features:
/// - Triple smoothing eliminates short-term noise
/// - Oscillates around zero (positive = uptrend, negative = downtrend)
/// - Leading indicator for trend changes via zero-line crossovers
///
/// Sources:
/// - Jack Hutson, "Technical Analysis of Stocks & Commodities" (1983)
/// - https://www.investopedia.com/terms/t/trix.asp
/// </remarks>
[SkipLocalsInit]
public sealed class Trix : AbstractBase
{
    private const int DefaultPeriod = 14;
    private readonly int _period;
    private readonly double _alpha;
    private readonly double _decay;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Rema1,
        double Rema2,
        double Rema3,
        double E1,
        double E2,
        double E3,
        double PrevEma3,
        int Count,
        double LastValid);

    private State _s;
    private State _ps;

    /// <summary>
    /// True when enough bars have been processed for valid TRIX output.
    /// TRIX applies triple EMA smoothing, so requires 3× period bars to converge.
    /// </summary>
    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>
    /// Period of the indicator.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Creates TRIX with specified period.
    /// </summary>
    /// <param name="period">Period for triple exponential smoothing (must be &gt; 0)</param>
    public Trix(int period = DefaultPeriod)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _alpha = 2.0 / (period + 1);
        _decay = 1.0 - _alpha;
        _s = new State(0, 0, 0, 1.0, 1.0, 1.0, 0, 0, 0);
        _ps = _s;
        Name = $"Trix({period})";
        WarmupPeriod = period * 3;
    }

    /// <summary>
    /// Creates TRIX with source subscription and specified period.
    /// </summary>
    public Trix(ITValuePublisher source, int period = DefaultPeriod) : this(period)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        double value = input.Value;
        if (!double.IsFinite(value))
        {
            value = double.IsFinite(s.LastValid) ? s.LastValid : 0.0;
        }
        else
        {
            s.LastValid = value;
        }

        if (isNew)
        {
            s.Count++;
        }

        // Triple EMA with warmup compensation (from PineScript)
        double ema1, ema2, ema3;

        if (s.Count == 1)
        {
            // First bar: initialize
            s.Rema1 = value;
            s.Rema2 = value;
            s.Rema3 = value;
            s.PrevEma3 = value;
            ema3 = value;
        }
        else
        {
            // EMA1: smooth source
            s.Rema1 = Math.FusedMultiplyAdd(s.Rema1, _decay, _alpha * value);

            if (s.E1 > 1e-10)
            {
                // Warmup: compensate for initial bias
                s.E1 *= _decay;
                ema1 = s.Rema1 / (1.0 - s.E1);
            }
            else
            {
                ema1 = s.Rema1;
            }

            // EMA2: smooth EMA1
            s.Rema2 = Math.FusedMultiplyAdd(s.Rema2, _decay, _alpha * ema1);

            if (s.E2 > 1e-10)
            {
                s.E2 *= _decay;
                ema2 = s.Rema2 / (1.0 - s.E2);
            }
            else
            {
                ema2 = s.Rema2;
            }

            // EMA3: smooth EMA2
            s.Rema3 = Math.FusedMultiplyAdd(s.Rema3, _decay, _alpha * ema2);

            if (s.E3 > 1e-10)
            {
                s.E3 *= _decay;
                ema3 = s.Rema3 / (1.0 - s.E3);
            }
            else
            {
                ema3 = s.Rema3;
            }
        }

        // TRIX = 100 * (EMA3 - prev_EMA3) / prev_EMA3
        double trix = Math.Abs(s.PrevEma3) > 1e-10
            ? 100.0 * (ema3 - s.PrevEma3) / s.PrevEma3
            : 0.0;

        if (isNew)
        {
            s.PrevEma3 = ema3;
        }

        _s = s;

        Last = new TValue(input.Time, trix);
        PubEvent(Last, isNew);
        return Last;
    }
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Restore streaming state by replaying
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }
    public override void Reset()
    {
        _s = new State(0, 0, 0, 1.0, 1.0, 1.0, 0, 0, 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Calculates TRIX for entire series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = DefaultPeriod)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, period);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch TRIX calculation using triple EMA with warmup compensation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = DefaultPeriod)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double alpha = 2.0 / (period + 1);
        double decay = 1.0 - alpha;
        double lastValid = 0.0;

        double rema1 = 0, rema2 = 0, rema3 = 0;
        double e1 = 1.0, e2 = 1.0, e3 = 1.0;
        double prevEma3 = 0;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];

            if (!double.IsFinite(val))
            {
                val = lastValid;
            }
            else
            {
                lastValid = val;
            }

            double ema3;

            if (i == 0)
            {
                rema1 = val;
                rema2 = val;
                rema3 = val;
                prevEma3 = val;
                ema3 = val;
            }
            else
            {
                // EMA1
                rema1 = Math.FusedMultiplyAdd(rema1, decay, alpha * val);
                double ema1;

                if (e1 > 1e-10)
                {
                    e1 *= decay;
                    ema1 = rema1 / (1.0 - e1);
                }
                else
                {
                    ema1 = rema1;
                }

                // EMA2
                rema2 = Math.FusedMultiplyAdd(rema2, decay, alpha * ema1);
                double ema2;

                if (e2 > 1e-10)
                {
                    e2 *= decay;
                    ema2 = rema2 / (1.0 - e2);
                }
                else
                {
                    ema2 = rema2;
                }

                // EMA3
                rema3 = Math.FusedMultiplyAdd(rema3, decay, alpha * ema2);

                if (e3 > 1e-10)
                {
                    e3 *= decay;
                    ema3 = rema3 / (1.0 - e3);
                }
                else
                {
                    ema3 = rema3;
                }
            }

            // TRIX = 100 * (EMA3 - prev_EMA3) / prev_EMA3
            output[i] = Math.Abs(prevEma3) > 1e-10
                ? 100.0 * (ema3 - prevEma3) / prevEma3
                : 0.0;

            prevEma3 = ema3;
        }
    }

    /// <summary>
    /// Creates TRIX indicator and calculates results for the source series.
    /// </summary>
    public static (TSeries Results, Trix Indicator) Calculate(TSeries source, int period = DefaultPeriod)
    {
        var indicator = new Trix(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
