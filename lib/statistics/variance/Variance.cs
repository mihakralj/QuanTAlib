using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// Variance: Measures the dispersion of a set of data points around their mean.
/// </summary>
/// <remarks>
/// Variance is calculated as the average of the squared differences from the Mean.
///
/// Formula:
/// Population Variance = Sum((x - Mean)^2) / N
/// Sample Variance = Sum((x - Mean)^2) / (N - 1)
///
/// This implementation uses the O(1) running sum of squares formula:
/// Variance = (SumSq - (Sum * Sum) / N) / (N - 1) (for Sample)
/// </remarks>
[SkipLocalsInit]
public sealed class Variance : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly bool _isPopulation;
    private double _sumSq;
    private double _p_sumSq;
    private int _updateCount;
    private const int ResyncInterval = 1000;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates a new Variance indicator.
    /// </summary>
    /// <param name="period">The lookback period.</param>
    /// <param name="isPopulation">If true, calculates Population Variance (div by N). If false, Sample Variance (div by N-1). Default is false (Sample).</param>
    public Variance(int period, bool isPopulation = false)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        _period = period;
        _isPopulation = isPopulation;
        _buffer = new RingBuffer(period);
        Name = $"Variance({period})";
        WarmupPeriod = period;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            // Snapshot state BEFORE mutations
            _p_sumSq = _sumSq;
            _buffer.Snapshot();

            if (_buffer.IsFull)
            {
                double oldVal = _buffer.Oldest;
                _sumSq = Math.FusedMultiplyAdd(-oldVal, oldVal, _sumSq);
            }

            _buffer.Add(input.Value);
            _sumSq = Math.FusedMultiplyAdd(input.Value, input.Value, _sumSq);

            _updateCount++;
            if (_updateCount % ResyncInterval == 0)
            {
                Resync();
            }
        }
        else
        {
            // Restore state from snapshot
            _sumSq = _p_sumSq;
            _buffer.Restore();

            // Now apply the correction value
            if (_buffer.IsFull)
            {
                double oldVal = _buffer.Oldest;
                _sumSq = Math.FusedMultiplyAdd(-oldVal, oldVal, _sumSq);
            }

            _buffer.Add(input.Value);
            _sumSq = Math.FusedMultiplyAdd(input.Value, input.Value, _sumSq);
        }

        double variance = 0;
        if (_buffer.Count > 1)
        {
            double n = _buffer.Count;
            // Var = (SumSq - 2*Mean*Sum + N*Mean^2) / (N or N-1)
            // Var = (SumSq - 2*Mean*(N*Mean) + N*Mean^2) / ...
            // Var = (SumSq - 2*N*Mean^2 + N*Mean^2) / ...
            // Var = (SumSq - N*Mean^2) / ...

            // Using Sum:
            // Var = (SumSq - (Sum*Sum)/N) / ...

            double numerator = _sumSq - (_buffer.Sum * _buffer.Sum) / n;

            // Handle floating point noise
            if (numerator < 0) numerator = 0;

            double denominator = _isPopulation ? n : (n - 1);
            variance = numerator / denominator;
        }

        Last = new TValue(input.Time, variance);
        PubEvent(Last);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period, _isPopulation);
        source.Times.CopyTo(tSpan);

        // Prime the state with the last 'period' values
        // This ensures that subsequent calls to Update(TValue) work correctly
        // We can't just copy the last value, we need to fill the buffer
        int primeStart = Math.Max(0, len - _period);
        for (int i = primeStart; i < len; i++)
        {
            Update(source[i]);
        }

        return new TSeries(t, v);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _sumSq = 0;
        _updateCount = 0;
        Last = default;
    }

    private void Resync()
    {
        var span = _buffer.GetSpan();
        _sumSq = span.DotProduct(span);
        _buffer.RecalculateSum();
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.UtcNow, value));
        }
    }

    public static TSeries Calculate(TSeries source, int period, bool isPopulation = false)
    {
        var variance = new Variance(period, isPopulation);
        return variance.Update(source);
    }

    /// <summary>
    /// Calculates Variance in-place, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// Uses SIMD acceleration for large, clean datasets.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output span (must be same length as source)</param>
    /// <param name="period">Variance period (must be >= 2)</param>
    /// <param name="isPopulation">If true, calculates Population Variance (div by N). If false, Sample Variance (div by N-1).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, bool isPopulation = false)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        if (period < 2)
            throw new ArgumentException("Period must be greater than or equal to 2", nameof(period));

        int len = source.Length;
        if (len == 0) return;

        // Try SIMD path for large, clean datasets
        const int SimdThreshold = 256;
        if (len >= SimdThreshold && !source.ContainsNonFinite())
        {
            if (Avx512F.IsSupported)
            {
                CalculateAvx512Core(source, output, period, isPopulation);
                return;
            }

            if (Avx2.IsSupported)
            {
                CalculateAvx2Core(source, output, period, isPopulation);
                return;
            }

            if (AdvSimd.Arm64.IsSupported)
            {
                CalculateNeonCore(source, output, period, isPopulation);
                return;
            }
        }

        // Scalar path with NaN handling
        CalculateScalarCore(source, output, period, isPopulation);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period, bool isPopulation)
    {
        int len = source.Length;
        double sum = 0;
        double sumSq = 0;

        // We need a buffer to handle the sliding window removal
        // For scalar path, we can use a simple array or stackalloc
        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        int bufferIndex = 0;
        int i = 0;

        // Warmup phase
        int warmupEnd = Math.Min(period, len);
        for (; i < warmupEnd; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val)) val = 0; // Fallback

            sum += val;
            sumSq = Math.FusedMultiplyAdd(val, val, sumSq);
            buffer[i] = val;

            double n = i + 1;
            if (n > 1)
            {
                double numerator = sumSq - (sum * sum) / n;
                if (numerator < 0) numerator = 0;
                double denominator = isPopulation ? n : (n - 1);
                output[i] = numerator / denominator;
            }
            else
            {
                output[i] = 0;
            }
        }

        // Sliding window phase
        int tickCount = period;
        for (; i < len; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val)) val = 0; // Fallback

            double oldVal = buffer[bufferIndex];

            sum = sum - oldVal + val;
            sumSq = Math.FusedMultiplyAdd(-oldVal, oldVal, sumSq);
            sumSq = Math.FusedMultiplyAdd(val, val, sumSq);

            buffer[bufferIndex] = val;
            bufferIndex++;
            if (bufferIndex >= period) bufferIndex = 0;

            double n = period;
            double numerator = sumSq - (sum * sum) / n;
            if (numerator < 0) numerator = 0;
            double denominator = isPopulation ? n : (n - 1);
            output[i] = numerator / denominator;

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                sum = buffer.SumSIMD();
                sumSq = buffer.DotProduct(buffer);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WarmupVariance(int period, bool isPopulation, ref double srcRef, ref double outRef, out double sum, out double sumSq)
    {
        sum = 0;
        sumSq = 0;
        for (int i = 0; i < period; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            sum += val;
            sumSq = Math.FusedMultiplyAdd(val, val, sumSq);

            double n = i + 1;
            if (n > 1)
            {
                double num = sumSq - (sum * sum) / n;
                if (num < 0) num = 0;
                double den = isPopulation ? n : (n - 1);
                Unsafe.Add(ref outRef, i) = num / den;
            }
            else
            {
                Unsafe.Add(ref outRef, i) = 0;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateAvx512Core(ReadOnlySpan<double> source, Span<double> output, int period, bool isPopulation)
    {
        int len = source.Length;
        const int VectorWidth = 8;

        ref double srcRef = ref MemoryMarshal.GetReference(source);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        double invN = 1.0 / period;
        double invDenom = 1.0 / (isPopulation ? period : (period - 1));

        WarmupVariance(period, isPopulation, ref srcRef, ref outRef, out double sum, out double sumSq);

        if (len <= period) return;

        var vInvN = Vector512.Create(invN);
        var vInvDenom = Vector512.Create(invDenom);
        var vZero = Vector512<double>.Zero;

        int simdEnd = period + ((len - period) / VectorWidth) * VectorWidth;
        int tickCount = period;

        for (int i = period; i < simdEnd; i += VectorWidth)
        {
            var vNew = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
            var vOld = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - period));

            // Delta for Sum
            var vDelta = Avx512F.Subtract(vNew, vOld);

            // Delta for SumSq
            var vNewSq = Avx512F.Multiply(vNew, vNew);
            var vOldSq = Avx512F.Multiply(vOld, vOld);
            var vDeltaSq = Avx512F.Subtract(vNewSq, vOldSq);

            // Prefix sum for Sum
            var vShift1 = Vector512.Create(0.0, vDelta.GetElement(0), vDelta.GetElement(1), vDelta.GetElement(2), vDelta.GetElement(3), vDelta.GetElement(4), vDelta.GetElement(5), vDelta.GetElement(6));
            var vP1 = Avx512F.Add(vDelta, vShift1);

            var vShift2 = Vector512.Create(0.0, 0.0, vP1.GetElement(0), vP1.GetElement(1), vP1.GetElement(2), vP1.GetElement(3), vP1.GetElement(4), vP1.GetElement(5));
            var vP2 = Avx512F.Add(vP1, vShift2);

            var vShift4 = Vector512.Create(0.0, 0.0, 0.0, 0.0, vP2.GetElement(0), vP2.GetElement(1), vP2.GetElement(2), vP2.GetElement(3));
            var vP4 = Avx512F.Add(vP2, vShift4);

            var vSumPrev = Vector512.Create(sum);
            var vSums = Avx512F.Add(vSumPrev, vP4);

            // Prefix sum for SumSq
            var vShiftSq1 = Vector512.Create(0.0, vDeltaSq.GetElement(0), vDeltaSq.GetElement(1), vDeltaSq.GetElement(2), vDeltaSq.GetElement(3), vDeltaSq.GetElement(4), vDeltaSq.GetElement(5), vDeltaSq.GetElement(6));
            var vP1Sq = Avx512F.Add(vDeltaSq, vShiftSq1);

            var vShiftSq2 = Vector512.Create(0.0, 0.0, vP1Sq.GetElement(0), vP1Sq.GetElement(1), vP1Sq.GetElement(2), vP1Sq.GetElement(3), vP1Sq.GetElement(4), vP1Sq.GetElement(5));
            var vP2Sq = Avx512F.Add(vP1Sq, vShiftSq2);

            var vShiftSq4 = Vector512.Create(0.0, 0.0, 0.0, 0.0, vP2Sq.GetElement(0), vP2Sq.GetElement(1), vP2Sq.GetElement(2), vP2Sq.GetElement(3));
            var vP4Sq = Avx512F.Add(vP2Sq, vShiftSq4);

            var vSumSqPrev = Vector512.Create(sumSq);
            var vSumSqs = Avx512F.Add(vSumSqPrev, vP4Sq);

            // Calculate Variance
            var vSumSquared = Avx512F.Multiply(vSums, vSums);
            var vMeanTerm = Avx512F.Multiply(vSumSquared, vInvN);
            var vNumerator = Avx512F.Subtract(vSumSqs, vMeanTerm);

            vNumerator = Avx512F.Max(vZero, vNumerator);

            var vResult = Avx512F.Multiply(vNumerator, vInvDenom);
            vResult.StoreUnsafe(ref Unsafe.Add(ref outRef, i));

            sum = vSums.GetElement(7);
            sumSq = vSumSqs.GetElement(7);

            tickCount += VectorWidth;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                int lastIdx = i + VectorWidth - 1;
                double recalcSum = 0;
                double recalcSumSq = 0;
                int startIdx = lastIdx - period + 1;
                for (int k = 0; k < period; k++)
                {
                    double v = Unsafe.Add(ref srcRef, startIdx + k);
                    recalcSum += v;
                    recalcSumSq += v * v;
                }
                sum = recalcSum;
                sumSq = recalcSumSq;
            }
        }

        for (int i = simdEnd; i < len; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            double oldVal = Unsafe.Add(ref srcRef, i - period);

            sum = sum - oldVal + val;
            sumSq = Math.FusedMultiplyAdd(-oldVal, oldVal, sumSq);
            sumSq = Math.FusedMultiplyAdd(val, val, sumSq);

            double numerator = sumSq - sum * sum * invN;
            if (numerator < 0) numerator = 0;
            Unsafe.Add(ref outRef, i) = numerator * invDenom;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateNeonCore(ReadOnlySpan<double> source, Span<double> output, int period, bool isPopulation)
    {
        int len = source.Length;
        const int VectorWidth = 2;

        ref double srcRef = ref MemoryMarshal.GetReference(source);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        double invN = 1.0 / period;
        double invDenom = 1.0 / (isPopulation ? period : (period - 1));

        WarmupVariance(period, isPopulation, ref srcRef, ref outRef, out double sum, out double sumSq);

        if (len <= period) return;

        var vInvN = Vector128.Create(invN);
        var vInvDenom = Vector128.Create(invDenom);
        var vZero = Vector128<double>.Zero;

        int simdEnd = period + ((len - period) / VectorWidth) * VectorWidth;
        int tickCount = period;

        for (int i = period; i < simdEnd; i += VectorWidth)
        {
            var vNew = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
            var vOld = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - period));

            // Delta for Sum
            var vDelta = AdvSimd.Arm64.Subtract(vNew, vOld);

            // Delta for SumSq
            var vNewSq = AdvSimd.Arm64.Multiply(vNew, vNew);
            var vOldSq = AdvSimd.Arm64.Multiply(vOld, vOld);
            var vDeltaSq = AdvSimd.Arm64.Subtract(vNewSq, vOldSq);

            // Prefix sum for Sum: [d0, d0+d1]
            double d0 = vDelta.GetElement(0);
            double d1 = vDelta.GetElement(1);
            double ps0 = sum + d0;
            double ps1 = ps0 + d1;
            var vSums = Vector128.Create(ps0, ps1);

            // Prefix sum for SumSq
            double dSq0 = vDeltaSq.GetElement(0);
            double dSq1 = vDeltaSq.GetElement(1);
            double psSq0 = sumSq + dSq0;
            double psSq1 = psSq0 + dSq1;
            var vSumSqs = Vector128.Create(psSq0, psSq1);

            // Calculate Variance
            var vSumSquared = AdvSimd.Arm64.Multiply(vSums, vSums);
            var vMeanTerm = AdvSimd.Arm64.Multiply(vSumSquared, vInvN);
            var vNumerator = AdvSimd.Arm64.Subtract(vSumSqs, vMeanTerm);

            vNumerator = AdvSimd.Arm64.Max(vZero, vNumerator);

            var vResult = AdvSimd.Arm64.Multiply(vNumerator, vInvDenom);
            vResult.StoreUnsafe(ref Unsafe.Add(ref outRef, i));

            sum = ps1;
            sumSq = psSq1;

            tickCount += VectorWidth;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                int lastIdx = i + VectorWidth - 1;
                double recalcSum = 0;
                double recalcSumSq = 0;
                int startIdx = lastIdx - period + 1;
                for (int k = 0; k < period; k++)
                {
                    double v = Unsafe.Add(ref srcRef, startIdx + k);
                    recalcSum += v;
                    recalcSumSq += v * v;
                }
                sum = recalcSum;
                sumSq = recalcSumSq;
            }
        }

        for (int i = simdEnd; i < len; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            double oldVal = Unsafe.Add(ref srcRef, i - period);

            sum = sum - oldVal + val;
            sumSq = Math.FusedMultiplyAdd(-oldVal, oldVal, sumSq);
            sumSq = Math.FusedMultiplyAdd(val, val, sumSq);

            double numerator = sumSq - sum * sum * invN;
            if (numerator < 0) numerator = 0;
            Unsafe.Add(ref outRef, i) = numerator * invDenom;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateAvx2Core(ReadOnlySpan<double> source, Span<double> output, int period, bool isPopulation)
    {
        int len = source.Length;
        const int VectorWidth = 4;

        ref double srcRef = ref MemoryMarshal.GetReference(source);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        double invN = 1.0 / period;
        double invDenom = 1.0 / (isPopulation ? period : (period - 1));

        WarmupVariance(period, isPopulation, ref srcRef, ref outRef, out double sum, out double sumSq);

        if (len <= period) return;

        var vInvN = Vector256.Create(invN);
        var vInvDenom = Vector256.Create(invDenom);
        var vZero = Vector256<double>.Zero;

        int simdEnd = period + ((len - period) / VectorWidth) * VectorWidth;
        int tickCount = period;

        for (int i = period; i < simdEnd; i += VectorWidth)
        {
            var vNew = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
            var vOld = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - period));

            // Delta for Sum
            var vDelta = Avx.Subtract(vNew, vOld);

            // Delta for SumSq
            var vNewSq = Avx.Multiply(vNew, vNew);
            var vOldSq = Avx.Multiply(vOld, vOld);
            var vDeltaSq = Avx.Subtract(vNewSq, vOldSq);

            // Prefix sum for Sum (same as Sma.cs)
            // Prefix sum on deltas to compute 4 variance values simultaneously:
            // Each lane accumulates deltas from all previous lanes within the vector.
            // Lane 0: Δ₀ (window ending at i)
            // Lane 1: Δ₀+Δ₁ (window ending at i+1)
            // Lane 2: Δ₀+Δ₁+Δ₂ (window ending at i+2)
            // Lane 3: Δ₀+Δ₁+Δ₂+Δ₃ (window ending at i+3)
            var vShift1 = Avx2.Permute4x64(vDelta.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
            vShift1 = Avx.Blend(vZero, vShift1, 0b_1110);
            var vP1 = Avx.Add(vDelta, vShift1);

            var vShift2 = Avx2.Permute4x64(vP1.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
            vShift2 = Avx.Blend(vZero, vShift2, 0b_1100);
            var vP2 = Avx.Add(vP1, vShift2);

            var vSumPrev = Vector256.Create(sum);
            var vSums = Avx.Add(vSumPrev, vP2);

            // Prefix sum for SumSq
            var vShiftSq1 = Avx2.Permute4x64(vDeltaSq.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftSq1 = Avx.Blend(vZero, vShiftSq1, 0b_1110);
            var vP1Sq = Avx.Add(vDeltaSq, vShiftSq1);

            var vShiftSq2 = Avx2.Permute4x64(vP1Sq.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftSq2 = Avx.Blend(vZero, vShiftSq2, 0b_1100);
            var vP2Sq = Avx.Add(vP1Sq, vShiftSq2);

            var vSumSqPrev = Vector256.Create(sumSq);
            var vSumSqs = Avx.Add(vSumSqPrev, vP2Sq);

            // Calculate Variance
            // Var = (SumSq - (Sum*Sum)/N) / Denom
            var vSumSquared = Avx.Multiply(vSums, vSums);
            var vMeanTerm = Avx.Multiply(vSumSquared, vInvN);
            var vNumerator = Avx.Subtract(vSumSqs, vMeanTerm);

            // Max(0, numerator) to handle floating point noise
            vNumerator = Avx.Max(vZero, vNumerator);

            var vResult = Avx.Multiply(vNumerator, vInvDenom);
            vResult.StoreUnsafe(ref Unsafe.Add(ref outRef, i));

            // Update scalar accumulators for next iteration
            sum = vSums.GetElement(3);
            sumSq = vSumSqs.GetElement(3);

            tickCount += VectorWidth;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                int lastIdx = i + VectorWidth - 1;
                double recalcSum = 0;
                double recalcSumSq = 0;
                int startIdx = lastIdx - period + 1;
                for (int k = 0; k < period; k++)
                {
                    double v = Unsafe.Add(ref srcRef, startIdx + k);
                    recalcSum += v;
                    recalcSumSq += v * v;
                }
                sum = recalcSum;
                sumSq = recalcSumSq;
            }
        }

        // Handle remaining elements
        for (int i = simdEnd; i < len; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            double oldVal = Unsafe.Add(ref srcRef, i - period);

            sum = sum - oldVal + val;
            sumSq = Math.FusedMultiplyAdd(-oldVal, oldVal, sumSq);
            sumSq = Math.FusedMultiplyAdd(val, val, sumSq);

            double numerator = sumSq - sum * sum * invN;
            if (numerator < 0) numerator = 0;
            Unsafe.Add(ref outRef, i) = numerator * invDenom;
        }
    }
}