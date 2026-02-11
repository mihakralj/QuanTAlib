using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Volume Weighted Average Price (VWAP) with optional periodic reset.
/// </summary>
/// <remarks>
/// VWAP uses the typical price <c>(High + Low + Close) / 3</c> weighted by volume:
/// <c>VWAP = Σ(typicalPrice × volume) / Σ(volume)</c>.
///
/// This implementation supports cumulative mode (<c>period=0</c>) or periodic reset
/// for session-based analysis. Commonly used by institutional traders for execution benchmarking.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Vwap.md">Detailed documentation</seealso>
/// <seealso href="vwap.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Vwap : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double SumPV, double SumVol, int Index, int BarsSinceReset)
    {
        public static State New() => new() { SumPV = 0, SumVol = 0, Index = 0, BarsSinceReset = 0 };
    }

    private readonly int _period;
    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidHigh;
    private double _lastValidLow;
    private double _lastValidClose;
    private double _lastValidVolume;
    private double _p_lastValidHigh;
    private double _p_lastValidLow;
    private double _p_lastValidClose;
    private double _p_lastValidVolume;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current VWAP value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has processed at least one bar.
    /// </summary>
    public bool IsHot => _state.Index > 0;

    /// <summary>
    /// Warmup period: 1 bar needed for first valid value.
    /// </summary>
    // S2325 suppressed: Instance property required for interface consistency across all indicators,
    // even when value is constant. All QuanTAlib indicators expose WarmupPeriod as instance property.
#pragma warning disable S2325
    public int WarmupPeriod => 1;
#pragma warning restore S2325

    /// <summary>
    /// Creates a new VWAP indicator with period-based reset.
    /// </summary>
    /// <param name="period">Period for VWAP reset (0 = no reset/cumulative). Default: 390 (typical trading day in minutes)</param>
    /// <exception cref="ArgumentException">Thrown when period is negative.</exception>
    public Vwap(int period = 0)
    {
        if (period < 0)
        {
            throw new ArgumentException("Period must be >= 0 (0 = no reset)", nameof(period));
        }

        _period = period;
        Name = period == 0 ? "VWAP" : $"VWAP({period})";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _state = State.New();
        _p_state = State.New();
        _lastValidHigh = 0;
        _lastValidLow = 0;
        _lastValidClose = 0;
        _lastValidVolume = 0;
        _p_lastValidHigh = 0;
        _p_lastValidLow = 0;
        _p_lastValidClose = 0;
        _p_lastValidVolume = 0;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetValidValue(double input, ref double lastValid)
    {
        if (double.IsFinite(input))
        {
            lastValid = input;
            return input;
        }
        return lastValid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _p_lastValidHigh = _lastValidHigh;
            _p_lastValidLow = _lastValidLow;
            _p_lastValidClose = _lastValidClose;
            _p_lastValidVolume = _lastValidVolume;
        }
        else
        {
            _state = _p_state;
            _lastValidHigh = _p_lastValidHigh;
            _lastValidLow = _p_lastValidLow;
            _lastValidClose = _p_lastValidClose;
            _lastValidVolume = _p_lastValidVolume;
        }

        // Get valid OHLCV values
        double high = GetValidValue(input.High, ref _lastValidHigh);
        double low = GetValidValue(input.Low, ref _lastValidLow);
        double close = GetValidValue(input.Close, ref _lastValidClose);
        double volume = GetValidValue(input.Volume, ref _lastValidVolume);

        // Calculate typical price (hlc3)
        double typicalPrice = (high + low + close) / 3.0;

        // Local copy for struct promotion
        var s = _state;

        // Check for period reset
        bool shouldReset = _period > 0 && s.BarsSinceReset >= _period;
        if (shouldReset)
        {
            s.SumPV = 0;
            s.SumVol = 0;
            s.BarsSinceReset = 0;
        }

        // Update cumulative sums
        if (volume > 0)
        {
            s.SumPV += typicalPrice * volume;
            s.SumVol += volume;
        }

        // Calculate VWAP
        double vwap = s.SumVol > double.Epsilon ? s.SumPV / s.SumVol : typicalPrice;

        if (isNew)
        {
            s.Index++;
            s.BarsSinceReset++;
        }

        _state = s;

        Last = new TValue(input.Time, vwap);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates VWAP with a TValue input (uses value as both price and assumes volume=1).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue Update(TValue input, bool isNew = true)
    {
        // Create synthetic bar: price as close, high, low; volume = 1
        var bar = new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 1.0);
        return Update(bar, isNew);
    }

    /// <summary>
    /// Calculates VWAP for an entire bar series.
    /// </summary>
    /// <param name="source">Source bar series</param>
    /// <returns>TSeries containing VWAP values</returns>
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
            var val = Update(source[i], isNew: true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Static calculation returning TSeries.
    /// </summary>
    /// <param name="source">Source bar series</param>
    /// <param name="period">Period for VWAP reset (0 = no reset)</param>
    /// <returns>TSeries containing VWAP values</returns>
    public static TSeries Batch(TBarSeries source, int period = 0)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Batch(source.High.Values, source.Low.Values, source.Close.Values, source.Volume.Values, v, period);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Zero-allocation span-based calculation.
    /// </summary>
    /// <param name="high">High prices</param>
    /// <param name="low">Low prices</param>
    /// <param name="close">Close prices</param>
    /// <param name="volume">Volume values</param>
    /// <param name="output">Output span for VWAP values</param>
    /// <param name="period">Period for VWAP reset (0 = no reset)</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int period = 0)
    {
        if (high.Length != low.Length)
        {
            throw new ArgumentException("High and Low spans must be of the same length", nameof(low));
        }

        if (high.Length != close.Length)
        {
            throw new ArgumentException("High and Close spans must be of the same length", nameof(close));
        }

        if (high.Length != volume.Length)
        {
            throw new ArgumentException("High and Volume spans must be of the same length", nameof(volume));
        }

        if (high.Length != output.Length)
        {
            throw new ArgumentException("Output span must be of the same length as input", nameof(output));
        }

        if (period < 0)
        {
            throw new ArgumentException("Period must be >= 0 (0 = no reset)", nameof(period));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        double sumPV = 0;
        double sumVol = 0;
        double lastValidHigh = 0;
        double lastValidLow = 0;
        double lastValidClose = 0;
        double lastValidVolume = 0;
        int barsSinceReset = 0;

        // Find first valid values
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(high[k]))
            {
                lastValidHigh = high[k];
                break;
            }
        }
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(low[k]))
            {
                lastValidLow = low[k];
                break;
            }
        }
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(close[k]))
            {
                lastValidClose = close[k];
                break;
            }
        }
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(volume[k]))
            {
                lastValidVolume = volume[k];
                break;
            }
        }

        for (int i = 0; i < len; i++)
        {
            // Get valid values with NaN substitution
            double h = double.IsFinite(high[i]) ? high[i] : lastValidHigh;
            double l = double.IsFinite(low[i]) ? low[i] : lastValidLow;
            double c = double.IsFinite(close[i]) ? close[i] : lastValidClose;
            double vol = double.IsFinite(volume[i]) ? volume[i] : lastValidVolume;

            if (double.IsFinite(high[i]))
            {
                lastValidHigh = high[i];
            }
            if (double.IsFinite(low[i]))
            {
                lastValidLow = low[i];
            }
            if (double.IsFinite(close[i]))
            {
                lastValidClose = close[i];
            }
            if (double.IsFinite(volume[i]))
            {
                lastValidVolume = volume[i];
            }

            // Calculate typical price (hlc3)
            double typicalPrice = (h + l + c) / 3.0;

            // Check for period reset
            if (period > 0 && barsSinceReset >= period)
            {
                sumPV = 0;
                sumVol = 0;
                barsSinceReset = 0;
            }

            // Update cumulative sums
            if (vol > 0)
            {
                sumPV += typicalPrice * vol;
                sumVol += vol;
            }

            // Calculate VWAP
            output[i] = sumVol > double.Epsilon ? sumPV / sumVol : typicalPrice;
            barsSinceReset++;
        }
    }

    public static (TSeries Results, Vwap Indicator) Calculate(TBarSeries source, int period = 0)
    {
        var indicator = new Vwap(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}