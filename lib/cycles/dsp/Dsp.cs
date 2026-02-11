using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DSP: Detrended Synthetic Price - Ehlers' oscillator that removes trend from price
/// using dual EMA algorithm with quarter-cycle and half-cycle periods.
/// </summary>
/// <remarks>
/// The Detrended Synthetic Price indicator, developed by John Ehlers, creates a
/// synthetic price series that oscillates around zero by subtracting a half-cycle
/// EMA from a quarter-cycle EMA. This effectively removes the trend component
/// and highlights the cyclical behavior.
///
/// Formula:
/// fast_period = max(2, round(period / 4))
/// slow_period = max(3, round(period / 2))
/// alpha_fast = 2 / (fast_period + 1)
/// alpha_slow = 2 / (slow_period + 1)
/// ema_fast = ema_fast + alpha_fast * (price - ema_fast)
/// ema_slow = ema_slow + alpha_slow * (price - ema_slow)
/// DSP = ema_fast - ema_slow
///
/// Properties:
/// - Oscillates around zero
/// - Removes trend to highlight cycles
/// - Quarter-cycle EMA responds quickly to price changes
/// - Half-cycle EMA provides the trend reference
/// - Crossings above zero indicate bullish momentum
/// - Crossings below zero indicate bearish momentum
///
/// Key Insight:
/// By using period fractions (1/4 and 1/2), the indicator naturally adapts to
/// the dominant cycle period in the data, providing better cycle isolation.
/// </remarks>
[SkipLocalsInit]
public sealed class Dsp : AbstractBase
{
    private readonly double _alphaFast;
    private readonly double _alphaSlow;
    private readonly double _decayFast;
    private readonly double _decaySlow;

    // State record for snapshot/restore
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private record struct State(
        double EmaFastRaw,
        double EmaSlowRaw,
        double EFast,
        double ESlow,
        bool InWarmup,
        double LastValidValue
    );

    private State _s;
    private State _ps;

    public override bool IsHot => !_s.InWarmup;

    /// <summary>
    /// Creates a new Detrended Synthetic Price indicator.
    /// </summary>
    /// <param name="period">The dominant cycle period (must be >= 4).</param>
    public Dsp(int period = 40)
    {
        if (period < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be at least 4.");
        }

        // Calculate fast (quarter-cycle) and slow (half-cycle) periods
        int fastPeriod = Math.Max(2, (int)Math.Round(period / 4.0));
        int slowPeriod = Math.Max(3, (int)Math.Round(period / 2.0));

        _alphaFast = 2.0 / (fastPeriod + 1);
        _alphaSlow = 2.0 / (slowPeriod + 1);
        _decayFast = 1.0 - _alphaFast;
        _decaySlow = 1.0 - _alphaSlow;

        Name = $"Dsp({period})";
        WarmupPeriod = slowPeriod * 3; // EMAs need time to stabilize

        // Initialize state
        _s = new State(0, 0, 1.0, 1.0, true, 0);
        _ps = _s;
    }

    /// <summary>
    /// Creates a chained Detrended Synthetic Price indicator.
    /// </summary>
    /// <param name="source">The source indicator to chain from.</param>
    /// <param name="period">The dominant cycle period.</param>
    public Dsp(ITValuePublisher source, int period = 40) : this(period)
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

        // Update raw EMAs using FMA pattern
        double emaFastRaw = Math.FusedMultiplyAdd(s.EmaFastRaw, _decayFast, _alphaFast * value);
        double emaSlowRaw = Math.FusedMultiplyAdd(s.EmaSlowRaw, _decaySlow, _alphaSlow * value);

        // Bias correction during warmup
        double eFast = s.EFast * _decayFast;
        double eSlow = s.ESlow * _decaySlow;

        double emaFast, emaSlow;
        bool inWarmup = eSlow > 0.05;  // Warmup based on slower EMA's bias correction factor

        if (inWarmup)
        {
            double cFast = 1.0 / (1.0 - eFast);
            double cSlow = 1.0 / (1.0 - eSlow);
            emaFast = cFast * emaFastRaw;
            emaSlow = cSlow * emaSlowRaw;
        }
        else
        {
            emaFast = emaFastRaw;
            emaSlow = emaSlowRaw;
        }

        // DSP = fast EMA - slow EMA
        double dsp = emaFast - emaSlow;

        // Update state
        _s = new State(emaFastRaw, emaSlowRaw, eFast, eSlow, inWarmup, s.LastValidValue);

        Last = new TValue(input.Time, dsp);
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
        _s = new State(0, 0, 1.0, 1.0, true, 0);
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
    /// Calculates DSP for a time series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 40)
    {
        var dsp = new Dsp(period);
        return dsp.Update(source);
    }

    /// <summary>
    /// Calculates DSP in-place using a pre-allocated output span.
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

        double alphaFast = 2.0 / (fastPeriod + 1);
        double alphaSlow = 2.0 / (slowPeriod + 1);
        double decayFast = 1.0 - alphaFast;
        double decaySlow = 1.0 - alphaSlow;

        double emaFastRaw = 0;
        double emaSlowRaw = 0;
        double eFast = 1.0;
        double eSlow = 1.0;
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

            // Update raw EMAs
            emaFastRaw = Math.FusedMultiplyAdd(emaFastRaw, decayFast, alphaFast * val);
            emaSlowRaw = Math.FusedMultiplyAdd(emaSlowRaw, decaySlow, alphaSlow * val);

            // Bias correction
            eFast *= decayFast;
            eSlow *= decaySlow;

            double emaFast, emaSlow;
            if (eSlow > 0.05)  // Warmup based on slower EMA's bias correction factor
            {
                double cFast = 1.0 / (1.0 - eFast);
                double cSlow = 1.0 / (1.0 - eSlow);
                emaFast = cFast * emaFastRaw;
                emaSlow = cSlow * emaSlowRaw;
            }
            else
            {
                emaFast = emaFastRaw;
                emaSlow = emaSlowRaw;
            }

            output[i] = emaFast - emaSlow;
        }
    }

    public static (TSeries Results, Dsp Indicator) Calculate(TSeries source, int period = 40)
    {
        var indicator = new Dsp(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}