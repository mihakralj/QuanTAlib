using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// ALMA: Arnaud Legoux Moving Average
/// </summary>
/// <remarks>
/// ALMA uses a Gaussian distribution to determine weights for the moving average.
/// It allows for adjusting smoothness and responsiveness via Offset and Sigma parameters.
///
/// Formula:
/// Weights are calculated using the Gaussian function:
/// W_i = exp( - (i - offset)^2 / (2 * sigma^2) )
/// where:
/// offset = floor(period * offset_param)
/// sigma = period / sigma_param
///
/// The final ALMA is the weighted sum of the price window divided by the sum of weights.
/// </remarks>
[SkipLocalsInit]
public sealed class Alma : AbstractBase, IDisposable
{
    private readonly int _period;
    private readonly double _offset;
    private readonly double _sigma;
    private readonly double[] _weights;
    private readonly double _invWeightSum;
    private readonly RingBuffer _buffer;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _pubHandler;

    private record struct State(double LastValidValue);
    private State _state;
    private State _p_state;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates ALMA with specified parameters.
    /// </summary>
    /// <param name="period">Window size (must be > 0)</param>
    /// <param name="offset">Gaussian offset (0-1, default 0.85). Closer to 1 makes it more responsive.</param>
    /// <param name="sigma">Standard deviation (default 6). Higher values make it sharper.</param>
    public Alma(int period, double offset = 0.85, double sigma = 6.0)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (sigma <= 0)
            throw new ArgumentException("Sigma must be greater than 0", nameof(sigma));
        if (offset < 0 || offset > 1)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be between 0 and 1");

        _period = period;
        _offset = offset;
        _sigma = sigma;
        _buffer = new RingBuffer(period);
        _weights = new double[period];
        Name = $"Alma({period}, {offset:F2}, {sigma:F2})";
        WarmupPeriod = period;

        // Precompute weights
        double m = offset * (period - 1);
        double s = period / sigma;
        double s2 = 2 * s * s;
        double sum = 0;

        for (int i = 0; i < period; i++)
        {
            double v = i - m;
            _weights[i] = Math.Exp(-(v * v) / s2);
            sum += _weights[i];
        }

        _invWeightSum = 1.0 / sum;
    }

    public Alma(ITValuePublisher source, int period, double offset = 0.85, double sigma = 6.0)
        : this(period, offset, sigma)
    {
        _source = source;
        _pubHandler = Handle;
        _source.Pub += _pubHandler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, TValueEventArgs e) => Update(e.Value, e.IsNew);

    public void Dispose()
    {
        if (_source != null && _pubHandler != null)
        {
            _source.Pub -= _pubHandler;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        return double.IsFinite(input) ? input : _state.LastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return Update(input, isNew, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue Update(TValue input, bool isNew, bool publish)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double val = GetValidValue(input.Value);
        if (double.IsFinite(input.Value))
        {
            _state.LastValidValue = input.Value;
        }

        _buffer.Add(val, isNew);

        double result = 0;
        if (_buffer.Count > 0)
        {
            result = CalculateWeightedSum();
        }

        Last = new TValue(input.Time, result);
        if (publish)
        {
            PubEvent(Last);
        }
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries([], []);

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Calculate(source.Values, vSpan, _period, _offset, _sigma);
        source.Times.CopyTo(tSpan);

        // Restore state
        _buffer.Clear();
        _state = default;

        // Replay last part to restore buffer state
        int startIndex = Math.Max(0, len - _period);
        for (int i = startIndex; i < len; i++)
        {
            Update(source[i], true, false);
        }

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateWeightedSum()
    {
        int count = _buffer.Count;
        if (count == 0) return 0;

        if (count < _period)
        {
            // Partial buffer: align newest with newest
            // Buffer[0] (oldest) -> Weights[period - count]
            ReadOnlySpan<double> bufferSpan = _buffer.GetSpan();
            int weightOffset = _period - count;

            // Use DotProduct for partial sum
            double sum = bufferSpan.DotProduct(_weights.AsSpan(weightOffset, count));

            // Calculate weightSum for this subset
            double wSum = 0;
            for (int i = 0; i < count; i++)
            {
                wSum += _weights[weightOffset + i];
            }

            return wSum > 0 ? sum / wSum : 0;
        }

        // Full buffer: use precomputed _weightSum and SIMD DotProduct
        // We use InternalBuffer and StartIndex to avoid allocation and handle wrapping
        ReadOnlySpan<double> internalBuf = _buffer.InternalBuffer;
        int head = _buffer.StartIndex;

        // Part 1: Oldest to End of Buffer -> InternalBuffer[Head ... Cap-1]
        //         Matches Weights[0 ... Cap-Head-1]
        int part1Len = _period - head;
        double sum1 = internalBuf.Slice(head, part1Len).DotProduct(_weights.AsSpan(0, part1Len));

        // Part 2: Start of Buffer to Newest -> InternalBuffer[0 ... Head-1]
        //         Matches Weights[Cap-Head ... Cap-1]
        double sum2 = internalBuf[..head].DotProduct(_weights.AsSpan(part1Len));

        return (sum1 + sum2) * _invWeightSum;
    }

    public static TSeries Batch(TSeries source, int period, double offset = 0.85, double sigma = 6.0)
    {
        var alma = new Alma(period, offset, sigma);
        return alma.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period, double offset = 0.85, double sigma = 6.0)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));

        // Precompute weights
        // Use stackalloc for small periods to avoid heap allocation, ArrayPool for large
        double[]? weightsArray = period > 256 ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> weights = period <= 256 
            ? stackalloc double[period] 
            : weightsArray!.AsSpan(0, period);

        double m = offset * (period - 1);
        double s = period / sigma;
        double s2 = 2 * s * s;
        double weightSum = 0;

        for (int i = 0; i < period; i++)
        {
            double v = i - m;
            weights[i] = Math.Exp(-(v * v) / s2);
            weightSum += weights[i];
        }
        double invWeightSum = 1.0 / weightSum;

        // Buffer for sliding window
        double[]? bufferArray = period > 256 ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> buffer = period <= 256 
            ? stackalloc double[period] 
            : bufferArray!.AsSpan(0, period);

        int bufferIdx = 0;
        int count = 0;
        double lastValid = 0;
        double currentWeightSum = 0;

        try
        {
            for (int i = 0; i < source.Length; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                    lastValid = val;
                else
                    val = lastValid;

                // Add to circular buffer
                buffer[bufferIdx] = val;
                bufferIdx = (bufferIdx + 1) % period;
                
                if (count < period)
                {
                    count++;
                    // Incremental weight sum update for warmup
                    // We added weights[period - count] to the active set
                    currentWeightSum += weights[period - count];
                }

                double sum = 0;

                if (count == period)
                {
                    // Buffer is full. bufferIdx points to the oldest element (next write position)
                    // We split the dot product into two parts to handle the circular buffer wrap-around

                    // Part 1: From bufferIdx to End of buffer
                    // Matches the beginning of the weights
                    int part1Len = period - bufferIdx;
                    sum += buffer.Slice(bufferIdx, part1Len).DotProduct(weights.Slice(0, part1Len));

                    // Part 2: From Start of buffer to bufferIdx
                    // Matches the rest of the weights
                    sum += buffer.Slice(0, bufferIdx).DotProduct(weights.Slice(part1Len));

                    output[i] = sum * invWeightSum;
                }
                else
                {
                    // Partial buffer
                    int startIdx = (bufferIdx - count + period) % period;
                    int weightOffset = period - count;

                    if (startIdx + count <= period)
                    {
                        // Contiguous in buffer
                        sum = buffer.Slice(startIdx, count).DotProduct(weights.Slice(weightOffset, count));
                    }
                    else
                    {
                        // Wrapped in buffer
                        int part1Len = period - startIdx;
                        int part2Len = count - part1Len;

                        sum = buffer.Slice(startIdx, part1Len).DotProduct(weights.Slice(weightOffset, part1Len));
                        sum += buffer.Slice(0, part2Len).DotProduct(weights.Slice(weightOffset + part1Len, part2Len));
                    }

                    output[i] = currentWeightSum > 0 ? sum / currentWeightSum : 0;
                }
            }
        }
        finally
        {
            if (weightsArray != null) ArrayPool<double>.Shared.Return(weightsArray);
            if (bufferArray != null) ArrayPool<double>.Shared.Return(bufferArray);
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }
}
