using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// III: Intraday Intensity Index
/// </summary>
/// <remarks>
/// Volume-weighted indicator measuring buying/selling pressure based on close position in range.
/// Range: -1 (close at low) to +1 (close at high) times volume; indicates distribution vs accumulation.
///
/// Calculation: <c>Position = (2 × Close - High - Low) / (High - Low)</c>,
/// <c>Raw_III = Position × Volume</c>, <c>III = SMA(Raw_III, period)</c> or cumulative sum.
/// </remarks>
/// <seealso href="Iii.md">Detailed documentation</seealso>
/// <seealso href="iii.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Iii : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Sum;
        public double CumulativeValue;
        public int Head;
        public int Count;
        public double LastValidValue;
    }

    private State _s;
    private State _ps;
    private readonly int _period;
    private readonly bool _cumulative;
    private readonly double[] _buffer;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public bool IsHot { get; private set; }
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Initializes a new instance of the Iii class.
    /// </summary>
    /// <param name="period">The smoothing period for SMA calculation (default: 14)</param>
    /// <param name="cumulative">Whether to accumulate values (default: false)</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1</exception>
    public Iii(int period = 14, bool cumulative = false)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        _period = period;
        _cumulative = cumulative;
        _buffer = new double[period];
        WarmupPeriod = period;
        Name = cumulative ? $"Iii({period},Cum)" : $"Iii({period})";
        _s = new State { LastValidValue = 0.0 };
        _ps = _s;
    }

    /// <summary>
    /// Updates the indicator with a new bar.
    /// </summary>
    /// <param name="bar">The bar data containing High, Low, Close, and Volume</param>
    /// <param name="isNew">Whether this is a new bar or an update to the current bar</param>
    /// <returns>The calculated III value</returns>
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
        double close = bar.Close;
        double volume = Math.Max(bar.Volume, 1.0); // Ensure minimum volume of 1

        // Calculate price range
        double range = high - low;

        // Calculate position multiplier: where close falls in the range
        // +1 when close = high, -1 when close = low, 0 when close = midpoint
        double positionMultiplier = range > 0 ? (2.0 * close - high - low) / range : 0.0;

        // Calculate raw III
        double rawIii = positionMultiplier * volume;

        // Handle NaN/Infinity
        if (!double.IsFinite(rawIii))
        {
            rawIii = s.LastValidValue;
        }
        else
        {
            s.LastValidValue = rawIii;
        }

        // Update cumulative value
        if (isNew)
        {
            s.CumulativeValue += rawIii;
        }

        // SMA calculation using ring buffer (for non-cumulative mode)
        if (isNew && s.Count >= _period)
        {
            s.Sum -= _buffer[s.Head];
        }

        if (isNew)
        {
            _buffer[s.Head] = rawIii;
            s.Sum += rawIii;
            s.Head = (s.Head + 1) % _period;
            if (s.Count < _period)
            {
                s.Count++;
            }
        }
        else
        {
            // For bar correction, update the previous value in buffer
            int prevHead = (s.Head + _period - 1) % _period;
            double oldValue = _buffer[prevHead];
            s.Sum = s.Sum - oldValue + rawIii;
            _buffer[prevHead] = rawIii;

            // Recalculate cumulative by removing old and adding new
            s.CumulativeValue = s.CumulativeValue - oldValue + rawIii;
        }

        // Calculate result based on mode
        double result;
        if (_cumulative)
        {
            result = s.CumulativeValue;
        }
        else
        {
            // For SMA: divide by s.Count during warmup, _period once fully warmed
            int divisor = s.Count < _period ? s.Count : _period;
            result = divisor > 0 ? s.Sum / divisor : 0.0;
        }

        _s = s;

        IsHot = s.Count >= _period;
        Last = new TValue(bar.Time, result);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// TValue input is not supported for III - requires TBar (OHLCV) data.
    /// </summary>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue value, bool isNew = true)
#pragma warning restore S2325
    {
        throw new NotSupportedException("III requires TBar (OHLCV) data. Use Update(TBar) instead.");
    }

    /// <summary>
    /// Updates III with a bar series.
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
    /// Calculates III for a series of bars.
    /// </summary>
    /// <param name="bars">The input bar series</param>
    /// <param name="period">The smoothing period</param>
    /// <param name="cumulative">Whether to use cumulative mode</param>
    /// <returns>A TSeries containing the III values</returns>
    public static TSeries Batch(TBarSeries bars, int period = 14, bool cumulative = false)
    {
        if (bars.Count == 0)
        {
            return [];
        }

        var t = bars.Open.Times.ToArray();
        var v = new double[bars.Count];

        Batch(bars.High.Values, bars.Low.Values, bars.Close.Values, bars.Volume.Values, v, period, cumulative);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates III values using span-based processing.
    /// </summary>
    /// <param name="high">Source high prices</param>
    /// <param name="low">Source low prices</param>
    /// <param name="close">Source close prices</param>
    /// <param name="volume">Source volumes</param>
    /// <param name="output">Output span for III values</param>
    /// <param name="period">The smoothing period</param>
    /// <param name="cumulative">Whether to use cumulative mode</param>
    /// <exception cref="ArgumentException">Thrown when spans have different lengths</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low,
        ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output,
        int period = 14, bool cumulative = false)
    {
        if (high.Length != low.Length)
        {
            throw new ArgumentException("High and low spans must have the same length", nameof(low));
        }
        if (high.Length != close.Length)
        {
            throw new ArgumentException("High and close spans must have the same length", nameof(close));
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

        int length = high.Length;
        if (length == 0)
        {
            return;
        }

        const int StackallocThreshold = 256;
        double[]? rentedBuffer = null;
        scoped Span<double> rawIii;

        if (length <= StackallocThreshold)
        {
            rawIii = stackalloc double[length];
        }
        else
        {
            rentedBuffer = System.Buffers.ArrayPool<double>.Shared.Rent(length);
            rawIii = rentedBuffer.AsSpan(0, length);
        }

        try
        {
            // Calculate raw III values
            for (int i = 0; i < length; i++)
            {
                double range = high[i] - low[i];
                double vol = Math.Max(volume[i], 1.0);
                double positionMultiplier = range > 0 ? (2.0 * close[i] - high[i] - low[i]) / range : 0.0;
                rawIii[i] = positionMultiplier * vol;

                if (!double.IsFinite(rawIii[i]))
                {
                    rawIii[i] = i > 0 ? rawIii[i - 1] : 0.0;
                }
            }

            if (cumulative)
            {
                // Cumulative mode
                double cumulativeSum = 0;
                for (int i = 0; i < length; i++)
                {
                    cumulativeSum += rawIii[i];
                    output[i] = cumulativeSum;
                }
            }
            else
            {
                // Apply SMA smoothing
                double sum = 0;
                for (int i = 0; i < length; i++)
                {
                    sum += rawIii[i];
                    if (i >= period)
                    {
                        sum -= rawIii[i - period];
                        output[i] = sum / period;
                    }
                    else
                    {
                        // During warmup, divide by actual sample count
                        output[i] = sum / (i + 1);
                    }
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

    public static (TSeries Results, Iii Indicator) Calculate(TBarSeries bars, int period = 14, bool cumulative = false)
    {
        var indicator = new Iii(period, cumulative);
        TSeries results = indicator.Update(bars);
        return (results, indicator);
    }
}