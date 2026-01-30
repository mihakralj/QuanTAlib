using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SINEMA: Sine-Weighted Moving Average
/// </summary>
/// <remarks>
/// <para>SINEMA applies sine-wave weighting to data points within the lookback window.
/// Weights are calculated as sin(π * (i+1) / period) for each position i, creating a
/// smooth bell-shaped weighting that emphasizes middle values while gracefully
/// tapering at the edges.</para>
/// <para>Calculation:
/// w[i] = sin(π * (i+1) / period)
/// SINEMA = Σ(P[i] * w[i]) / Σ(w[i])</para>
///
/// Unlike SMA's uniform weighting or WMA's linear ramp, sine weighting provides
/// a smooth transition that can reduce high-frequency noise while preserving
/// mid-frequency trends.
///
/// IsHot:
/// Becomes true when the buffer is full (period samples processed).
/// </remarks>
[SkipLocalsInit]
public sealed class Sinema : AbstractBase
{
    private readonly int _period;
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;
    private bool _disposed;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValidValue);
    private State _state;
    private State _p_state;

    /// <summary>
    /// Creates SINEMA with specified period.
    /// </summary>
    /// <param name="period">Number of values in the lookback window (must be > 0)</param>
    public Sinema(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Sinema({period})";
        WarmupPeriod = period;
        _handler = Handle;

        // Pre-calculate sine weights for full period
        _weights = new double[period];
        double sum = 0;
        for (int i = 0; i < period; i++)
        {
            _weights[i] = Math.Sin(Math.PI * (i + 1) / period);
            sum += _weights[i];
        }
        _weightSum = sum;
    }

    public Sinema(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        source.Pub += _handler;
    }

    public Sinema(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        _source = source;
        source.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode B: Streaming (Stateful)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// True if the SINEMA has enough data to produce valid results.
    /// SINEMA is "hot" when the buffer is full (has received at least 'period' values).
    /// </summary>
    public override bool IsHot => _buffer.IsFull;

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode C: Priming (The Bridge)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    /// <param name="source">Historical data</param>
    /// <param name="step">Optional time step (unused)</param>
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

        // Seed LastValidValue from history before warmup window
        _state.LastValidValue = double.NaN;
        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (double.IsFinite(source[i]))
            {
                _state.LastValidValue = source[i];
                break;
            }
        }

        // If not found, search in warmup window
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

        // Feed the RingBuffer
        for (int i = startIndex; i < source.Length; i++)
        {
            double val = GetValidValue(source[i]);
            _buffer.Add(val);
        }

        // Calculate result
        double result = CalculateFromBuffer();
        Last = new TValue(DateTime.MinValue, result);
        _p_state = _state;
    }

    /// <summary>
    /// Gets a valid input value, using last-value substitution for non-finite inputs.
    /// </summary>
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
    private double CalculateFromBuffer()
    {
        if (_buffer.Count == 0)
        {
            return double.NaN;
        }

        int count = _buffer.Count;
        double sum = 0;
        double weightSum = 0;

        // For partial buffer, recalculate weights for current count
        if (count < _period)
        {
            int idx = 0;
            foreach (double val in _buffer)
            {
                double w = Math.Sin(Math.PI * (idx + 1) / count);
                sum += val * w;
                weightSum += w;
                idx++;
            }
        }
        else
        {
            // Full buffer: use pre-calculated weights
            int idx = 0;
            foreach (double val in _buffer)
            {
                sum += val * _weights[idx];
                idx++;
            }
            weightSum = _weightSum;
        }

        return weightSum > 0 ? sum / weightSum : double.NaN;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            double val = GetValidValue(input.Value);
            _buffer.Add(val);
        }
        else
        {
            _state = _p_state;
            double val = GetValidValue(input.Value);
            _buffer.UpdateNewest(val);
        }

        double result = CalculateFromBuffer();
        Last = new TValue(input.Time, result);
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

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode A: Batch (Stateless)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Calculates SINEMA for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="period">SINEMA period</param>
    /// <returns>SINEMA series</returns>
    public static TSeries Batch(TSeries source, int period)
    {
        var sinema = new Sinema(period);
        return sinema.Update(source);
    }

    /// <summary>
    /// Calculates SINEMA in-place, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output span (must be same length as source)</param>
    /// <param name="period">SINEMA period (must be > 0)</param>
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;

        const int StackAllocThreshold = 256;
        double[]? rentedBuffer = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        double[]? rentedWeights = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;

        Span<double> buffer = rentedBuffer != null
            ? rentedBuffer.AsSpan(0, period)
            : stackalloc double[period];
        Span<double> weights = rentedWeights != null
            ? rentedWeights.AsSpan(0, period)
            : stackalloc double[period];

        try
        {
            double lastValid = double.NaN;

            // Find first valid value to seed lastValid
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(source[k]))
                {
                    lastValid = source[k];
                    break;
                }
            }

            int bufferIndex = 0;
            int i = 0;

            // Warmup phase: buffer not yet full
            int warmupEnd = Math.Min(period, len);
            for (; i < warmupEnd; i++)
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

                buffer[i] = val;

                // Calculate weights for current count
                int count = i + 1;
                double sum = 0;
                double weightSum = 0;
                for (int j = 0; j < count; j++)
                {
                    double w = Math.Sin(Math.PI * (j + 1) / count);
                    sum += buffer[j] * w;
                    weightSum += w;
                }

                output[i] = weightSum > 0 ? sum / weightSum : val;
            }

            // Pre-calculate full-period weights
            double fullWeightSum = 0;
            for (int j = 0; j < period; j++)
            {
                weights[j] = Math.Sin(Math.PI * (j + 1) / period);
                fullWeightSum += weights[j];
            }

            // Steady-state: buffer is full, use sliding window
            for (; i < len; i++)
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

                buffer[bufferIndex] = val;
                bufferIndex++;
                if (bufferIndex >= period)
                {
                    bufferIndex = 0;
                }

                // Calculate weighted sum using circular buffer
                double sum = 0;
                int bufIdx = bufferIndex;
                for (int j = 0; j < period; j++)
                {
                    sum += buffer[bufIdx] * weights[j];
                    bufIdx++;
                    if (bufIdx >= period)
                    {
                        bufIdx = 0;
                    }
                }

                output[i] = sum / fullWeightSum;
            }
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<double>.Shared.Return(rentedBuffer);
            }

            if (rentedWeights != null)
            {
                ArrayPool<double>.Shared.Return(rentedWeights);
            }
        }
    }

    /// <summary>
    /// Runs a batch calculation on history and returns a "Hot" Sinema instance
    /// ready to process the next tick immediately.
    /// </summary>
    /// <param name="source">Historical time series</param>
    /// <param name="period">SINEMA Period</param>
    /// <returns>A tuple containing the full calculation results and the hot indicator instance</returns>
    public static (TSeries Results, Sinema Indicator) Calculate(TSeries source, int period)
    {
        var sinema = new Sinema(period);
        TSeries results = sinema.Update(source);
        return (results, sinema);
    }

    /// <summary>
    /// Resets the SINEMA state.
    /// </summary>
    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    /// <summary>
    /// Disposes the indicator and unsubscribes from the source.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= _handler;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
