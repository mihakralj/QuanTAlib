using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// INERTIA: Inertia Oscillator
/// </summary>
/// <remarks>
/// Measures the raw distance between the current price and the
/// Time Series Forecast (linear regression endpoint):
/// <c>Inertia = source − TSF</c>
///
/// Positive values indicate price is above the regression line (bullish inertia);
/// negative values indicate price is below (bearish inertia).
///
/// Uses O(1) incremental sumY / sumXY maintenance from the PineScript reference.
///
/// References:
///   Donald Dorsey, "Relative Volatility Index", Technical Analysis of Stocks &amp; Commodities, 1993
///   PineScript reference: inertia.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Inertia : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    // Precomputed linear regression constants (full window)
    private readonly double _sumX;     // 0 + 1 + ... + (period-1)
    private readonly double _denomX;   // period * sumX2 - sumX²

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SumY,
        double SumXY,
        int Count,
        double LastValid);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;
    private int _tickCount;

    /// <summary>
    /// Creates Inertia with specified period.
    /// </summary>
    /// <param name="period">Lookback period for linear regression (must be &gt; 0)</param>
    public Inertia(int period = 20)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Inertia({period})";
        WarmupPeriod = period;

        _sumX = period * (period - 1) / 2.0;
        double sumX2 = period * (period - 1.0) * (2.0 * period - 1.0) / 6.0;
        _denomX = period * sumX2 - _sumX * _sumX;
    }

    /// <summary>
    /// Creates Inertia with specified source and period.
    /// </summary>
    public Inertia(ITValuePublisher source, int period = 20) : this(period)
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

            // O(1) incremental sumXY maintenance (PineScript algorithm)
            if (_buffer.Count == _buffer.Capacity)
            {
                double oldest = _buffer.Oldest;
                _state.SumY -= oldest;
                _state.SumXY -= _state.SumY;
                _state.SumXY += (_period - 1) * value;
            }
            else
            {
                _state.SumXY += _state.Count * value;
                _state.Count++;
            }

            _state.SumY += value;
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
        double slope = (_period * _state.SumXY - _sumX * _state.SumY) / _denomX;
        double intercept = (_state.SumY - slope * _sumX) / _period;
        double tsf = Math.FusedMultiplyAdd(slope, _period - 1, intercept);

        // Inertia = source - TSF (raw residual, no normalization)
        double inertia = value - tsf;

        Last = new TValue(input.Time, inertia);
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
        _tickCount = 0;
        Last = default;
    }

    /// <summary>
    /// Calculates Inertia for entire series.
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
    /// Batch Inertia calculation with O(1) incremental linear regression.
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

        double sumX = period * (period - 1) / 2.0;
        double sumX2 = period * (period - 1.0) * (2.0 * period - 1.0) / 6.0;
        double denomX = period * sumX2 - sumX * sumX;

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

            double slope = (period * sumXY - sumX * sumY) / denomX;
            double intercept = (sumY - slope * sumX) / period;
            double tsf = Math.FusedMultiplyAdd(slope, period - 1, intercept);

            output[i] = val - tsf;
        }
    }

    /// <summary>
    /// Calculates Inertia for a series, returning both results and the indicator instance.
    /// </summary>
    public static (TSeries Results, Inertia Indicator) Calculate(TSeries source, int period = 20)
    {
        var indicator = new Inertia(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
