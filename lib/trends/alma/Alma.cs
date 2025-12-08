using System;
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
public sealed class Alma : ITValuePublisher
{
    private readonly int _period;
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly RingBuffer _buffer;
    private double _lastValidValue;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event Action<TValue>? Pub;

    /// <summary>
    /// Current ALMA value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the ALMA has enough data to produce valid results (buffer is full).
    /// </summary>
    public bool IsHot => _buffer.IsFull;

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

        _period = period;
        _buffer = new RingBuffer(period);
        _weights = new double[period];
        Name = $"Alma({period}, {offset:F2}, {sigma:F2})";

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

        _weightSum = sum;
    }

    public Alma(ITValuePublisher source, int period, double offset = 0.85, double sigma = 6.0) 
        : this(period, offset, sigma)
    {
        source.Pub += (item) => Update(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _lastValidValue = input;
            return input;
        }
        return _lastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        double val = GetValidValue(input.Value);
        _buffer.Add(val, isNew);

        double result = 0;
        if (_buffer.Count > 0)
        {
            result = CalculateWeightedSum();
        }

        Last = new TValue(input.Time, result);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries(new List<long>(), new List<double>());

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Calculate(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Restore state
        _buffer.Clear();
        _lastValidValue = 0;
        
        // Replay last part to restore buffer state
        int startIndex = Math.Max(0, len - _period);
        for (int i = startIndex; i < len; i++)
        {
            Update(source[i]);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateWeightedSum()
    {
        // If buffer is not full, we only use the most recent 'count' weights?
        // Standard ALMA usually waits for full period, or re-normalizes weights.
        // Here we'll re-normalize based on how many items we have.
        // But to match standard behavior, we usually just run on what we have.
        // However, the weights are designed for a specific period.
        // Using a partial window with full-period weights might be weird.
        // Let's stick to the standard: use the weights corresponding to the filled positions.
        // Since RingBuffer adds new items at 'head', and we want to apply weights 
        // such that weights[period-1] applies to the newest item, etc.
        
        // RingBuffer: [Oldest ... Newest]
        // Weights:    [0 ... period-1]
        // We want:    Sum(Buffer[i] * Weights[i]) / Sum(Weights)
        
        // BUT: If buffer is not full, say count=5, period=10.
        // We have 5 items. Should we use weights[0..4] or weights[5..9]?
        // Usually, moving averages grow.
        // Let's assume we use the last 'count' weights, normalized.
        
        ReadOnlySpan<double> bufferSpan = _buffer.GetSpan();
        int count = bufferSpan.Length;
        
        // If not full, we need to handle it carefully.
        // For simplicity and performance, let's just iterate.
        // Optimization: If full, use SIMD.
        
        if (count < _period)
        {
            double sum = 0;
            double wSum = 0;
            // Map weights to buffer: 
            // Buffer[0] (oldest) -> Weights[period - count] ??
            // Actually, standard is: Weights are fixed.
            // Let's align newest with newest.
            // Buffer[count-1] (newest) <-> Weights[period-1]
            // Buffer[0] (oldest)       <-> Weights[period-count]
            
            int weightOffset = _period - count;
            for (int i = 0; i < count; i++)
            {
                double w = _weights[weightOffset + i];
                sum += bufferSpan[i] * w;
                wSum += w;
            }
            return wSum > 0 ? sum / wSum : 0;
        }

        // Full buffer
        return CalculateWeightedSumSimd(bufferSpan);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateWeightedSumSimd(ReadOnlySpan<double> buffer)
    {
        double sum = 0;
        int i = 0;
        int len = _period;

        if (Avx2.IsSupported && len >= Vector256<double>.Count)
        {
            var vSum = Vector256<double>.Zero;
            ref double bufRef = ref MemoryMarshal.GetReference(buffer);
            ref double wRef = ref MemoryMarshal.GetReference(_weights.AsSpan());

            for (; i <= len - Vector256<double>.Count; i += Vector256<double>.Count)
            {
                var vBuf = Vector256.LoadUnsafe(ref Unsafe.Add(ref bufRef, i));
                var vW = Vector256.LoadUnsafe(ref Unsafe.Add(ref wRef, i));
                vSum = Avx.Add(vSum, Avx.Multiply(vBuf, vW));
            }

            // Horizontal sum
            vSum = Avx.Add(vSum, Avx2.Permute4x64(vSum.AsUInt64(), 0b_01_00_11_10).AsDouble());
            vSum = Avx.Add(vSum, Avx2.Permute4x64(vSum.AsUInt64(), 0b_00_00_00_01).AsDouble());
            sum = vSum.GetElement(0);
        }

        // Scalar fallback
        for (; i < len; i++)
        {
            sum += buffer[i] * _weights[i];
        }

        return sum / _weightSum;
    }

    public static TSeries Calculate(TSeries source, int period, double offset = 0.85, double sigma = 6.0)
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
            throw new ArgumentException("Source and output must have the same length");

        // Precompute weights
        double[] weights = new double[period];
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

        // Buffer for sliding window
        // Use stackalloc for small periods
        Span<double> buffer = period <= 256 ? stackalloc double[period] : new double[period];
        int bufferIdx = 0;
        int count = 0;
        double lastValid = 0;

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
            if (count < period) count++;

            // Calculate weighted sum
            // We need to iterate buffer from oldest to newest to match weights[0..period-1]
            // Oldest is at: (bufferIdx - count + period) % period
            // But wait, the buffer wraps.
            // Let's just iterate 0..count-1 and map to buffer index.
            
            double sum = 0;
            double currentWeightSum = 0;
            
            int startIdx = (bufferIdx - count + period) % period;
            int weightOffset = period - count; // Align weights to end
            
            // Optimization: If full, we can use SIMD if we unwrap the buffer or handle wrapping.
            // For simplicity in static method (and since we can't easily unwrap stackalloc),
            // we'll use scalar loop with modulo.
            // Or better: copy to a temporary linear buffer? No, that's too much copying.
            
            // Actually, for full period, we can do two loops (part1, part2) to avoid modulo in loop.
            
            if (count == period)
            {
                // Buffer is full. startIdx is bufferIdx (which is the oldest, since we just wrote to bufferIdx-1)
                // Wait, bufferIdx points to the NEXT write position.
                // So bufferIdx is the Oldest.
                
                // Part 1: bufferIdx to End
                int part1Len = period - bufferIdx;
                for (int j = 0; j < part1Len; j++)
                {
                    sum += buffer[bufferIdx + j] * weights[j];
                }
                
                // Part 2: 0 to bufferIdx
                for (int j = 0; j < bufferIdx; j++)
                {
                    sum += buffer[j] * weights[part1Len + j];
                }
                
                output[i] = sum / weightSum;
            }
            else
            {
                // Partial buffer
                for (int j = 0; j < count; j++)
                {
                    int idx = (startIdx + j) % period;
                    double w = weights[weightOffset + j];
                    sum += buffer[idx] * w;
                    currentWeightSum += w;
                }
                output[i] = currentWeightSum > 0 ? sum / currentWeightSum : 0;
            }
        }
    }

    public void Reset()
    {
        _buffer.Clear();
        _lastValidValue = 0;
        Last = default;
    }
}
