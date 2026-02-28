using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// FISHER: Fisher Transform
/// </summary>
/// <remarks>
/// Converts price into a Gaussian normal distribution via the inverse
/// hyperbolic tangent with IIR feedback, producing sharp turning points:
/// <c>Fisher = atanh(v) + 0.5 × Fish[1]</c>
/// where <c>v</c> is the EMA-smoothed normalized price clamped to (−0.999, 0.999).
///
/// Normalization maps price to [−1, 1] using highest/lowest over <c>period</c> bars.
/// Signal line (Trigger) is the previous bar's Fisher value: <c>Fish[1]</c>.
///
/// References:
///   John Ehlers, "Using The Fisher Transform", 2002
///   PineScript reference: fisher.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Fisher : AbstractBase
{
    private readonly int _period;
    private readonly double _alpha;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Value,
        double FisherValue,
        double Signal,
        double LastValid,
        int Count);
    private State _state;
    private State _p_state;

    /// <summary>
    /// Creates Fisher Transform with specified period.
    /// </summary>
    /// <param name="period">Lookback period for min/max normalization (must be &gt; 0)</param>
    /// <param name="alpha">EMA smoothing factor (0 &lt; alpha &lt;= 1, default 0.33)</param>
    public Fisher(int period = 10, double alpha = 0.33)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (alpha is <= 0 or > 1)
        {
            throw new ArgumentException("Alpha must be in (0, 1]", nameof(alpha));
        }

        _period = period;
        _alpha = alpha;
        _buffer = new RingBuffer(period);
        Name = $"Fisher({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates Fisher Transform with specified source and period.
    /// </summary>
    public Fisher(ITValuePublisher source, int period = 10, double alpha = 0.33) : this(period, alpha)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    /// Current Fisher Transform value.
    /// </summary>
    public double FisherValue => _state.FisherValue;

    /// <summary>
    /// Current Signal line value.
    /// </summary>
    public double Signal => _state.Signal;

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
            _buffer.Add(value);
            _state.Count++;
        }
        else
        {
            _state = _p_state;
            _buffer.UpdateNewest(value);
        }

        // Find min/max over the buffer
        double highest = double.MinValue;
        double lowest = double.MaxValue;
        int count = _buffer.Count;
        for (int i = 0; i < count; i++)
        {
            double v = _buffer[i];
            if (v > highest)
            {
                highest = v;
            }
            if (v < lowest)
            {
                lowest = v;
            }
        }

        // Ehlers/Skender normalization
        double range = highest - lowest;
        if (range != 0.0)
        {
            _state.Value = (0.66 * (((value - lowest) / range) - 0.5))
                + (0.67 * _state.Value);
        }
        else
        {
            _state.Value = 0.0;  // Skender: xv[i] = 0 when range=0
        }

        // Ehlers/Skender: snap to ±0.999 when |Value1| > 0.99
        // Clamped value MUST be stored back — Skender stores array2[i] clamped,
        // so next iteration's IIR feedback (0.67 * xv[i-1]) uses the clamped value.
        if (_state.Value > 0.99)
        {
            _state.Value = 0.999;
        }
        else if (_state.Value < -0.99)
        {
            _state.Value = -0.999;
        }

        // Ehlers 2002: Fish = arctanh(Value1) + 0.5 * Fish[1]  (IIR feedback)
        double fisher = (0.5 * Math.Log((1.0 + _state.Value) / (1.0 - _state.Value)))
            + (0.5 * _state.FisherValue);

        // Signal line: previous bar's Fisher value (Fish[1])
        _state.Signal = _state.FisherValue;
        _state.FisherValue = fisher;

        Last = new TValue(input.Time, fisher);
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

        Batch(source.Values, vSpan, _period, _alpha);
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
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    /// <summary>
    /// Calculates Fisher Transform for entire series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 10, double alpha = 0.33)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, period, alpha);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch Fisher Transform with O(period) streaming min/max.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 10, double alpha = 0.33)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (alpha <= 0 || alpha > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be in the range (0, 1].");
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        var buffer = new RingBuffer(period);
        double emaValue = 0.0;
        double fisherValue = 0.0;
        double lastValid = 0.0;

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

            buffer.Add(val);

            // Find min/max
            double highest = double.MinValue;
            double lowest = double.MaxValue;
            int count = buffer.Count;
            for (int j = 0; j < count; j++)
            {
                double v = buffer[j];
                if (v > highest)
                {
                    highest = v;
                }
                if (v < lowest)
                {
                    lowest = v;
                }
            }

            // Ehlers/Skender normalization
            double range = highest - lowest;
            if (range != 0.0)
            {
                emaValue = (0.66 * (((val - lowest) / range) - 0.5))
                    + (0.67 * emaValue);
            }
            else
            {
                emaValue = 0.0;  // Skender: xv[i] = 0 when range=0
            }

            // Ehlers/Skender: snap to ±0.999 when |Value1| > 0.99
            // Clamped value stored back — Skender stores array2[i] clamped,
            // so next iteration's IIR feedback (0.67 * xv[i-1]) uses the clamped value.
            if (emaValue > 0.99)
            {
                emaValue = 0.999;
            }
            else if (emaValue < -0.99)
            {
                emaValue = -0.999;
            }

            // Ehlers 2002: Fish = arctanh(Value1) + 0.5 * Fish[1]  (IIR feedback)
            fisherValue = (0.5 * Math.Log((1.0 + emaValue) / (1.0 - emaValue)))
                + (0.5 * fisherValue);

            output[i] = fisherValue;
        }
    }

    /// <summary>
    /// Creates a Fisher Transform indicator, processes the source, and returns results with the indicator.
    /// </summary>
    public static (TSeries Results, Fisher Indicator) Calculate(TSeries source, int period = 10, double alpha = 0.33)
    {
        var indicator = new Fisher(period, alpha);
        return (indicator.Update(source), indicator);
    }
}
