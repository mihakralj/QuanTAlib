using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CFO: Chande Forecast Oscillator (also known as FOSC)
/// </summary>
/// <remarks>
/// Measures the percentage difference between the current price and the
/// Time Series Forecast (linear regression endpoint):
/// <c>CFO = 100 Ã— (source âˆ’ TSF) / source</c>
///
/// Uses O(1) incremental sumY / sumXY maintenance from the PineScript reference.
/// When source equals zero, returns NaN to avoid division by zero.
///
/// References:
///   Tushar Chande, "The New Technical Trader", 1994
///   PineScript reference: cfo.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Cfo : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    // Precomputed linear regression constants (full window)
    private readonly double _sumX;     // 0 + 1 + ... + (period-1)
    private readonly double _denomX;   // period * sumX2 - sumXÂ²

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SumY,
        double SumXY,
        double SumYComp,
        double SumXYComp,
        int Count,
        double LastValid);
    private State _state;
    private State _p_state;

    /// <summary>
    /// Creates CFO with specified period.
    /// </summary>
    /// <param name="period">Lookback period for linear regression (must be &gt; 0)</param>
    public Cfo(int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Cfo({period})";
        WarmupPeriod = period;

        _sumX = period * (period - 1) / 2.0;
        double sumX2 = period * (period - 1.0) * ((2.0 * period) - 1.0) / 6.0;
        _denomX = (period * sumX2) - (_sumX * _sumX);
    }

    /// <summary>
    /// Creates CFO with specified source and period.
    /// </summary>
    public Cfo(ITValuePublisher source, int period = 14) : this(period)
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

            // Kahan compensated O(1) incremental sumXY maintenance
            if (_buffer.Count == _buffer.Capacity)
            {
                double oldest = _buffer.Oldest;
                // Kahan delta for SumY
                {
                    double delta = value - oldest;
                    double y = delta - _state.SumYComp;
                    double t = _state.SumY + y;
                    _state.SumYComp = (t - _state.SumY) - y;
                    _state.SumY = t;
                }
                // SumXY: net delta = -(SumY_old - oldest) + (period-1)*value
                // Since SumY already updated: SumY_old - oldest = SumY_new - value
                // So net delta = -(SumY_new - value) + (period-1)*value = -SumY_new + period*value
                {
                    double netDelta = -_state.SumY + (_period * value);
                    double y = netDelta - _state.SumXYComp;
                    double t = _state.SumXY + y;
                    _state.SumXYComp = (t - _state.SumXY) - y;
                    _state.SumXY = t;
                }
            }
            else
            {
                // Warmup: Kahan addition for SumY
                {
                    double y = value - _state.SumYComp;
                    double t = _state.SumY + y;
                    _state.SumYComp = (t - _state.SumY) - y;
                    _state.SumY = t;
                }
                // Kahan addition for SumXY
                {
                    double addXY = _state.Count * value;
                    double y = addXY - _state.SumXYComp;
                    double t = _state.SumXY + y;
                    _state.SumXYComp = (t - _state.SumXY) - y;
                    _state.SumXY = t;
                }
                _state.Count++;
            }

            _buffer.Add(value);
        }
        else
        {
            _state = _p_state;

            _buffer.UpdateNewest(value);
            RecalculateSums();
        }

        if (!_buffer.IsFull)
        {
            Last = new TValue(input.Time, 0.0);
            PubEvent(Last, isNew);
            return Last;
        }

        // Linear regression: slope, intercept, TSF
        double slope = ((_period * _state.SumXY) - (_sumX * _state.SumY)) / _denomX;
        double intercept = (_state.SumY - (slope * _sumX)) / _period;
        double tsf = Math.FusedMultiplyAdd(slope, _period - 1, intercept);

        // CFO = 100 * (source - tsf) / source
        double cfo = value == 0.0 ? double.NaN : 100.0 * (value - tsf) / value; // skipcq: CS-R1077 - Exact-zero guard: value is a price; zero means no data, division by zero produces Infinity

        Last = new TValue(input.Time, cfo);
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
        _state.SumY = 0.0;
        _state.SumXY = 0.0;
        _state.Count = _buffer.Count;
        for (int i = 0; i < _buffer.Count; i++)
        {
            double v = _buffer[i];
            _state.SumY += v;
            _state.SumXY += i * v;
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
        _state = default;
        _p_state = default;
        Last = default;
    }

    /// <summary>
    /// Calculates CFO for entire series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 14)
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
    /// Batch CFO calculation with O(1) incremental linear regression.
    /// </summary>
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

        double sumX = period * (period - 1) / 2.0;
        double sumX2 = period * (period - 1.0) * ((2.0 * period) - 1.0) / 6.0;
        double denomX = (period * sumX2) - (sumX * sumX);

        double sumY = 0.0;
        double sumXY = 0.0;
        int count = 0;
        double lastValid = 0.0;

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

            // O(1) incremental sumXY maintenance
            if (valueBuffer.Count == valueBuffer.Capacity)
            {
                double oldest = valueBuffer.Oldest;
                sumY -= oldest;
                sumXY -= sumY;
                sumXY += (period - 1) * val;
            }
            else
            {
                sumXY += count * val;
                count++;
            }

            sumY += val;
            valueBuffer.Add(val);

            if (count < period)
            {
                output[i] = 0.0;
                continue;
            }

            double slope = ((period * sumXY) - (sumX * sumY)) / denomX;
            double intercept = (sumY - (slope * sumX)) / period;
            double tsf = Math.FusedMultiplyAdd(slope, period - 1, intercept);

            output[i] = val == 0.0 ? double.NaN : 100.0 * (val - tsf) / val; // skipcq: CS-R1077 - Exact-zero guard: val is a price; zero means no data, division by zero produces Infinity
        }
    }

    public static (TSeries Results, Cfo Indicator) Calculate(TSeries source, int period = 14)
    {
        var indicator = new Cfo(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
