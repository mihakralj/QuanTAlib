using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// SIMD-accelerated extension methods for high-performance array operations.
/// Uses Vector<T> for 4-8x speedup on supported hardware with automatic scalar fallback.
/// </summary>
public static class SimdExtensions
{
    // Internal scalar implementations for testability
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool ContainsNonFiniteScalar(ReadOnlySpan<double> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (!double.IsFinite(span[i]))
                return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double SumScalar(ReadOnlySpan<double> span)
    {
        double scalar = 0.0;
        for (int i = 0; i < span.Length; i++)
            scalar += span[i];
        return scalar;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double MinScalar(ReadOnlySpan<double> span)
    {
        if (span.Length == 0)
            throw new ArgumentException("Span must not be empty", nameof(span));

        double min = span[0];
        for (int i = 1; i < span.Length; i++)
        {
            if (span[i] < min)
                min = span[i];
        }
        return min;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double MaxScalar(ReadOnlySpan<double> span)
    {
        if (span.Length == 0)
            throw new ArgumentException("Span must not be empty", nameof(span));

        double max = span[0];
        for (int i = 1; i < span.Length; i++)
        {
            if (span[i] > max)
                max = span[i];
        }
        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double VarianceScalar(ReadOnlySpan<double> span, double mean)
    {
        double sumSquares = 0.0;
        for (int i = 0; i < span.Length; i++)
        {
            double diff = span[i] - mean;
            sumSquares += diff * diff;
        }
        return sumSquares / (span.Length - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (double Min, double Max) MinMaxScalar(ReadOnlySpan<double> span)
    {
        double scalarMin = span[0];
        double scalarMax = span[0];
        for (int i = 1; i < span.Length; i++)
        {
            if (span[i] < scalarMin) scalarMin = span[i];
            if (span[i] > scalarMax) scalarMax = span[i];
        }
        return (scalarMin, scalarMax);
    }

    /// <summary>
    /// Checks if span contains any non-finite values (NaN or Infinity).
    /// Returns true if any non-finite value is found.
    /// Uses SIMD: NaN detected via v != v (NaN is the only value where this is true),
    /// Infinity detected via |v| > MaxValue comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsNonFinite(this ReadOnlySpan<double> span)
    {
        if (span.IsEmpty) return false;

        if (Vector.IsHardwareAccelerated && span.Length >= Vector<double>.Count)
        {
            int vectorSize = Vector<double>.Count;
            int i = 0;
            var maxValue = new Vector<double>(double.MaxValue);

            for (; i <= span.Length - vectorSize; i += vectorSize)
            {
                var vector = new Vector<double>(span.Slice(i, vectorSize));

                // NaN check: NaN != NaN, so Vector.Equals(v, v) will be false for NaN lanes
                var nanCheck = Vector.Equals(vector, vector);
                if (!nanCheck.Equals(Vector<long>.AllBitsSet))
                    return true;

                // Infinity check: |v| > MaxValue (Infinity has magnitude > MaxValue)
                var absVec = Vector.Abs(vector);
                var infCheck = Vector.GreaterThan(absVec, maxValue);
                if (!infCheck.Equals(Vector<long>.Zero))
                    return true;
            }

            for (; i < span.Length; i++)
            {
                if (!double.IsFinite(span[i]))
                    return true;
            }

            return false;
        }

        return ContainsNonFiniteScalar(span);
    }

    /// <summary>
    /// Calculates sum using SIMD vectorization when available.
    /// 4-8x faster than scalar loop on AVX2/AVX-512 hardware.
    /// Returns NaN if any input value is non-finite.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SumSIMD(this ReadOnlySpan<double> span)
    {
        if (span.IsEmpty) return 0.0;

        // Guard against non-finite inputs
        if (span.ContainsNonFinite()) return double.NaN;

        if (Vector.IsHardwareAccelerated && span.Length >= Vector<double>.Count)
        {
            Vector<double> sum = Vector<double>.Zero;
            int vectorSize = Vector<double>.Count;
            int i = 0;

            for (; i <= span.Length - vectorSize; i += vectorSize)
            {
                var vector = new Vector<double>(span.Slice(i, vectorSize));
                sum += vector;
            }

            double result = 0.0;
            for (int j = 0; j < vectorSize; j++)
                result += sum[j];

            for (; i < span.Length; i++)
                result += span[i];

            return result;
        }

        return SumScalar(span);
    }

    /// <summary>
    /// Calculates minimum value using SIMD vectorization when available.
    /// 4-6x faster than scalar loop on AVX2/AVX-512 hardware.
    /// Returns NaN if any input value is non-finite.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double MinSIMD(this ReadOnlySpan<double> span)
    {
        if (span.IsEmpty) return double.NaN;
        if (span.Length == 1) return span[0];

        // Guard against non-finite inputs
        if (span.ContainsNonFinite()) return double.NaN;

        if (Vector.IsHardwareAccelerated && span.Length >= Vector<double>.Count)
        {
            int vectorSize = Vector<double>.Count;
            var minVec = new Vector<double>(span[..vectorSize]);
            int i = vectorSize;

            for (; i <= span.Length - vectorSize; i += vectorSize)
            {
                var vector = new Vector<double>(span.Slice(i, vectorSize));
                minVec = Vector.Min(minVec, vector);
            }

            double result = minVec[0];
            for (int j = 1; j < vectorSize; j++)
            {
                if (minVec[j] < result)
                    result = minVec[j];
            }

            for (; i < span.Length; i++)
            {
                if (span[i] < result)
                    result = span[i];
            }

            return result;
        }

        return MinScalar(span);
    }

    /// <summary>
    /// Calculates maximum value using SIMD vectorization when available.
    /// 4-6x faster than scalar loop on AVX2/AVX-512 hardware.
    /// Returns NaN if any input value is non-finite.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double MaxSIMD(this ReadOnlySpan<double> span)
    {
        if (span.IsEmpty) return double.NaN;
        if (span.Length == 1) return span[0];

        // Guard against non-finite inputs
        if (span.ContainsNonFinite()) return double.NaN;

        if (Vector.IsHardwareAccelerated && span.Length >= Vector<double>.Count)
        {
            int vectorSize = Vector<double>.Count;
            var maxVec = new Vector<double>(span[..vectorSize]);
            int i = vectorSize;

            for (; i <= span.Length - vectorSize; i += vectorSize)
            {
                var vector = new Vector<double>(span.Slice(i, vectorSize));
                maxVec = Vector.Max(maxVec, vector);
            }

            double result = maxVec[0];
            for (int j = 1; j < vectorSize; j++)
            {
                if (maxVec[j] > result)
                    result = maxVec[j];
            }

            for (; i < span.Length; i++)
            {
                if (span[i] > result)
                    result = span[i];
            }

            return result;
        }

        return MaxScalar(span);
    }

    /// <summary>
    /// Calculates average using SIMD vectorization when available.
    /// 4-8x faster than scalar loop on AVX2/AVX-512 hardware.
    /// Returns NaN if any input value is non-finite.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double AverageSIMD(this ReadOnlySpan<double> span)
    {
        if (span.IsEmpty) return double.NaN;
        // SumSIMD already guards against non-finite, which will propagate NaN
        return span.SumSIMD() / span.Length;
    }

    /// <summary>
    /// Calculates variance using a two-pass SIMD variant that computes the mean first (via AverageSIMD) and then sums squared differences to produce variance.
    /// Note that this is not the single-pass Welford algorithm.
    /// Returns NaN if any input value is non-finite or if mean is non-finite.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double VarianceSIMD(this ReadOnlySpan<double> span, double? mean = null)
    {
        if (span.Length < 2) return double.NaN;

        double m;
        if (mean.HasValue)
        {
            if (span.ContainsNonFinite()) return double.NaN;
            m = mean.Value;
        }
        else
        {
            m = span.AverageSIMD();
        }

        if (!double.IsFinite(m)) return double.NaN;

        if (Vector.IsHardwareAccelerated && span.Length >= Vector<double>.Count)
        {
            var meanVec = new Vector<double>(m);
            Vector<double> sumSq = Vector<double>.Zero;
            int vectorSize = Vector<double>.Count;
            int i = 0;

            for (; i <= span.Length - vectorSize; i += vectorSize)
            {
                var vector = new Vector<double>(span.Slice(i, vectorSize));
                var diff = vector - meanVec;
                sumSq += diff * diff;
            }

            double result = 0.0;
            for (int j = 0; j < vectorSize; j++)
                result += sumSq[j];

            for (; i < span.Length; i++)
            {
                double diff = span[i] - m;
                result += diff * diff;
            }

            return result / (span.Length - 1);
        }

        return VarianceScalar(span, m);
    }

    /// <summary>
    /// Calculates standard deviation using SIMD vectorization.
    /// Returns NaN if any input value is non-finite or if mean is non-finite.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double StdDevSIMD(this ReadOnlySpan<double> span, double? mean = null)
    {
        // VarianceSIMD already guards against non-finite, which will propagate NaN through Sqrt
        return Math.Sqrt(span.VarianceSIMD(mean));
    }

    /// <summary>
    /// Finds both min and max in a single pass using SIMD vectorization.
    /// More efficient than calling MinSIMD and MaxSIMD separately.
    /// Returns (NaN, NaN) if any input value is non-finite.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (double Min, double Max) MinMaxSIMD(this ReadOnlySpan<double> span)
    {
        if (span.IsEmpty) return (double.NaN, double.NaN);
        if (span.Length == 1) return (span[0], span[0]);

        // Guard against non-finite inputs
        if (span.ContainsNonFinite()) return (double.NaN, double.NaN);

        if (Vector.IsHardwareAccelerated && span.Length >= Vector<double>.Count)
        {
            int vectorSize = Vector<double>.Count;
            var minVec = new Vector<double>(span[..vectorSize]);
            var maxVec = minVec;
            int i = vectorSize;

            for (; i <= span.Length - vectorSize; i += vectorSize)
            {
                var vector = new Vector<double>(span.Slice(i, vectorSize));
                minVec = Vector.Min(minVec, vector);
                maxVec = Vector.Max(maxVec, vector);
            }

            double min = minVec[0];
            double max = maxVec[0];
            for (int j = 1; j < vectorSize; j++)
            {
                if (minVec[j] < min) min = minVec[j];
                if (maxVec[j] > max) max = maxVec[j];
            }

            for (; i < span.Length; i++)
            {
                if (span[i] < min) min = span[i];
                if (span[i] > max) max = span[i];
            }

            return (min, max);
        }

        return MinMaxScalar(span);
    }

    /// <summary>
    /// Element-wise addition of two spans using SIMD.
    /// result[i] = left[i] + right[i]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> result)
    {
        if (left.Length != right.Length || left.Length != result.Length)
            throw new ArgumentException("All spans must have the same length");

        int i = 0;
        if (Vector.IsHardwareAccelerated && left.Length >= Vector<double>.Count)
        {
            int vectorSize = Vector<double>.Count;
            for (; i <= left.Length - vectorSize; i += vectorSize)
            {
                var vLeft = new Vector<double>(left.Slice(i, vectorSize));
                var vRight = new Vector<double>(right.Slice(i, vectorSize));
                (vLeft + vRight).CopyTo(result.Slice(i, vectorSize));
            }
        }

        for (; i < left.Length; i++)
        {
            result[i] = left[i] + right[i];
        }
    }

    /// <summary>
    /// Element-wise subtraction of two spans using SIMD.
    /// result[i] = left[i] - right[i]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Subtract(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> result)
    {
        if (left.Length != right.Length || left.Length != result.Length)
            throw new ArgumentException("All spans must have the same length");

        int i = 0;
        if (Vector.IsHardwareAccelerated && left.Length >= Vector<double>.Count)
        {
            int vectorSize = Vector<double>.Count;
            for (; i <= left.Length - vectorSize; i += vectorSize)
            {
                var vLeft = new Vector<double>(left.Slice(i, vectorSize));
                var vRight = new Vector<double>(right.Slice(i, vectorSize));
                (vLeft - vRight).CopyTo(result.Slice(i, vectorSize));
            }
        }

        for (; i < left.Length; i++)
        {
            result[i] = left[i] - right[i];
        }
    }

    /// <summary>
    /// Calculates the dot product of two spans using SIMD intrinsics.
    /// Supports AVX512, AVX2, and NEON (ARM64).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DotProduct(this ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Spans must have equal length");

        if (a.IsEmpty) return 0.0;

        int len = a.Length;

        // Fast path for very small kernels (avoid SIMD overhead)
        if (len <= 3)
        {
            ref double aRef = ref MemoryMarshal.GetReference(a);
            ref double bRef = ref MemoryMarshal.GetReference(b);

            double sum = aRef * bRef;
            if (len > 1) sum += Unsafe.Add(ref aRef, 1) * Unsafe.Add(ref bRef, 1);
            if (len > 2) sum += Unsafe.Add(ref aRef, 2) * Unsafe.Add(ref bRef, 2);
            return sum;
        }

        if (Avx512F.IsSupported)
            return DotProductAvx512(a, b);

        if (Avx2.IsSupported)
            return DotProductAvx2(a, b);

        if (AdvSimd.Arm64.IsSupported)
            return DotProductNeon(a, b);

        double s1 = 0, s2 = 0, s3 = 0, s4 = 0;
        ref double ar = ref MemoryMarshal.GetReference(a);
        ref double br = ref MemoryMarshal.GetReference(b);

        int i = 0;
        // Unroll scalar loop with 4 accumulators to break dependency chains
        for (; i <= len - 4; i += 4)
        {
            s1 += Unsafe.Add(ref ar, i) * Unsafe.Add(ref br, i);
            s2 += Unsafe.Add(ref ar, i + 1) * Unsafe.Add(ref br, i + 1);
            s3 += Unsafe.Add(ref ar, i + 2) * Unsafe.Add(ref br, i + 2);
            s4 += Unsafe.Add(ref ar, i + 3) * Unsafe.Add(ref br, i + 3);
        }

        double s = s1 + s2 + s3 + s4;

        for (; i < len; i++)
        {
            s += Unsafe.Add(ref ar, i) * Unsafe.Add(ref br, i);
        }
        return s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProductAvx512(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        int len = a.Length;
        int i = 0;
        Vector512<double> vSum = Vector512<double>.Zero;
        Vector512<double> vSum2 = Vector512<double>.Zero;
        Vector512<double> vSum3 = Vector512<double>.Zero;
        Vector512<double> vSum4 = Vector512<double>.Zero;

        ref double aRef = ref MemoryMarshal.GetReference(a);
        ref double bRef = ref MemoryMarshal.GetReference(b);

        // Unroll loop: Process 32 doubles (4 vectors) at a time
        if (len >= 32)
        {
            for (; i <= len - 32; i += 32)
            {
                var va1 = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
                var vb1 = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, i));

                var va2 = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 8));
                var vb2 = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 8));

                var va3 = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 16));
                var vb3 = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 16));

                var va4 = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 24));
                var vb4 = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 24));

                vSum = Avx512F.FusedMultiplyAdd(va1, vb1, vSum);
                vSum2 = Avx512F.FusedMultiplyAdd(va2, vb2, vSum2);
                vSum3 = Avx512F.FusedMultiplyAdd(va3, vb3, vSum3);
                vSum4 = Avx512F.FusedMultiplyAdd(va4, vb4, vSum4);
            }
        }

        // Process remaining vectors (8 doubles at a time)
        for (; i <= len - 8; i += 8)
        {
            var va = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
            var vb = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, i));
            vSum = Avx512F.FusedMultiplyAdd(va, vb, vSum);
        }

        // Combine accumulators
        vSum = Avx512F.Add(vSum, vSum2);
        vSum3 = Avx512F.Add(vSum3, vSum4);
        vSum = Avx512F.Add(vSum, vSum3);

        Vector256<double> v256 = Avx.Add(vSum.GetLower(), vSum.GetUpper());
        Vector128<double> lower = v256.GetLower();
        Vector128<double> upper = v256.GetUpper();
        Vector128<double> combined = Sse2.Add(lower, upper);
        double sum = combined.GetElement(0) + combined.GetElement(1);

        // Scalar remainder
        for (; i < len; i++)
        {
            sum += Unsafe.Add(ref aRef, i) * Unsafe.Add(ref bRef, i);
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProductAvx2(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        int len = a.Length;
        int i = 0;
        Vector256<double> vSum = Vector256<double>.Zero;
        Vector256<double> vSum2 = Vector256<double>.Zero;
        Vector256<double> vSum3 = Vector256<double>.Zero;
        Vector256<double> vSum4 = Vector256<double>.Zero;

        ref double aRef = ref MemoryMarshal.GetReference(a);
        ref double bRef = ref MemoryMarshal.GetReference(b);

        // Unroll loop: Process 16 doubles (4 vectors) at a time
        if (len >= 16)
        {
            for (; i <= len - 16; i += 16)
            {
                var va1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
                var vb1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, i));

                var va2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 4));
                var vb2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 4));

                var va3 = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 8));
                var vb3 = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 8));

                var va4 = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 12));
                var vb4 = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 12));

                if (Fma.IsSupported)
                {
                    vSum = Fma.MultiplyAdd(va1, vb1, vSum);
                    vSum2 = Fma.MultiplyAdd(va2, vb2, vSum2);
                    vSum3 = Fma.MultiplyAdd(va3, vb3, vSum3);
                    vSum4 = Fma.MultiplyAdd(va4, vb4, vSum4);
                }
                else
                {
                    vSum = Avx.Add(vSum, Avx.Multiply(va1, vb1));
                    vSum2 = Avx.Add(vSum2, Avx.Multiply(va2, vb2));
                    vSum3 = Avx.Add(vSum3, Avx.Multiply(va3, vb3));
                    vSum4 = Avx.Add(vSum4, Avx.Multiply(va4, vb4));
                }
            }
        }

        // Process remaining vectors (4 doubles at a time)
        for (; i <= len - 4; i += 4)
        {
            var va = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
            var vb = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, i));

            vSum = Fma.IsSupported
                ? Fma.MultiplyAdd(va, vb, vSum)
                : Avx.Add(vSum, Avx.Multiply(va, vb));
        }

        // Combine accumulators
        vSum = Avx.Add(vSum, vSum2);
        vSum3 = Avx.Add(vSum3, vSum4);
        vSum = Avx.Add(vSum, vSum3);

        // Horizontal sum
        Vector128<double> lower = vSum.GetLower();
        Vector128<double> upper = vSum.GetUpper();
        Vector128<double> combined = Sse2.Add(lower, upper);
        double sum = combined.GetElement(0) + combined.GetElement(1);

        // Process remaining elements (scalar)
        for (; i < len; i++)
        {
            sum += Unsafe.Add(ref aRef, i) * Unsafe.Add(ref bRef, i);
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProductNeon(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        int len = a.Length;
        int i = 0;
        Vector128<double> vSum = Vector128<double>.Zero;
        Vector128<double> vSum2 = Vector128<double>.Zero;
        Vector128<double> vSum3 = Vector128<double>.Zero;
        Vector128<double> vSum4 = Vector128<double>.Zero;

        ref double aRef = ref MemoryMarshal.GetReference(a);
        ref double bRef = ref MemoryMarshal.GetReference(b);

        // Unroll loop: Process 8 doubles (4 vectors) at a time
        if (len >= 8)
        {
            for (; i <= len - 8; i += 8)
            {
                var va1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
                var vb1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, i));

                var va2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 2));
                var vb2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 2));

                var va3 = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 4));
                var vb3 = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 4));

                var va4 = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 6));
                var vb4 = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 6));

                // NEON has FMA on ARM64
                // Since we are inside DotProductNeon which is guarded by AdvSimd.Arm64.IsSupported,
                // we can assume Arm64 support.
                vSum = AdvSimd.Arm64.FusedMultiplyAdd(vSum, va1, vb1);
                vSum2 = AdvSimd.Arm64.FusedMultiplyAdd(vSum2, va2, vb2);
                vSum3 = AdvSimd.Arm64.FusedMultiplyAdd(vSum3, va3, vb3);
                vSum4 = AdvSimd.Arm64.FusedMultiplyAdd(vSum4, va4, vb4);
            }
        }

        // Process remaining vectors (2 doubles at a time)
        for (; i <= len - 2; i += 2)
        {
            var va = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
            var vb = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, i));

            vSum = AdvSimd.Arm64.FusedMultiplyAdd(vSum, va, vb);
        }

        // Combine accumulators
        vSum = AdvSimd.Arm64.Add(vSum, vSum2);
        vSum3 = AdvSimd.Arm64.Add(vSum3, vSum4);
        vSum = AdvSimd.Arm64.Add(vSum, vSum3);

        // Horizontal sum (NEON has pairwise add)
        double sum = AdvSimd.Arm64.AddPairwiseScalar(vSum).ToScalar();

        // Scalar remainder (0-1 elements)
        for (; i < len; i++)
        {
            sum += Unsafe.Add(ref aRef, i) * Unsafe.Add(ref bRef, i);
        }

        return sum;
    }

}
