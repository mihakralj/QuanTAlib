using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BBWN: Bollinger Band Width Normalized
/// </summary>
/// <remarks>
/// Normalized version of Bollinger Band Width (BBW) that scales the width
/// to a [0,1] range based on historical min/max values over a lookback period.
/// This normalization helps identify relative volatility levels and makes
/// comparison across different timeframes and instruments more meaningful.
///
/// Formula:
/// <c>BBW = 2 × multiplier × StdDev(source, period)</c>
/// <c>BBWN = (BBW - min(BBW_lookback)) / (max(BBW_lookback) - min(BBW_lookback))</c>
///
/// The indicator first calculates the standard BBW, then normalizes it using
/// the min/max values from a specified lookback period. Values near 0 indicate
/// low relative volatility, while values near 1 indicate high relative volatility.
///
/// Key properties:
/// - Range: [0, 1] (normalized)
/// - 0.0 indicates lowest relative volatility in lookback period
/// - 1.0 indicates highest relative volatility in lookback period
/// - 0.5 indicates mid-range volatility when no normalization range exists
/// </remarks>
[SkipLocalsInit]
public sealed class Bbwn : AbstractBase
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
    /// Creates BBWN with specified period, multiplier, and lookback.
    /// </summary>
    /// <param name="period">Lookback period for BB calculations (must be > 0)</param>
    /// <param name="multiplier">Standard deviation multiplier (must be > 0)</param>
    /// <param name="lookback">Historical lookback period for normalization (must be > 0)</param>
    public Bbwn(int period, double multiplier = 2.0, int lookback = 252)
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
        Name = $"Bbwn({period},{multiplier:F1},{lookback})";
        WarmupPeriod = period + lookback;
    }

    /// <summary>
    /// Creates BBWN with specified source, period, multiplier, and lookback.
    /// </summary>
    public Bbwn(ITValuePublisher source, int period, double multiplier = 2.0, int lookback = 252) : this(period, multiplier, lookback)
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
    /// Historical lookback period for normalization.
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

        // Add BBW to history buffer for normalization
        if (isNew)
        {
            _bbwBuffer.Add(bbw);
        }
        else
        {
            _bbwBuffer.UpdateNewest(bbw);
        }

        // Normalize BBW to [0,1] range using historical min/max
        double bbwn = 0.5; // Default when no range exists
        if (_bbwBuffer.Count >= 1)
        {
            double minBbw = double.MaxValue;
            double maxBbw = double.MinValue;

            for (int i = 0; i < _bbwBuffer.Count; i++)
            {
                double histBbw = _bbwBuffer[i];
                if (double.IsFinite(histBbw))
                {
                    minBbw = Math.Min(minBbw, histBbw);
                    maxBbw = Math.Max(maxBbw, histBbw);
                }
            }

            double range = maxBbw - minBbw;
            if (range > 0 && double.IsFinite(range))
            {
                bbwn = (bbw - minBbw) / range;
            }
        }

        // Clamp to [0,1] range
        bbwn = Math.Max(0.0, Math.Min(1.0, bbwn));

        Last = new TValue(input.Time, bbwn);
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
    /// Calculates BBWN for entire series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period, double multiplier = 2.0, int lookback = 252)
    {
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
    /// Batch BBWN calculation with O(1) rolling variance and normalization.
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
        var bbwHistory = new RingBuffer(lookback);

        for (int i = 0; i < len; i++)
        {
            double val = source[i];

            // Add new value
            sum += val;
            sumSq += val * val;

            // Remove oldest if past warmup
            if (i >= period)
            {
                double oldest = source[i - period];
                sum -= oldest;
                sumSq -= oldest * oldest;
            }

            // Calculate BBW
            int count = Math.Min(i + 1, period);
            double mean = sum / count;
            double variance = Math.Max(0.0, (sumSq / count) - (mean * mean));
            double stddev = Math.Sqrt(variance);
            double bbw = mult2 * stddev;

            // Add to BBW history
            bbwHistory.Add(bbw);

            // Normalize BBW to [0,1] range
            double bbwn = 0.5; // Default
            if (bbwHistory.Count >= 1)
            {
                double minBbw = double.MaxValue;
                double maxBbw = double.MinValue;

                for (int j = 0; j < bbwHistory.Count; j++)
                {
                    double histBbw = bbwHistory[j];
                    // Only update min/max with finite values to prevent NaN/Infinity corruption
                    if (double.IsFinite(histBbw))
                    {
                        minBbw = Math.Min(minBbw, histBbw);
                        maxBbw = Math.Max(maxBbw, histBbw);
                    }
                }

                double range = maxBbw - minBbw;
                if (range > 0 && double.IsFinite(range))
                {
                    bbwn = (bbw - minBbw) / range;
                }
            }

            // Clamp to [0,1] range
            output[i] = Math.Max(0.0, Math.Min(1.0, bbwn));
        }
    }

    public static (TSeries Results, Bbwn Indicator) Calculate(TSeries source, int period, double multiplier = 2.0, int lookback = 252)
    {
        var indicator = new Bbwn(period, multiplier, lookback);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}