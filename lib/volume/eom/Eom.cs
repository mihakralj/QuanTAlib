using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// EOM: Ease of Movement
/// </summary>
/// <remarks>
/// Volume-based oscillator measuring how easily prices move via price change and volume relationship.
/// Positive values indicate upward ease; negative values indicate downward ease.
///
/// Calculation: <c>Midpoint = (High + Low) / 2</c>, <c>Box_Ratio = (Volume / Scale) / (High - Low)</c>,
/// <c>Raw_EOM = (Midpoint - prev_Midpoint) / Box_Ratio</c>, <c>EOM = SMA(Raw_EOM, period)</c>.
/// </remarks>
/// <seealso href="Eom.md">Detailed documentation</seealso>
/// <seealso href="eom.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Eom : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double PrevMidPoint;
        public double Sum;
        public int Head;
        public int Count;
        public double LastValidValue;
        public bool HasPrevMidPoint;
    }

    private State _s;
    private State _ps;
    private readonly int _period;
    private readonly double _volumeScale;
    private readonly double[] _buffer;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public bool IsHot { get; private set; }
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Initializes a new instance of the Eom class.
    /// </summary>
    /// <param name="period">The smoothing period for SMA calculation (default: 14)</param>
    /// <param name="volumeScale">The volume scaling factor (default: 10000)</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1 or volumeScale is less than or equal to 0</exception>
    public Eom(int period = 14, double volumeScale = 10000)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }
        if (volumeScale <= 0)
        {
            throw new ArgumentException("Volume scale must be > 0", nameof(volumeScale));
        }

        _period = period;
        _volumeScale = volumeScale;
        _buffer = new double[period];
        WarmupPeriod = period + 1; // +1 for previous midpoint
        Name = $"Eom({period},{volumeScale:F0})";
        _s = new State { LastValidValue = 0.0 };
        _ps = _s;
    }

    /// <summary>
    /// Updates the indicator with a new bar.
    /// </summary>
    /// <param name="bar">The bar data containing High, Low, Close, and Volume</param>
    /// <param name="isNew">Whether this is a new bar or an update to the current bar</param>
    /// <returns>The calculated EOM value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
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

        double high = bar.High;
        double low = bar.Low;
        double volume = bar.Volume;

        // Calculate midpoint
        double midPoint = (high + low) * 0.5;

        // Calculate midpoint change (0 if no previous)
        double midPointChange = s.HasPrevMidPoint ? midPoint - s.PrevMidPoint : 0.0;

        // Calculate price range
        double priceRange = high - low;

        // Calculate raw EOM
        double rawEom;
        if (priceRange > 0 && volume > 0)
        {
            double boxRatio = (volume / _volumeScale) / priceRange;
            rawEom = Math.Abs(boxRatio) > 0 ? midPointChange / boxRatio : 0.0;
        }
        else
        {
            rawEom = 0.0;
        }

        // Handle NaN/Infinity
        if (!double.IsFinite(rawEom))
        {
            rawEom = s.LastValidValue;
        }
        else
        {
            s.LastValidValue = rawEom;
        }

        // SMA calculation using ring buffer
        if (isNew && s.Count >= _period)
        {
            s.Sum -= _buffer[s.Head];
        }

        if (isNew)
        {
            _buffer[s.Head] = rawEom;
            s.Sum += rawEom;
            s.Head = (s.Head + 1) % _period;
            if (s.Count < _period)
            {
                s.Count++;
            }
            s.PrevMidPoint = midPoint;
            s.HasPrevMidPoint = true;
        }
        else
        {
            // For bar correction: state was restored, so s.Head is the current slot to overwrite
            int currentIndex = s.Head;
            double oldValue = _buffer[currentIndex];
            s.Sum = s.Sum - oldValue + rawEom;
            _buffer[currentIndex] = rawEom;
        }

        double result = s.Count > 0 ? s.Sum / s.Count : 0.0;

        _s = s;

        IsHot = s.Count >= _period && s.HasPrevMidPoint;
        Last = new TValue(bar.Time, result);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// TValue input is not supported for EOM - requires TBar (OHLCV) data.
    /// </summary>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue value, bool isNew = true)
#pragma warning restore S2325
    {
        throw new NotSupportedException("EOM requires TBar (OHLCV) data. Use Update(TBar) instead.");
    }

    /// <summary>
    /// Updates EOM with a bar series.
    /// </summary>
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

    /// <summary>
    /// Resets the indicator to its initial state.
    /// </summary>
    public void Reset()
    {
        _s = new State { LastValidValue = 0.0 };
        _ps = _s;
        Array.Clear(_buffer);
        IsHot = false;
        Last = default;
    }

    /// <summary>
    /// Calculates EOM for a series of bars.
    /// </summary>
    /// <param name="bars">The input bar series</param>
    /// <param name="period">The smoothing period</param>
    /// <param name="volumeScale">The volume scaling factor</param>
    /// <returns>A TSeries containing the EOM values</returns>
    public static TSeries Batch(TBarSeries bars, int period = 14, double volumeScale = 10000)
    {
        if (bars.Count == 0)
        {
            return [];
        }

        var t = bars.Open.Times.ToArray();
        var v = new double[bars.Count];

        Batch(bars.High.Values, bars.Low.Values, bars.Volume.Values, v, period, volumeScale);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates EOM values using span-based processing.
    /// </summary>
    /// <param name="high">Source high prices</param>
    /// <param name="low">Source low prices</param>
    /// <param name="volume">Source volumes</param>
    /// <param name="output">Output span for EOM values</param>
    /// <param name="period">The smoothing period</param>
    /// <param name="volumeScale">The volume scaling factor</param>
    /// <exception cref="ArgumentException">Thrown when spans have different lengths</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low,
        ReadOnlySpan<double> volume, Span<double> output, int period = 14, double volumeScale = 10000)
    {
        if (high.Length != low.Length)
        {
            throw new ArgumentException("High and low spans must have the same length", nameof(low));
        }
        if (high.Length != volume.Length)
        {
            throw new ArgumentException("High and volume spans must have the same length", nameof(volume));
        }
        if (high.Length != output.Length)
        {
            throw new ArgumentException("Output span must have the same length as input", nameof(output));
        }
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }
        if (volumeScale <= 0)
        {
            throw new ArgumentException("Volume scale must be > 0", nameof(volumeScale));
        }

        int length = high.Length;
        if (length == 0)
        {
            return;
        }

        const int StackallocThreshold = 256;
        double[]? rentedBuffer = null;
        scoped Span<double> rawEom;

        if (length <= StackallocThreshold)
        {
            rawEom = stackalloc double[length];
        }
        else
        {
            rentedBuffer = System.Buffers.ArrayPool<double>.Shared.Rent(length);
            rawEom = rentedBuffer.AsSpan(0, length);
        }

        try
        {
            // Calculate raw EOM values
            double prevMidPoint = (high[0] + low[0]) * 0.5;
            rawEom[0] = 0.0; // First value has no previous midpoint

            for (int i = 1; i < length; i++)
            {
                double midPoint = (high[i] + low[i]) * 0.5;
                double midPointChange = midPoint - prevMidPoint;
                double priceRange = high[i] - low[i];

                if (priceRange > 0 && volume[i] > 0)
                {
                    double boxRatio = (volume[i] / volumeScale) / priceRange;
                    rawEom[i] = Math.Abs(boxRatio) > 0 ? midPointChange / boxRatio : 0.0;
                }
                else
                {
                    rawEom[i] = 0.0;
                }

                if (!double.IsFinite(rawEom[i]))
                {
                    rawEom[i] = i > 0 ? rawEom[i - 1] : 0.0;
                }

                prevMidPoint = midPoint;
            }

            // Apply SMA smoothing
            double sum = 0;
            for (int i = 0; i < length; i++)
            {
                sum += rawEom[i];
                if (i >= period)
                {
                    sum -= rawEom[i - period];
                    output[i] = sum / period;
                }
                else
                {
                    output[i] = sum / (i + 1);
                }
            }
        }
        finally
        {
            if (rentedBuffer != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedBuffer);
            }
        }
    }

    public static (TSeries Results, Eom Indicator) Calculate(TBarSeries bars, int period = 14, double volumeScale = 10000)
    {
        var indicator = new Eom(period, volumeScale);
        TSeries results = indicator.Update(bars);
        return (results, indicator);
    }
}