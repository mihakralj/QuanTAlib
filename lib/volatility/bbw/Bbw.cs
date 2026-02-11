using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BBW: Bollinger Band Width (Normalized)
/// </summary>
/// <remarks>
/// Measures the normalized width between upper and lower Bollinger Bands as a
/// fraction of the SMA. BBW quantifies volatility relative to price level and is
/// useful for identifying "squeeze" conditions (low volatility) that often precede
/// significant price moves.
///
/// Formula:
/// <c>BBW = (2 × multiplier × StdDev(source, period)) / SMA(source, period)</c>
///
/// Since Bollinger Bands are calculated as SMA ± (multiplier × StdDev), the raw width
/// is 2 × multiplier × StdDev. This implementation normalizes by dividing by the SMA,
/// expressing the band width as a percentage/fraction of the mean price. This makes
/// BBW comparable across instruments with different price levels.
///
/// This implementation uses O(1) running variance calculation via the sum-of-squares
/// method, with periodic resynchronization to prevent floating-point drift.
///
/// Key properties:
/// - Always non-negative (output is normalized as fraction of SMA)
/// - High BBW indicates high relative volatility
/// - Low BBW indicates low relative volatility ("squeeze")
/// - Default: 20-period, 2.0 multiplier (same as standard Bollinger Bands)
/// </remarks>
[SkipLocalsInit]
public sealed class Bbw : AbstractBase
{
    private readonly int _period;
    private readonly double _multiplier;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Sum,
        double SumSq,
        double LastValid);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;
    private int _tickCount;

    /// <summary>
    /// Creates BBW with specified period and multiplier.
    /// </summary>
    /// <param name="period">Lookback period (must be > 0)</param>
    /// <param name="multiplier">Standard deviation multiplier (must be > 0)</param>
    public Bbw(int period, double multiplier = 2.0)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (multiplier <= 0)
        {
            throw new ArgumentException("Multiplier must be greater than 0", nameof(multiplier));
        }

        _period = period;
        _multiplier = multiplier;
        _buffer = new RingBuffer(period);
        Name = $"Bbw({period},{multiplier:F1})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates BBW with specified source, period, and multiplier.
    /// </summary>
    public Bbw(ITValuePublisher source, int period, double multiplier = 2.0) : this(period, multiplier)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Period of the indicator.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Standard deviation multiplier.
    /// </summary>
    public double Multiplier => _multiplier;

    /// <inheritdoc/>
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

            // Remove oldest value contribution if buffer full
            if (_buffer.Count == _buffer.Capacity)
            {
                double oldest = _buffer.Oldest;
                _state.Sum -= oldest;
                _state.SumSq -= oldest * oldest;
            }

            // Add new value
            _state.Sum += value;
            _state.SumSq += value * value;
            _buffer.Add(value);

            _tickCount++;
            if (_buffer.IsFull && _tickCount >= ResyncInterval)
            {
                _tickCount = 0;
                RecalculateSums();
            }
        }
        else
        {
            _state = _p_state;

            // Update the newest value in buffer
            _buffer.UpdateNewest(value);
            RecalculateSums();
        }

        // Calculate variance: Var = E[X²] - E[X]²
        int count = _buffer.Count;
        double mean = _state.Sum / count;
        double variance = Math.Max(0.0, (_state.SumSq / count) - (mean * mean));
        double stddev = Math.Sqrt(variance);

        // BBW = 2 × multiplier × StdDev / SMA (normalized band width)
        // Guard against division by zero
        double bbw = mean > 0 ? (2.0 * _multiplier * stddev) / mean : 0.0;

        Last = new TValue(input.Time, bbw);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <inheritdoc/>
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period, _multiplier);
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

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        _tickCount = 0;
        Last = default;
    }

    /// <summary>
    /// Calculates BBW for entire series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period, double multiplier = 2.0)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, period, multiplier);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch BBW calculation with O(1) rolling variance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, double multiplier = 2.0)
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

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double sum = 0.0;
        double sumSq = 0.0;
        double mult2 = 2.0 * multiplier;
        double lastValid = 0.0;

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

            // Calculate variance and BBW
            int count = Math.Min(i + 1, period);
            double mean = sum / count;
            double variance = Math.Max(0.0, (sumSq / count) - (mean * mean));
            double stddev = Math.Sqrt(variance);

            // BBW = 2 × multiplier × StdDev / SMA (normalized)
            output[i] = mean > 0 ? (mult2 * stddev) / mean : 0.0;
        }
    }

    public static (TSeries Results, Bbw Indicator) Calculate(TSeries source, int period, double multiplier = 2.0)
    {
        var indicator = new Bbw(period, multiplier);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}