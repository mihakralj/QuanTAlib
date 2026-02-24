using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Bias (also known as Disparity Index): Measures the percentage deviation of a price from its moving average.
/// </summary>
/// <remarks>
/// Bias (BIAS) calculates how far the current price deviates from its Simple Moving Average (SMA),
/// expressed as a percentage. It's commonly used to identify overbought/oversold conditions.
///
/// Formula:
/// BIAS = (Price - SMA) / SMA = Price/SMA - 1
///
/// Key Features:
/// - O(1) time complexity per update using running sum
/// - Zero allocation in hot path
/// - Handles division by zero (returns 0 when SMA is 0)
/// - NaN/Infinity safe with last-valid-value substitution
///
/// IsHot:
/// Becomes true when the buffer is full (period samples processed).
/// </remarks>
[SkipLocalsInit]
public sealed class Bias : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Sum;
        public double LastInput;
        public double LastValidValue;
        public int TickCount;
    }

    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;
    private const double Epsilon = 1e-10;

    /// <summary>
    /// Creates Bias with specified period.
    /// </summary>
    /// <param name="period">Number of values for SMA calculation (must be > 0)</param>
    public Bias(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Bias({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    public Bias(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += _handler;
    }

    public Bias(TSeries source, int period) : this(period)
    {
        source.Pub += _handler;
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        _p_state = _state;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if Bias has enough data to produce valid results.
    /// Bias is "hot" when the buffer is full (has received at least 'period' values).
    /// </summary>
    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        // Reset state
        _buffer.Clear();
        _state = default;
        _p_state = default;

        int warmupLength = Math.Min(source.Length, WarmupPeriod);
        int startIndex = source.Length - warmupLength;

        // Seed LastValidValue
        _state.LastValidValue = double.NaN;
        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (double.IsFinite(source[i]))
            {
                _state.LastValidValue = source[i];
                break;
            }
        }

        if (double.IsNaN(_state.LastValidValue))
        {
            for (int i = startIndex; i < source.Length; i++)
            {
                if (double.IsFinite(source[i]))
                {
                    _state.LastValidValue = source[i];
                    break;
                }
            }
        }

        // Feed the buffer and calculate sum
        for (int i = startIndex; i < source.Length; i++)
        {
            double val = GetValidValue(source[i]);
            _buffer.Add(val);
            _state.Sum += val;
            _state.LastInput = val;
        }

        // Calculate final Bias
        double sma = _state.Sum / _buffer.Count;
        double bias = Math.Abs(sma) > Epsilon ? (_state.LastInput - sma) / sma : 0;
        Last = new TValue(DateTime.MinValue, bias);
        _p_state = _state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue = input;
            return input;
        }
        return _state.LastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateState(double val)
    {
        if (_buffer.Count == _buffer.Capacity)
        {
            _state.Sum -= _buffer.Oldest;
        }

        _buffer.Add(val);
        _state.Sum += val;

        _state.TickCount++;
        if (_buffer.IsFull && _state.TickCount >= ResyncInterval)
        {
            _state.TickCount = 0;
            _state.Sum = _buffer.GetSpan().SumSIMD();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _buffer.Snapshot();

            double val = GetValidValue(input.Value);
            UpdateState(val);
            _state.LastInput = val;
        }
        else
        {
            // Restore both scalar state and buffer state
            _state = _p_state;
            _buffer.Restore();

            // Use restored LastValidValue for NaN handling without updating it
            double val = double.IsFinite(input.Value) ? input.Value : _state.LastValidValue;

            // Replicate the same operation as isNew=true: UpdateState
            // This properly removes oldest and adds newest, maintaining sliding window
            UpdateState(val);
            _state.LastInput = val;
        }

        // Calculate Bias: (Price - SMA) / SMA
        double sma = _state.Sum / _buffer.Count;
        double bias = Math.Abs(sma) > Epsilon ? (_state.LastInput - sma) / sma : 0;

        Last = new TValue(input.Time, bias);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates Bias for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var bias = new Bias(period);
        return bias.Update(source);
    }

    /// <summary>
    /// Calculates Bias in-place using O(1) running sum.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
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

        CalculateScalarCore(source, output, period);
    }

    /// <summary>
    /// Runs a batch calculation and returns a "Hot" Bias instance.
    /// </summary>
    public static (TSeries Results, Bias Indicator) Calculate(TSeries source, int period)
    {
        var bias = new Bias(period);
        TSeries results = bias.Update(source);
        return (results, bias);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;

        const int StackAllocThreshold = 256;
        double[]? bufferArray = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : bufferArray!.AsSpan(0, period);

        double sum = 0;
        double lastValid = double.NaN;

        // Find first valid value
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(source[k]))
            {
                lastValid = source[k];
                break;
            }
        }

        try
        {
            int bufferIndex = 0;
            int tickCount = 0;

            // Warmup phase
            int warmupEnd = Math.Min(period, len);
            for (int i = 0; i < warmupEnd; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                {
                    lastValid = val;
                }
                else
                {
                    val = lastValid;
                }

                sum += val;
                buffer[i] = val;

                double n = i + 1;
                double sma = sum / n;
                output[i] = Math.Abs(sma) > Epsilon ? (val - sma) / sma : 0;
            }

            // Main phase with sliding window
            for (int i = period; i < len; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                {
                    lastValid = val;
                }
                else
                {
                    val = lastValid;
                }

                double oldVal = buffer[bufferIndex];
                sum = sum - oldVal + val;

                buffer[bufferIndex] = val;
                bufferIndex++;
                if (bufferIndex >= period)
                {
                    bufferIndex = 0;
                }

                double sma = sum / period;
                output[i] = Math.Abs(sma) > Epsilon ? (val - sma) / sma : 0;

                // Periodic resync for long sequences
                tickCount++;
                if (tickCount >= ResyncInterval)
                {
                    tickCount = 0;
                    sum = 0;
                    for (int k = 0; k < period; k++)
                    {
                        sum += buffer[k];
                    }
                }
            }
        }
        finally
        {
            if (bufferArray != null)
            {
                ArrayPool<double>.Shared.Return(bufferArray);
            }
        }
    }

    /// <summary>
    /// Resets the Bias state.
    /// </summary>
    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }
}