using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// ADOSC: Accumulation/Distribution Oscillator (Chaikin Oscillator)
/// </summary>
/// <remarks>
/// Measures momentum of the ADL using dual EMAs. Positive values indicate accumulation momentum;
/// negative indicates distribution. Standard parameters: fast=3, slow=10.
///
/// Calculation: <c>ADOSC = EMA(ADL, fast) - EMA(ADL, slow)</c>.
/// </remarks>
/// <seealso href="Adosc.md">Detailed documentation</seealso>
/// <seealso href="adosc.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Adosc : ITValuePublisher
{
    private readonly Adl _adl;
    private readonly Ema _emaFast;
    private readonly Ema _emaSlow;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current ADOSC value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has enough data to produce valid results.
    /// </summary>
    public bool IsHot => _emaSlow.IsHot;

    /// <summary>
    /// The number of bars required to warm up the indicator.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates ADOSC with specified periods.
    /// </summary>
    /// <param name="fastPeriod">Fast EMA period (default 3)</param>
    /// <param name="slowPeriod">Slow EMA period (default 10)</param>
    public Adosc(int fastPeriod = 3, int slowPeriod = 10)
    {
        if (fastPeriod <= 0)
        {
            throw new ArgumentException("Fast period must be greater than 0", nameof(fastPeriod));
        }

        if (slowPeriod <= 0)
        {
            throw new ArgumentException("Slow period must be greater than 0", nameof(slowPeriod));
        }

        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));
        }

        _adl = new Adl();
        _emaFast = new Ema(fastPeriod);
        _emaSlow = new Ema(slowPeriod);
        WarmupPeriod = slowPeriod;
        Name = $"Adosc({fastPeriod},{slowPeriod})";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _adl.Reset();
        _emaFast.Reset();
        _emaSlow.Reset();
        Last = default;
    }

    /// <summary>
    /// Updates the indicator with a new ADL value.
    /// </summary>
    /// <param name="input">The new ADL value</param>
    /// <param name="isNew">Whether this is a new value or an update to the last value</param>
    /// <returns>The updated ADOSC value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        var eFast = _emaFast.Update(input, isNew);
        var eSlow = _emaSlow.Update(input, isNew);

        double adosc = eFast.Value - eSlow.Value;
        Last = new TValue(input.Time, adosc);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a new bar.
    /// </summary>
    /// <param name="input">The new bar data</param>
    /// <param name="isNew">Whether this is a new bar or an update to the last bar</param>
    /// <returns>The updated ADOSC value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        var adl = _adl.Update(input, isNew);
        return Update(adl, isNew);
    }

    /// <summary>
    /// Updates the indicator with a series of bars.
    /// </summary>
    /// <param name="source">The source series of bars</param>
    /// <returns>The ADOSC series</returns>
    public TSeries Update(TBarSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], isNew: true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    // EMA compensator threshold (same as in Ema.cs)
    private const double COMPENSATOR_THRESHOLD = 1e-10;

    /// <summary>
    /// Calculates ADOSC for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="fastPeriod">Fast EMA period (default 3)</param>
    /// <param name="slowPeriod">Slow EMA period (default 10)</param>
    /// <returns>ADOSC series</returns>
    public static TSeries Batch(TBarSeries source, int fastPeriod = 3, int slowPeriod = 10)
    {
        var adosc = new Adosc(fastPeriod, slowPeriod);
        return adosc.Update(source);
    }

    /// <summary>
    /// Calculates ADOSC for the entire span using a single-pass algorithm.
    /// Zero allocation for maximum performance.
    /// Uses compensator pattern from EMA for proper early-stage bias correction.
    /// </summary>
    /// <param name="high">High prices</param>
    /// <param name="low">Low prices</param>
    /// <param name="close">Close prices</param>
    /// <param name="volume">Volume</param>
    /// <param name="output">Output span</param>
    /// <param name="fastPeriod">Fast EMA period (default 3)</param>
    /// <param name="slowPeriod">Slow EMA period (default 10)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int fastPeriod = 3, int slowPeriod = 10)
    {
        if (high.Length != low.Length || high.Length != close.Length ||
            high.Length != volume.Length || high.Length != output.Length)
        {
            throw new ArgumentException("All spans must be of the same length.", nameof(output));
        }
        if (fastPeriod <= 0)
        {
            throw new ArgumentException("Fast period must be greater than 0", nameof(fastPeriod));
        }
        if (slowPeriod <= 0)
        {
            throw new ArgumentException("Slow period must be greater than 0", nameof(slowPeriod));
        }
        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // EMA parameters (same formula as Ema.cs: alpha = 2 / (period + 1))
        double alphaFast = 2.0 / (fastPeriod + 1);
        double alphaSlow = 2.0 / (slowPeriod + 1);
        double decayFast = 1.0 - alphaFast;
        double decaySlow = 1.0 - alphaSlow;

        // State variables (no heap allocations)
        double adl = 0;
        double emaFast = 0;
        double emaSlow = 0;
        double eFast = 1.0;  // Compensation factor for fast EMA (starts at 1, decays toward 0)
        double eSlow = 1.0;  // Compensation factor for slow EMA
        bool fastCompensated = false;
        bool slowCompensated = false;

        // Single pass: compute ADL, both EMAs, and output in one loop
        for (int i = 0; i < len; i++)
        {
            double h = high[i];
            double l = low[i];
            double c = close[i];
            double vol = volume[i];

            // 1. Compute Money Flow Multiplier and Volume
            double hl = h - l;
            double mfm = 0;
            if (hl > double.Epsilon)
            {
                mfm = (c - l - (h - c)) / hl;
            }
            double mfv = mfm * vol;

            // 2. Update ADL (cumulative)
            adl += mfv;

            // 3. Update Fast EMA with FMA (same pattern as Ema.cs Compute method)
            // state.Ema = Math.FusedMultiplyAdd(state.Ema, decay, alpha * input)
            emaFast = Math.FusedMultiplyAdd(emaFast, decayFast, alphaFast * adl);

            // 4. Update Slow EMA with FMA
            emaSlow = Math.FusedMultiplyAdd(emaSlow, decaySlow, alphaSlow * adl);

            // 5. Compute compensated EMA values (same logic as Ema.cs Compute method)
            // Compensator decays: e *= decay, then result = ema / (1 - e) until e <= threshold
            double fastValue = 0, slowValue = 0;

            if (!fastCompensated)
            {
                eFast *= decayFast;
                if (eFast <= COMPENSATOR_THRESHOLD)
                {
                    fastCompensated = true;
                    fastValue = emaFast;
                }
                else
                {
                    fastValue = emaFast / (1.0 - eFast);
                }
            }
            else
            {
                fastValue = emaFast;
            }

            if (!slowCompensated)
            {
                eSlow *= decaySlow;
                if (eSlow <= COMPENSATOR_THRESHOLD)
                {
                    slowCompensated = true;
                    slowValue = emaSlow;
                }
                else
                {
                    slowValue = emaSlow / (1.0 - eSlow);
                }
            }
            else
            {
                slowValue = emaSlow;
            }

            output[i] = fastValue - slowValue;
        }
    }

    public static (TSeries Results, Adosc Indicator) Calculate(TBarSeries source, int fastPeriod = 3, int slowPeriod = 10)
    {
        var indicator = new Adosc(fastPeriod, slowPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}