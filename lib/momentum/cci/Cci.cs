using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CCI: Commodity Channel Index
/// </summary>
/// <remarks>
/// Measures the deviation of price from its statistical mean, normalized by mean
/// absolute deviation. Developed by Donald Lambert to identify cyclical turns.
///
/// Calculation:
/// <code>
/// TP = (High + Low + Close) / 3
/// SMA = Simple Moving Average of TP over period
/// Mean Deviation = SUM(|TP - SMA|) / period
/// CCI = (TP - SMA) / (0.015 * Mean Deviation)
/// </code>
///
/// Key levels:
/// - Above +100: Strong uptrend, potentially overbought
/// - Below -100: Strong downtrend, potentially oversold
/// - Zero line crossover: Trend change signal
///
/// The 0.015 constant ensures approximately 70-80% of values fall between +100 and -100.
/// </remarks>
/// <seealso href="Cci.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Cci : ITValuePublisher
{
    private const int DefaultPeriod = 20;
    private const double LambertConstant = 0.015;

    private readonly int _period;
    private readonly RingBuffer _tpBuffer;
    private int _sampleCount;
    private double _lastValid;
    private TValue _last;

    // State for bar correction
    [StructLayout(LayoutKind.Auto)]
    private record struct State(int SampleCount, double LastValid, double Sum);
    private State _state, _p_state;

    /// <summary>
    /// Event fired when a new CCI value is calculated.
    /// </summary>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Most recently calculated CCI value.
    /// </summary>
    public TValue Last => _last;

    /// <summary>
    /// True when the indicator has enough data for valid calculations.
    /// </summary>
    public bool IsHot => _sampleCount >= _period;

    /// <summary>
    /// The lookback period.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Number of bars required for warmup.
    /// </summary>
    public int WarmupPeriod => _period;

    /// <summary>
    /// Creates a CCI indicator with specified period.
    /// </summary>
    /// <param name="period">Lookback period (must be >= 2, default 20)</param>
    public Cci(int period = DefaultPeriod)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2", nameof(period));
        }

        _period = period;
        _tpBuffer = new RingBuffer(period);
        _sampleCount = 0;
        _lastValid = 0;
        _last = new TValue(DateTime.MinValue, 0);
    }

    /// <summary>
    /// Resets the indicator to its initial state.
    /// </summary>
    public void Reset()
    {
        _tpBuffer.Clear();
        _sampleCount = 0;
        _lastValid = 0;
        _last = default;
        _state = default;
        _p_state = default;
    }

    /// <summary>
    /// Updates the CCI with a new bar.
    /// </summary>
    /// <param name="bar">The input bar with OHLC data</param>
    /// <param name="isNew">True for a new bar, false for updating current bar</param>
    /// <returns>The updated CCI value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        // State management for bar correction
        if (isNew)
        {
            _p_state = _state;
            _sampleCount++;
        }
        else
        {
            _state = _p_state;
            _sampleCount = _state.SampleCount + 1;
        }

        // Calculate typical price
        double tp = (bar.High + bar.Low + bar.Close) / 3.0;

        // Handle invalid values
        if (!double.IsFinite(tp))
        {
            tp = _lastValid;
        }
        else
        {
            _lastValid = tp;
        }

        // Add to buffer
        _tpBuffer.Add(tp, isNew);

        // Calculate CCI
        double result = CalculateCci(tp);

        // Save state
        _state = new State(_sampleCount, _lastValid, 0);

        _last = new TValue(bar.Time, result);
        Pub?.Invoke(this, new TValueEventArgs { Value = _last, IsNew = isNew });
        return _last;
    }

    /// <summary>
    /// Updates CCI from a TBarSeries.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        var result = new TSeries(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            var tv = Update(source[i], true);
            result.Add(tv, true);
        }
        return result;
    }

    /// <summary>
    /// Primes the indicator with historical bars.
    /// </summary>
    public void Prime(TBarSeries source)
    {
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], true);
        }
    }

    /// <summary>
    /// Convenience method for batch processing.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period = DefaultPeriod)
    {
        var indicator = new Cci(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Calculates CCI and returns both the result and the indicator instance.
    /// </summary>
    public static (TSeries Results, Cci Indicator) Calculate(TBarSeries source, int period = DefaultPeriod)
    {
        var indicator = new Cci(period);
        var results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateCci(double currentTp)
    {
        int count = _tpBuffer.Count;
        if (count == 0)
        {
            return 0.0;
        }

        // Calculate SMA of typical prices
        double sum = 0.0;
        for (int i = 0; i < count; i++)
        {
            sum += _tpBuffer[i];
        }
        double sma = sum / count;

        // Calculate mean deviation
        double devSum = 0.0;
        for (int i = 0; i < count; i++)
        {
            devSum += Math.Abs(_tpBuffer[i] - sma);
        }
        double meanDev = devSum / count;

        // Calculate CCI
        if (meanDev <= double.Epsilon)
        {
            return 0.0;
        }

        return (currentTp - sma) / (LambertConstant * meanDev);
    }
}
