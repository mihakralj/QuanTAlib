using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BBWP: Bollinger Band Width Percentile
/// </summary>
/// <remarks>
/// BBWP measures where the current Bollinger Band Width falls within its
/// historical distribution, expressing the result as a percentile rank
/// between 0 and 1. Unlike BBWN which normalizes using min/max values,
/// BBWP uses percentile ranking which is more robust to outliers.
///
/// Formula:
/// <c>BBW = 2 × multiplier × StdDev(source, period)</c>
/// <c>BBWP = count(BBW_history &lt; BBW_current) / total_count</c>
///
/// The indicator first calculates the standard BBW, then determines what
/// percentage of historical BBW values fall below the current value.
/// Values near 0 indicate current volatility is lower than most historical
/// readings, while values near 1 indicate it's higher than most.
///
/// Key properties:
/// - Range: [0, 1] (percentile)
/// - 0.0 indicates current BBW is lowest in lookback period
/// - 1.0 indicates current BBW is highest in lookback period
/// - 0.5 indicates median volatility when no percentile can be calculated
/// </remarks>
[SkipLocalsInit]
public sealed class Bbwp : AbstractBase
{
    private readonly int _period;
    private readonly double _multiplier;
    private readonly int _lookback;
    private readonly RingBuffer _buffer;
    private readonly RingBuffer _bbwBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Sum,
        double SumSq,
        double SumComp,
        double SumSqComp,
        double LastValid);
    private State _state;
    private State _p_state;

    /// <summary>
    /// Creates BBWP with specified period, multiplier, and lookback.
    /// </summary>
    /// <param name="period">Lookback period for BB calculations (must be > 0)</param>
    /// <param name="multiplier">Standard deviation multiplier (must be > 0)</param>
    /// <param name="lookback">Historical lookback period for percentile calculation (must be > 0)</param>
    public Bbwp(int period, double multiplier = 2.0, int lookback = 252)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (multiplier <= 0)
        {
            throw new ArgumentException("Multiplier must be greater than 0", nameof(multiplier));
        }

        if (lookback <= 0)
        {
            throw new ArgumentException("Lookback must be greater than 0", nameof(lookback));
        }

        _period = period;
        _multiplier = multiplier;
        _lookback = lookback;
        _buffer = new RingBuffer(period);
        _bbwBuffer = new RingBuffer(lookback);
        Name = $"Bbwp({period},{multiplier:F1},{lookback})";
        WarmupPeriod = period + lookback;
    }

    /// <summary>
    /// Creates BBWP with specified source, period, multiplier, and lookback.
    /// </summary>
    public Bbwp(ITValuePublisher source, int period, double multiplier = 2.0, int lookback = 252) : this(period, multiplier, lookback)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _buffer.IsFull && _bbwBuffer.Count >= Math.Min(10, _lookback);

    /// <summary>
    /// Period of the indicator.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Standard deviation multiplier.
    /// </summary>
    public double Multiplier => _multiplier;

    /// <summary>
    /// Historical lookback period for percentile calculation.
    /// </summary>
    public int Lookback => _lookback;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        // Sanitize input
        if (!double.IsFinite(value))
        {
            value = double.IsFinite(_state.LastValid) ? _state.LastValid : 0.0;
        }
        else
        {
            _state.LastValid = value;
        }

        if (isNew)
        {
            _p_state = _state;

            // Kahan compensated sliding window update
            if (_buffer.Count == _buffer.Capacity)
            {
                double oldest = _buffer.Oldest;
                double delta = value - oldest;
                {
                    double y = delta - _state.SumComp;
                    double t = _state.Sum + y;
                    _state.SumComp = (t - _state.Sum) - y;
                    _state.Sum = t;
                }
                {
                    double deltaSq = (value * value) - (oldest * oldest);
                    double y = deltaSq - _state.SumSqComp;
                    double t = _state.SumSq + y;
                    _state.SumSqComp = (t - _state.SumSq) - y;
                    _state.SumSq = t;
                }
            }
            else
            {
                {
                    double y = value - _state.SumComp;
                    double t = _state.Sum + y;
                    _state.SumComp = (t - _state.Sum) - y;
                    _state.Sum = t;
                }
                {
                    double sq = value * value;
                    double y = sq - _state.SumSqComp;
                    double t = _state.SumSq + y;
                    _state.SumSqComp = (t - _state.SumSq) - y;
                    _state.SumSq = t;
                }
            }

            _buffer.Add(value);
        }
        else
        {
            _state = _p_state;

            // Update the newest value in buffer
            _buffer.UpdateNewest(value);
            RecalculateSums();
        }

        // Calculate BBW first
        int count = _buffer.Count;
        if (count == 0)
        {
            Last = new TValue(input.Time, 0.5);
            PubEvent(Last, isNew);
            return Last;
        }

        double mean = _state.Sum / count;
        double variance = Math.Max(0.0, (_state.SumSq / count) - (mean * mean));
        double stddev = Math.Sqrt(variance);
        double bbw = 2.0 * _multiplier * stddev;

        // Add BBW to history buffer for percentile calculation
        if (isNew)
        {
            _bbwBuffer.Add(bbw);
        }
        else
        {
            _bbwBuffer.UpdateNewest(bbw);
        }

        // Calculate percentile of current BBW within historical distribution
        double bbwp = 0.5; // Default when no percentile can be calculated
        int totalCount = _bbwBuffer.Count;
        if (totalCount >= 1)
        {
            int countBelow = 0;
            for (int i = 0; i < totalCount; i++)
            {
                if (_bbwBuffer[i] < bbw)
                {
                    countBelow++;
                }
            }
            bbwp = (double)countBelow / totalCount;
        }

        // Clamp to [0,1] range (should already be in range, but ensure safety)
        bbwp = Math.Max(0.0, Math.Min(1.0, bbwp));

        Last = new TValue(input.Time, bbwp);
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

        Batch(source.Values, vSpan, _period, _multiplier, _lookback);
        source.Times.CopyTo(tSpan);

        // Update internal state to match final position
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecalculateSums()
    {
        _state.Sum = 0.0;
        _state.SumSq = 0.0;
        for (int i = 0; i < _buffer.Count; i++)
        {
            double v = _buffer[i];
            _state.Sum += v;
            _state.SumSq += v * v;
        }
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
        _buffer.Clear();
        _bbwBuffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    /// <summary>
    /// Calculates BBWP for entire series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period, double multiplier = 2.0, int lookback = 252)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (multiplier <= 0)
        {
            throw new ArgumentException("Multiplier must be greater than 0", nameof(multiplier));
        }

        if (lookback <= 0)
        {
            throw new ArgumentException("Lookback must be greater than 0", nameof(lookback));
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, period, multiplier, lookback);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch BBWP calculation with O(1) rolling variance and percentile ranking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, double multiplier = 2.0, int lookback = 252)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (multiplier <= 0)
        {
            throw new ArgumentException("Multiplier must be greater than 0", nameof(multiplier));
        }

        if (lookback <= 0)
        {
            throw new ArgumentException("Lookback must be greater than 0", nameof(lookback));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double sum = 0.0;
        double sumSq = 0.0;
        double mult2 = 2.0 * multiplier;
        double lastValid = 0.0;
        var bbwHistory = new RingBuffer(lookback);

        // Buffer to track sanitized values for correct window removal
        var valueBuffer = new RingBuffer(period);

        for (int i = 0; i < len; i++)
        {
            double val = source[i];

            // Sanitize input - mirror Update method behavior
            if (!double.IsFinite(val))
            {
                val = lastValid;
            }
            else
            {
                lastValid = val;
            }

            // Remove oldest sanitized value if past warmup
            if (i >= period)
            {
                double oldest = valueBuffer.Oldest;
                sum -= oldest;
                sumSq -= oldest * oldest;
            }

            // Add new sanitized value
            sum += val;
            sumSq += val * val;
            valueBuffer.Add(val);

            // Calculate BBW
            int count = Math.Min(i + 1, period);
            double mean = sum / count;
            double variance = Math.Max(0.0, (sumSq / count) - (mean * mean));
            double stddev = Math.Sqrt(variance);
            double bbw = mult2 * stddev;

            // Add to BBW history
            bbwHistory.Add(bbw);

            // Calculate percentile of current BBW within historical distribution
            double bbwp = 0.5; // Default
            int totalCount = bbwHistory.Count;
            if (totalCount >= 1)
            {
                int countBelow = 0;
                for (int j = 0; j < totalCount; j++)
                {
                    if (bbwHistory[j] < bbw)
                    {
                        countBelow++;
                    }
                }
                bbwp = (double)countBelow / totalCount;
            }

            // Clamp to [0,1] range
            output[i] = Math.Max(0.0, Math.Min(1.0, bbwp));
        }
    }

    public static (TSeries Results, Bbwp Indicator) Calculate(TSeries source, int period, double multiplier = 2.0, int lookback = 252)
    {
        var indicator = new Bbwp(period, multiplier, lookback);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}