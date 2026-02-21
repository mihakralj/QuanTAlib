using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// KRI: Kairi Relative Index
/// </summary>
/// <remarks>
/// Percentage deviation of the current price from its Simple Moving Average:
/// <c>KRI = 100 × (source − SMA) / SMA</c>
///
/// Uses a circular buffer with running sum for O(1) per-bar updates.
/// Positive KRI indicates price is above its average (bullish);
/// negative indicates price is below (bearish).
///
/// References:
///   Japanese technical analysis tradition
///   PineScript reference: kri.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Kri : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Sum,
        double LastValid,
        int Count);
    private State _state;
    private State _p_state;

    /// <summary>
    /// Creates Kairi Relative Index with specified period.
    /// </summary>
    /// <param name="period">SMA lookback period (must be &gt; 0)</param>
    public Kri(int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Kri({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates KRI with specified source and period.
    /// </summary>
    public Kri(ITValuePublisher source, int period = 14) : this(period)
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

        if (isNew)
        {
            if (_buffer.IsFull)
            {
                _state.Sum -= _buffer[0];
            }
            _state.Sum += value;
            _buffer.Add(value);
            _state.Count = _buffer.Count;
        }
        else
        {
            _buffer.UpdateNewest(value);
            // Recompute sum from buffer to avoid drift from mismatched eviction state
            double sum = 0;
            for (int j = 0; j < _buffer.Count; j++)
            {
                sum += _buffer[j];
            }
            _state.Sum = sum;
            _state.Count = _buffer.Count;
        }

        double sma = _state.Sum / Math.Max(1, _state.Count);
        double kri = sma != 0.0 ? 100.0 * (value - sma) / sma : 0.0;

        Last = new TValue(input.Time, kri);
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
        _state = default;
        _p_state = default;
        Last = default;
    }

    /// <summary>Calculates KRI for entire series.</summary>
    public static TSeries Batch(TSeries source, int period = 14)
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

    /// <summary>Batch KRI via circular buffer with running sum.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 14)
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
        double sum = 0.0;
        double lastValid = 0.0;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val)) { val = lastValid; } else { lastValid = val; }

            if (buffer.IsFull) { sum -= buffer[0]; }
            sum += val;
            buffer.Add(val);

            double sma = sum / Math.Max(1, buffer.Count);
            output[i] = sma != 0.0 ? 100.0 * (val - sma) / sma : 0.0;
        }
    }

    /// <summary>Creates a KRI indicator, processes source, returns results with indicator.</summary>
    public static (TSeries Results, Kri Indicator) Calculate(TSeries source, int period = 14)
    {
        var indicator = new Kri(period);
        return (indicator.Update(source), indicator);
    }
}
