using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PSL: Psychological Line
/// </summary>
/// <remarks>
/// Percentage of up-bars over a lookback period:
/// <c>PSL = 100 × (count of up-bars in period) / period</c>
///
/// An "up-bar" is when source &gt; source[1].
/// Uses a circular buffer storing 1.0 (up) or 0.0 (down/unchanged) with running sum.
/// Output range: [0, 100].
///
/// References:
///   Japanese technical analysis tradition
///   PineScript reference: psl.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Psl : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double UpSum,
        double PrevValue,
        double LastValid,
        int Count);
    private State _state;
    private State _p_state;

    /// <summary>
    /// Creates Psychological Line with specified period.
    /// </summary>
    /// <param name="period">Lookback period (must be &gt; 0)</param>
    public Psl(int period = 12)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Psl({period})";
        WarmupPeriod = period;
        _state = new State(UpSum: 0, PrevValue: double.NaN, LastValid: 0, Count: 0);
        _p_state = _state;
    }

    /// <summary>
    /// Creates PSL with specified source and period.
    /// </summary>
    public Psl(ITValuePublisher source, int period = 12) : this(period)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <inheritdoc/>
    public override bool IsHot => _buffer.IsFull;

    /// <summary>Period of the indicator.</summary>
    public int Period => _period;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double value = input.Value;

        if (!double.IsFinite(value))
        {
            value = double.IsFinite(_state.LastValid) ? _state.LastValid : 0.0;
        }
        else
        {
            _state.LastValid = value;
        }

        double upVal = double.IsFinite(_state.PrevValue) && value > _state.PrevValue ? 1.0 : 0.0;

        if (isNew)
        {
            if (_buffer.IsFull)
            {
                _state.UpSum -= _buffer[0];
            }
            _state.UpSum += upVal;
            _buffer.Add(upVal);
            _state.PrevValue = value;
            _state.Count = _buffer.Count;
        }
        else
        {
            _buffer.UpdateNewest(upVal);
            // Recompute sum from buffer to avoid drift from mismatched eviction state
            double sum = 0;
            for (int j = 0; j < _buffer.Count; j++)
            {
                sum += _buffer[j];
            }
            _state.UpSum = sum;
            _state.PrevValue = value;
            _state.Count = _buffer.Count;
        }

        double result = 100.0 * _state.UpSum / Math.Max(1, _state.Count);

        Last = new TValue(input.Time, result);
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

        Batch(source.Values, CollectionsMarshal.AsSpan(v), _period);
        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

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
        _state = new State(UpSum: 0, PrevValue: double.NaN, LastValid: 0, Count: 0);
        _p_state = _state;
        Last = default;
    }

    /// <summary>Calculates PSL for entire series.</summary>
    public static TSeries Batch(TSeries source, int period = 12)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        Batch(source.Values, CollectionsMarshal.AsSpan(v), period);
        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    /// <summary>Batch PSL via circular buffer with running sum of up-bars.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 12)
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
        double upSum = 0.0;
        double lastValid = 0.0;
        double prevValue = double.NaN;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val)) { val = lastValid; } else { lastValid = val; }

            double upVal = double.IsFinite(prevValue) && val > prevValue ? 1.0 : 0.0;

            if (buffer.IsFull) { upSum -= buffer[0]; }
            upSum += upVal;
            buffer.Add(upVal);
            prevValue = val;

            output[i] = 100.0 * upSum / Math.Max(1, buffer.Count);
        }
    }

    /// <summary>Creates a PSL indicator, processes source, returns results with indicator.</summary>
    public static (TSeries Results, Psl Indicator) Calculate(TSeries source, int period = 12)
    {
        var indicator = new Psl(period);
        return (indicator.Update(source), indicator);
    }
}
