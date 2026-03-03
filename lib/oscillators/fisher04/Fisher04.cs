using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// FISHER04: Ehlers Fisher Transform (2004 Cybernetic Analysis)
/// </summary>
/// <remarks>
/// Implements the revised Fisher Transform from Ehlers' "Cybernetic Analysis
/// for Stocks and Futures" (Wiley, 2004), Chapter 1. This version uses wider
/// normalization and gentler arctanh scaling than the original 2002 TASC article:
///
/// <c>Value1 = 0.5 × 2 × ((Price − MinL)/(MaxH − MinL) − 0.5) + 0.5 × Value1[1]</c>
/// <c>Fish = 0.25 × ln((1 + Value1)/(1 − Value1)) + 0.5 × Fish[1]</c>
///
/// Key differences from Fisher (2002):
///   • Normalization coefficient: 1.0 (vs 0.66)
///   • IIR feedback on Value1: 0.5 (vs 0.67)
///   • Clamp threshold: 0.9999 (vs 0.99→0.999)
///   • Fisher multiplier: 0.25 (vs 0.5)
///   • Fisher IIR: 0.5 (same)
///
/// References:
///   John Ehlers, "Cybernetic Analysis for Stocks and Futures", Wiley, 2004
///   PineScript reference: fisher04.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Fisher04 : AbstractBase
{
    private readonly int _period;
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
    /// Creates Fisher04 Transform with specified period.
    /// </summary>
    /// <param name="period">Lookback period for min/max normalization (must be &gt; 0)</param>
    public Fisher04(int period = 10)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Fisher04({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates Fisher04 Transform with specified source and period.
    /// </summary>
    public Fisher04(ITValuePublisher source, int period = 10) : this(period)
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

        // Ehlers 2004 normalization: Value1 = 1.0 * ((price-low)/range - 0.5) + 0.5 * Value1[1]
        double range = highest - lowest;
        if (range != 0.0)
        {
            _state.Value = (((value - lowest) / range) - 0.5)
                + (0.5 * _state.Value);
        }
        else
        {
            _state.Value = 0.0;
        }

        // Ehlers 2004: clamp to ±0.9999
        if (_state.Value > 0.9999)
        {
            _state.Value = 0.9999;
        }
        else if (_state.Value < -0.9999)
        {
            _state.Value = -0.9999;
        }

        // Ehlers 2004: Fish = 0.25 * arctanh(Value1) + 0.5 * Fish[1]
        double fisher = (0.25 * Math.Log((1.0 + _state.Value) / (1.0 - _state.Value)))
            + (0.5 * _state.FisherValue);

        // Signal line: previous bar's Fisher value (Fish[1])
        _state.Signal = _state.FisherValue;
        _state.FisherValue = fisher;

        Last = new TValue(input.Time, fisher);
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

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromTicks(1);
        DateTime baseTime = DateTime.UtcNow - (interval * (source.Length - 1));
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(baseTime + (interval * i), source[i]), isNew: true);
        }
    }
    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    /// <summary>
    /// Calculates Fisher04 Transform for entire series.
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
    /// Batch Fisher04 Transform with O(period) streaming min/max.
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

            // Ehlers 2004 normalization: 1.0 * ((val-low)/range - 0.5) + 0.5 * prev
            double range = highest - lowest;
            if (range != 0.0)
            {
                emaValue = (((val - lowest) / range) - 0.5)
                    + (0.5 * emaValue);
            }
            else
            {
                emaValue = 0.0;
            }

            // Ehlers 2004: clamp to ±0.9999
            if (emaValue > 0.9999)
            {
                emaValue = 0.9999;
            }
            else if (emaValue < -0.9999)
            {
                emaValue = -0.9999;
            }

            // Ehlers 2004: Fish = 0.25 * arctanh(Value1) + 0.5 * Fish[1]
            fisherValue = (0.25 * Math.Log((1.0 + emaValue) / (1.0 - emaValue)))
                + (0.5 * fisherValue);

            output[i] = fisherValue;
        }
    }

    /// <summary>
    /// Creates a Fisher04 indicator, processes the source, and returns results with the indicator.
    /// </summary>
    public static (TSeries Results, Fisher04 Indicator) Calculate(TSeries source, int period = 10)
    {
        var indicator = new Fisher04(period);
        return (indicator.Update(source), indicator);
    }
}
