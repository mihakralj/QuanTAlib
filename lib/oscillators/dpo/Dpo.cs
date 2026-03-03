using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DPO: Detrended Price Oscillator
/// </summary>
/// <remarks>
/// Removes the trend component from price by subtracting a displaced SMA,
/// isolating short-term cycles:
/// <c>DPO = price − SMA[displacement]</c>
/// where <c>displacement = floor(period / 2) + 1</c>.
///
/// Uses O(1) streaming via RingBuffer running sum for SMA and a second
/// RingBuffer to store SMA history for the displacement lookback.
///
/// References:
///   William Blau, "Momentum, Direction, and Divergence", 1995
///   PineScript reference: dpo.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Dpo : AbstractBase
{
    private readonly int _period;
    private readonly int _displacement;
    private readonly RingBuffer _smaBuffer;
    private readonly RingBuffer _smaHistory;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        int Count,
        double LastValid);
    private State _state;
    private State _p_state;

    /// <summary>
    /// Creates DPO with specified period.
    /// </summary>
    /// <param name="period">Lookback period for SMA calculation (must be &gt; 0)</param>
    public Dpo(int period = 20)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _displacement = (period / 2) + 1;
        _smaBuffer = new RingBuffer(period);
        _smaHistory = new RingBuffer(_displacement + 1);
        Name = $"Dpo({period})";
        WarmupPeriod = period + _displacement;
    }

    /// <summary>
    /// Creates DPO with specified source and period.
    /// </summary>
    public Dpo(ITValuePublisher source, int period = 20) : this(period)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _state.Count >= WarmupPeriod;

    /// <summary>
    /// Period of the indicator.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Displacement of the SMA lookback.
    /// </summary>
    public int Displacement => _displacement;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
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
            _p_state = _state;
            _smaBuffer.Snapshot();
            _smaHistory.Snapshot();

            _smaBuffer.Add(value);
            _state.Count++;

            if (_smaBuffer.IsFull)
            {
                double sma = _smaBuffer.Sum / _period;
                _smaHistory.Add(sma);
            }
        }
        else
        {
            _state = _p_state;
            _smaBuffer.Restore();
            _smaHistory.Restore();

            // skipcq:CS-R1140 - Mirror isNew=true path: Restore undoes the Add, so re-Add the corrected value
            _smaBuffer.Add(value);
            _state.Count++;

            if (_smaBuffer.IsFull)
            {
                double sma = _smaBuffer.Sum / _period;
                _smaHistory.Add(sma);
            }
        }

        double result;
        if (_smaHistory.IsFull)
        {
            double displacedSma = _smaHistory.Oldest;
            result = value - displacedSma;
        }
        else
        {
            result = 0.0;
        }

        Last = new TValue(input.Time, result);
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
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }
    public override void Reset()
    {
        _smaBuffer.Clear();
        _smaHistory.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    /// <summary>
    /// Calculates DPO for entire series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 20)
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
    /// Batch DPO calculation with O(1) streaming SMA and displacement.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 20)
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

        int displacement = (period / 2) + 1;

        var smaBuffer = new RingBuffer(period);
        var smaHistory = new RingBuffer(displacement + 1);
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

            smaBuffer.Add(val);

            if (smaBuffer.IsFull)
            {
                double sma = smaBuffer.Sum / period;
                smaHistory.Add(sma);
            }

            if (smaHistory.IsFull)
            {
                output[i] = val - smaHistory.Oldest;
            }
            else
            {
                output[i] = 0.0;
            }
        }
    }

    /// <summary>
    /// Creates DPO indicator and calculates results for the source series.
    /// </summary>
    public static (TSeries Results, Dpo Indicator) Calculate(TSeries source, int period = 20)
    {
        var indicator = new Dpo(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
