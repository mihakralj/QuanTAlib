using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ER: Efficiency Ratio (Kaufman)
/// </summary>
/// <remarks>
/// Measures the signal-to-noise ratio of price movement over a lookback period.
/// ER = |Price − Price[period]| / Σ|Price[i] − Price[i−1]| for i over period bars.
/// Output ranges from 0 (choppy/noisy) to 1 (perfectly trending).
///
/// Uses dual circular buffers with a running sum for O(1) per-bar updates:
///   - Close buffer (period+1): stores source values; signal = |newest − oldest|
///   - Noise buffer (period): stores |bar-to-bar change|; noise = running sum
///
/// References:
///   Perry Kaufman, "Trading Systems and Methods", 1995
///   PineScript reference: er.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Er : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _closeBuf;
    private readonly RingBuffer _noiseBuf;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double NoiseSum,
        double PrevValue,
        double LastValid,
        int Count);
    private State _state;
    private State _p_state;

    /// <summary>
    /// Creates Efficiency Ratio indicator with specified period.
    /// </summary>
    /// <param name="period">Lookback period for efficiency measurement (must be &gt; 0)</param>
    public Er(int period = 10)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _closeBuf = new RingBuffer(period + 1);
        _noiseBuf = new RingBuffer(period);
        Name = $"Er({period})";
        WarmupPeriod = period + 1;
    }

    /// <summary>
    /// Creates Efficiency Ratio with specified source and period.
    /// </summary>
    public Er(ITValuePublisher source, int period = 10) : this(period)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _closeBuf.IsFull;

    /// <summary>
    /// Period of the indicator.
    /// </summary>
    public int Period => _period;

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

            // Compute bar-to-bar absolute change
            double absChange = double.IsFinite(_state.PrevValue) ? Math.Abs(value - _state.PrevValue) : 0.0;

            // Update noise running sum: subtract oldest, add newest
            if (_noiseBuf.IsFull)
            {
                _state.NoiseSum -= _noiseBuf[0];
            }
            _state.NoiseSum += absChange;
            _noiseBuf.Add(absChange);

            // Update close buffer
            _closeBuf.Add(value);
            _state.PrevValue = value;
            _state.Count++;
        }
        else
        {
            _state = _p_state;

            double absChange = double.IsFinite(_state.PrevValue) ? Math.Abs(value - _state.PrevValue) : 0.0;

            if (_noiseBuf.IsFull)
            {
                _state.NoiseSum -= _noiseBuf[0];
            }
            _state.NoiseSum += absChange;
            _noiseBuf.UpdateNewest(absChange);

            _closeBuf.UpdateNewest(value);
            _state.PrevValue = value;
            _state.Count++;
        }

        // Signal = |current - oldest close|
        double signal = _closeBuf.Count > _period
            ? Math.Abs(value - _closeBuf[0])
            : 0.0;

        // ER = signal / noise, clamped to [0, 1]
        double er = _state.NoiseSum > 0.0 ? signal / _state.NoiseSum : 0.0;
        er = Math.Clamp(er, 0.0, 1.0);

        Last = new TValue(input.Time, er);
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

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromTicks(1);
        DateTime baseTime = DateTime.UtcNow - (interval * (source.Length - 1));
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(baseTime + (interval * i), source[i]), isNew: true);
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _closeBuf.Clear();
        _noiseBuf.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    /// <summary>
    /// Calculates Efficiency Ratio for entire series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 10)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, period);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch Efficiency Ratio calculation via dual circular buffers with running sum.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 10)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        var closeBuf = new RingBuffer(period + 1);
        var noiseBuf = new RingBuffer(period);
        double noiseSum = 0.0;
        double lastValid = 0.0;
        double prevValue = double.NaN;

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

            double absChange = double.IsFinite(prevValue) ? Math.Abs(val - prevValue) : 0.0;
            prevValue = val;

            // Update noise
            if (noiseBuf.IsFull)
            {
                noiseSum -= noiseBuf[0];
            }
            noiseSum += absChange;
            noiseBuf.Add(absChange);

            // Update close
            closeBuf.Add(val);

            // Signal
            double signal = closeBuf.Count > period
                ? Math.Abs(val - closeBuf[0])
                : 0.0;

            double er = noiseSum > 0.0 ? signal / noiseSum : 0.0;
            output[i] = Math.Clamp(er, 0.0, 1.0);
        }
    }

    /// <summary>
    /// Creates an ER indicator, processes the source, and returns results with the indicator.
    /// </summary>
    public static (TSeries Results, Er Indicator) Calculate(TSeries source, int period = 10)
    {
        var indicator = new Er(period);
        return (indicator.Update(source), indicator);
    }
}
