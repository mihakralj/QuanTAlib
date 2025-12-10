using System.Numerics;
using System.Runtime.CompilerServices;

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
            var minVec = new Vector<double>(span.Slice(0, vectorSize));
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
            var maxVec = new Vector<double>(span.Slice(0, vectorSize));
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
            var minVec = new Vector<double>(span.Slice(0, vectorSize));
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
}
