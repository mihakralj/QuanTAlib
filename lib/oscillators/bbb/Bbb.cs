using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BBB: Bollinger %B
/// </summary>
/// <remarks>
/// <para>
/// Bollinger %B measures where price sits within Bollinger Bands:
/// <c>%B = (Price - Lower) / (Upper - Lower)</c>
/// </para>
///
/// This implementation uses O(1) rolling sums for mean and variance.
///
/// Formula:
/// <c>Basis = SMA(source, period)</c>
/// <c>StdDev = sqrt(E[x^2] - E[x]^2)</c>
/// <c>Upper = Basis + multiplier * StdDev</c>
/// <c>Lower = Basis - multiplier * StdDev</c>
/// <c>BBB = (source - Lower) / (Upper - Lower)</c>
///
/// When band width is zero, returns 0.5 (neutral).
///
/// References:
/// - John Bollinger, "Bollinger on Bollinger Bands"
/// - PineScript reference: bbb.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Bbb : AbstractBase
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
    /// Creates BBB with specified period and multiplier.
    /// </summary>
    /// <param name="period">Lookback period (must be &gt; 0)</param>
    /// <param name="multiplier">Standard deviation multiplier (must be &gt; 0)</param>
    public Bbb(int period = 20, double multiplier = 2.0)
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
        Name = $"Bbb({period},{multiplier:F1})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates BBB with specified source, period, and multiplier.
    /// </summary>
    public Bbb(ITValuePublisher source, int period = 20, double multiplier = 2.0) : this(period, multiplier)
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
        double dev = _multiplier * stddev;

        double upper = mean + dev;
        double lower = mean - dev;
        double width = upper - lower;

        double bbb = width > 0.0 ? (value - lower) / width : 0.5;

        Last = new TValue(input.Time, bbb);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <inheritdoc/>
    public override TSeries Update(TSeries source)
    {
        Reset();
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        source.Times.CopyTo(tSpan);

        for (int i = 0; i < len; i++)
        {
            vSpan[i] = Update(new TValue(tSpan[i], source.Values[i]), isNew: true).Value;
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates BBB for entire series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 20, double multiplier = 2.0)
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
    /// Batch BBB calculation with O(1) rolling variance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 20, double multiplier = 2.0)
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
        double lastValid = 0.0;
        double mult = multiplier;

        var valueBuffer = new RingBuffer(period);

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

            if (i >= period)
            {
                double oldest = valueBuffer.Oldest;
                sum -= oldest;
                sumSq -= oldest * oldest;
            }

            sum += val;
            sumSq += val * val;
            valueBuffer.Add(val);

            int count = Math.Min(i + 1, period);
            double mean = sum / count;
            double variance = Math.Max(0.0, (sumSq / count) - (mean * mean));
            double stddev = Math.Sqrt(variance);
            double dev = mult * stddev;

            double upper = mean + dev;
            double lower = mean - dev;
            double width = upper - lower;

            output[i] = width > 0.0 ? (val - lower) / width : 0.5;
        }
    }

    /// <summary>
    /// Calculates BBB and returns both results and the warm indicator.
    /// </summary>
    public static (TSeries Results, Bbb Indicator) Calculate(TSeries source, int period = 20, double multiplier = 2.0)
    {
        var indicator = new Bbb(period, multiplier);
        TSeries results = indicator.Update(source);
        return (results, indicator);
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
}
