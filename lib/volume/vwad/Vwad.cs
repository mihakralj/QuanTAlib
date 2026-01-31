using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Volume Weighted Accumulation/Distribution (VWAD) indicator that weights
/// each bar's contribution based on its volume relative to the rolling volume sum.
/// </summary>
/// <remarks>
/// VWAD enhances ADL by weighting volume contributions:
/// <c>MFM = [(Close - Low) - (High - Close)] / (High - Low)</c>,
/// <c>VolWeight = Volume / Σ(Volume, period)</c>,
/// <c>VWAD = Σ(Volume × MFM × VolWeight)</c>.
///
/// This implementation is optimized for streaming updates with O(1) per bar using circular buffers.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed
/// for each OHLCV component independently.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Vwad.md">Detailed documentation</seealso>
/// <seealso href="vwad.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Vwad : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double CumulativeVwad, double SumVol, int Index)
    {
        public static State New() => new() { CumulativeVwad = 0, SumVol = 0, Index = 0 };
    }

    private readonly int _period;
    private readonly RingBuffer _volBuffer;
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
    /// Current VWAD value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has processed at least one bar.
    /// </summary>
    public bool IsHot => _state.Index > 0;

    /// <summary>
    /// Warmup period required before volume weighting is fully effective.
    /// </summary>
    public int WarmupPeriod => _period;

    /// <summary>
    /// Creates a new VWAD indicator.
    /// </summary>
    /// <param name="period">Lookback period for volume weighting (default: 20)</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Vwad(int period = 20)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        _period = period;
        _volBuffer = new RingBuffer(period);
        Name = $"VWAD({period})";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _volBuffer.Clear();
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
    private double GetValidValue(double input, ref double lastValid)
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
            _volBuffer.Snapshot();
        }
        else
        {
            _state = _p_state;
            _lastValidHigh = _p_lastValidHigh;
            _lastValidLow = _p_lastValidLow;
            _lastValidClose = _p_lastValidClose;
            _lastValidVolume = _p_lastValidVolume;
            _volBuffer.Restore();
        }

        // Get valid OHLCV values
        double high = GetValidValue(input.High, ref _lastValidHigh);
        double low = GetValidValue(input.Low, ref _lastValidLow);
        double close = GetValidValue(input.Close, ref _lastValidClose);
        double volume = GetValidValue(input.Volume, ref _lastValidVolume);

        // Local copy for struct promotion
        var s = _state;

        // Update rolling volume sum
        if (_volBuffer.IsFull)
        {
            s.SumVol -= _volBuffer.Oldest;
        }
        s.SumVol += volume;
        _volBuffer.Add(volume);

        // Calculate Money Flow Multiplier
        double highLowRange = high - low;
        double mfm = 0;
        if (highLowRange > double.Epsilon)
        {
            mfm = (close - low - (high - close)) / highLowRange;
        }

        // Calculate volume weight and weighted MFV
        double volWeight = s.SumVol > double.Epsilon ? volume / s.SumVol : 0;
        double weightedMfv = volume * mfm * volWeight;

        // Update cumulative VWAD
        s.CumulativeVwad += weightedMfv;

        if (isNew)
        {
            s.Index++;
        }

        _state = s;

        Last = new TValue(input.Time, s.CumulativeVwad);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates VWAD with a TValue input.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// VWAD requires OHLCV bar data to calculate the Money Flow Multiplier and Volume Weight.
    /// Use Update(TBar) instead.
    /// </exception>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue input, bool isNew = true)
#pragma warning restore S2325
    {
        throw new NotSupportedException(
            "VWAD requires OHLCV bar data to calculate the Money Flow Multiplier and Volume Weight. " +
            "Use Update(TBar) instead.");
    }

    /// <summary>
    /// Calculates VWAD for an entire bar series.
    /// </summary>
    /// <param name="source">Source bar series</param>
    /// <returns>TSeries containing VWAD values</returns>
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
    /// <param name="period">Lookback period for volume weighting</param>
    /// <returns>TSeries containing VWAD values</returns>
    public static TSeries Calculate(TBarSeries source, int period = 20)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Calculate(source.High.Values, source.Low.Values, source.Close.Values, source.Volume.Values, v, period);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Zero-allocation span-based calculation.
    /// </summary>
    /// <param name="high">High prices</param>
    /// <param name="low">Low prices</param>
    /// <param name="close">Close prices</param>
    /// <param name="volume">Volume values</param>
    /// <param name="output">Output span for VWAD values</param>
    /// <param name="period">Lookback period for volume weighting</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Calculate(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int period = 20)
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

        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        double sumVol = 0;
        double cumulativeVwad = 0;
        double lastValidHigh = 0;
        double lastValidLow = 0;
        double lastValidClose = 0;
        double lastValidVolume = 0;

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

            // Update rolling volume sum
            sumVol += vol;
            if (i >= period)
            {
                double oldVol = double.IsFinite(volume[i - period]) ? volume[i - period] : 0;
                sumVol -= oldVol;
            }

            // Calculate Money Flow Multiplier
            double highLowRange = h - l;
            double mfm = 0;
            if (highLowRange > double.Epsilon)
            {
                mfm = (c - l - (h - c)) / highLowRange;
            }

            // Calculate volume weight and weighted MFV
            double volWeight = sumVol > double.Epsilon ? vol / sumVol : 0;
            double weightedMfv = vol * mfm * volWeight;

            // Update cumulative VWAD
            cumulativeVwad += weightedMfv;
            output[i] = cumulativeVwad;
        }
    }
}