using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SSF-DSP: SSF-Based Detrended Synthetic Price - Ehlers' oscillator that removes trend
/// from price using dual Super Smooth Filters with quarter-cycle and half-cycle periods.
/// </summary>
/// <remarks>
/// The SSF-based Detrended Synthetic Price indicator creates a synthetic price series
/// that oscillates around zero by subtracting a half-cycle SSF from a quarter-cycle SSF.
/// Unlike the EMA-based DSP, this version uses Super Smooth Filters which provide
/// better smoothing characteristics with minimal lag.
///
/// Formula:
/// fast_period = max(2, round(period / 4))
/// slow_period = max(3, round(period / 2))
/// arg = sqrt(2) * PI / period
/// c1 = 1 - c2 - c3
/// c2 = 2 * exp(-arg) * cos(arg)
/// c3 = -exp(-arg)^2
/// input = (price + price[1]) / 2
/// SSF = c1 * input + c2 * SSF[1] + c3 * SSF[2]
/// SSF-DSP = SSF_fast - SSF_slow
///
/// Properties:
/// - Oscillates around zero
/// - Removes trend to highlight cycles
/// - Super Smooth Filter provides better noise rejection than EMA
/// - Quarter-cycle SSF responds quickly to price changes
/// - Half-cycle SSF provides the trend reference
/// - Crossings above zero indicate bullish momentum
/// - Crossings below zero indicate bearish momentum
///
/// Key Insight:
/// The Super Smooth Filter is a 2-pole Butterworth-style IIR filter that
/// provides excellent smoothing with zero lag at the cutoff frequency.
/// </remarks>
[SkipLocalsInit]
public sealed class Ssfdsp : AbstractBase
{
    private readonly double _c1Fast, _c2Fast, _c3Fast;
    private readonly double _c1Slow, _c2Slow, _c3Slow;
    private readonly int _slowPeriod;

    // State record for snapshot/restore
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SsfFast1,
        double SsfFast2,
        double SsfSlow1,
        double SsfSlow2,
        double PrevInput,
        int Count,
        double LastValidValue
    );

    private State _s;
    private State _ps;

    public override bool IsHot => _s.Count >= _slowPeriod * 2;

    /// <summary>
    /// Creates a new SSF-based Detrended Synthetic Price indicator.
    /// </summary>
    /// <param name="period">The dominant cycle period (must be >= 4).</param>
    public Ssfdsp(int period = 40)
    {
        if (period < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be at least 4.");
        }

        // Calculate fast (quarter-cycle) and slow (half-cycle) periods
        int fastPeriod = Math.Max(2, (int)Math.Round(period / 4.0));
        _slowPeriod = Math.Max(3, (int)Math.Round(period / 2.0));

        // Precompute SSF coefficients: sqrt(2) * PI / period
        double sqrt2Pi = Math.Sqrt(2.0) * Math.PI;

        // Fast SSF coefficients
        double argFast = sqrt2Pi / fastPeriod;
        double expFast = Math.Exp(-argFast);
        _c2Fast = 2.0 * expFast * Math.Cos(argFast);
        _c3Fast = -expFast * expFast;
        _c1Fast = 1.0 - _c2Fast - _c3Fast;

        // Slow SSF coefficients
        double argSlow = sqrt2Pi / _slowPeriod;
        double expSlow = Math.Exp(-argSlow);
        _c2Slow = 2.0 * expSlow * Math.Cos(argSlow);
        _c3Slow = -expSlow * expSlow;
        _c1Slow = 1.0 - _c2Slow - _c3Slow;

        Name = $"SsfDsp({period})";
        WarmupPeriod = _slowPeriod * 2;

        // Initialize state
        _s = new State(0, 0, 0, 0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Creates a chained SSF-based Detrended Synthetic Price indicator.
    /// </summary>
    /// <param name="source">The source indicator to chain from.</param>
    /// <param name="period">The dominant cycle period.</param>
    public Ssfdsp(ITValuePublisher source, int period = 40) : this(period)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Pub += HandleInput;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleInput(object? sender, in TValueEventArgs e)
    {
        Update(e.Value, e.IsNew);
    }

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

        // Handle non-finite values
        double value = input.Value;
        if (!double.IsFinite(value))
        {
            value = s.LastValidValue;
        }
        else
        {
            s = s with { LastValidValue = value };
        }

        // SSF uses averaged input: (current + previous) / 2
        double avgInput = (value + s.PrevInput) * 0.5;

        // Initialize on first values
        double ssfFast, ssfSlow;
        if (s.Count == 0)
        {
            // First bar: initialize all SSF values to input
            ssfFast = avgInput;
            ssfSlow = avgInput;
            s = s with { SsfFast1 = avgInput, SsfFast2 = avgInput, SsfSlow1 = avgInput, SsfSlow2 = avgInput };
        }
        else if (s.Count == 1)
        {
            // Second bar: use simple average
            ssfFast = avgInput;
            ssfSlow = avgInput;
            s = s with { SsfFast2 = s.SsfFast1, SsfFast1 = avgInput, SsfSlow2 = s.SsfSlow1, SsfSlow1 = avgInput };
        }
        else
        {
            // Apply SSF recursion: SSF = c1*input + c2*SSF[1] + c3*SSF[2]
            ssfFast = Math.FusedMultiplyAdd(_c1Fast, avgInput, Math.FusedMultiplyAdd(_c2Fast, s.SsfFast1, _c3Fast * s.SsfFast2));
            ssfSlow = Math.FusedMultiplyAdd(_c1Slow, avgInput, Math.FusedMultiplyAdd(_c2Slow, s.SsfSlow1, _c3Slow * s.SsfSlow2));

            s = s with { SsfFast2 = s.SsfFast1, SsfFast1 = ssfFast, SsfSlow2 = s.SsfSlow1, SsfSlow1 = ssfSlow };
        }

        // SSF-DSP = fast SSF - slow SSF
        double ssfdsp = ssfFast - ssfSlow;

        // Update state
        _s = s with { PrevInput = value, Count = s.Count + 1 };

        Last = new TValue(input.Time, ssfdsp);
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

        // Single pass: advance state and fill output in one iteration
        int i = 0;
        foreach (var tv in source)
        {
            var result = Update(tv);
            tSpan[i] = tv.Time;
            vSpan[i] = result.Value;
            i++;
        }

        return new TSeries(t, v);
    }

    public override void Reset()
    {
        _s = new State(0, 0, 0, 0, 0, 0, 0);
        _ps = _s;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.UtcNow, value));
        }
    }

    /// <summary>
    /// Calculates SSF-DSP for a time series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 40)
    {
        var ssfdsp = new Ssfdsp(period);
        return ssfdsp.Update(source);
    }

    /// <summary>
    /// Calculates SSF-DSP in-place using a pre-allocated output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 40)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be at least 4.");
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Calculate fast (quarter-cycle) and slow (half-cycle) periods
        int fastPeriod = Math.Max(2, (int)Math.Round(period / 4.0));
        int slowPeriod = Math.Max(3, (int)Math.Round(period / 2.0));

        // Precompute SSF coefficients
        double sqrt2Pi = Math.Sqrt(2.0) * Math.PI;

        double argFast = sqrt2Pi / fastPeriod;
        double expFast = Math.Exp(-argFast);
        double c2Fast = 2.0 * expFast * Math.Cos(argFast);
        double c3Fast = -expFast * expFast;
        double c1Fast = 1.0 - c2Fast - c3Fast;

        double argSlow = sqrt2Pi / slowPeriod;
        double expSlow = Math.Exp(-argSlow);
        double c2Slow = 2.0 * expSlow * Math.Cos(argSlow);
        double c3Slow = -expSlow * expSlow;
        double c1Slow = 1.0 - c2Slow - c3Slow;

        double ssfFast1 = 0, ssfFast2 = 0;
        double ssfSlow1 = 0, ssfSlow2 = 0;
        double prevInput = 0;
        double lastValid = 0;

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

            // SSF uses averaged input
            double avgInput = (val + prevInput) * 0.5;
            prevInput = val;

            double ssfFast, ssfSlow;
            if (i == 0)
            {
                ssfFast = avgInput;
                ssfSlow = avgInput;
                ssfFast1 = ssfFast2 = avgInput;
                ssfSlow1 = ssfSlow2 = avgInput;
            }
            else if (i == 1)
            {
                ssfFast = avgInput;
                ssfSlow = avgInput;
                ssfFast2 = ssfFast1;
                ssfFast1 = avgInput;
                ssfSlow2 = ssfSlow1;
                ssfSlow1 = avgInput;
            }
            else
            {
                ssfFast = Math.FusedMultiplyAdd(c1Fast, avgInput, Math.FusedMultiplyAdd(c2Fast, ssfFast1, c3Fast * ssfFast2));
                ssfSlow = Math.FusedMultiplyAdd(c1Slow, avgInput, Math.FusedMultiplyAdd(c2Slow, ssfSlow1, c3Slow * ssfSlow2));

                ssfFast2 = ssfFast1;
                ssfFast1 = ssfFast;
                ssfSlow2 = ssfSlow1;
                ssfSlow1 = ssfSlow;
            }

            output[i] = ssfFast - ssfSlow;
        }
    }

    public static (TSeries Results, Ssfdsp Indicator) Calculate(TSeries source, int period = 40)
    {
        var indicator = new Ssfdsp(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}