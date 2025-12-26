using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// Skew: Measures the asymmetry of the probability distribution of a real-valued random variable about its mean.
/// </summary>
/// <remarks>
/// Skewness value interpretation:
/// - Negative skew: The left tail is longer; the mass of the distribution is concentrated on the right.
/// - Positive skew: The right tail is longer; the mass of the distribution is concentrated on the left.
/// - Zero skew: The tails on both sides of the mean balance out (e.g. symmetric distribution).
///
/// This implementation uses O(1) running sums of powers (x, x^2, x^3) to calculate moments.
/// </remarks>
[SkipLocalsInit]
public sealed class Skew : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly bool _isPopulation;
    private double _sum;
    private double _sumSq;
    private double _sumCu;
    private int _updateCount;
    private const int ResyncInterval = 1000;
    private const double Epsilon = 1e-10;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates a new Skew indicator.
    /// </summary>
    /// <param name="period">The lookback period (must be >= 3).</param>
    /// <param name="isPopulation">If true, calculates Population Skewness. If false, Sample Skewness (default).</param>
    public Skew(int period, bool isPopulation = false)
    {
        if (period < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 3 for Skewness.");
        }
        _period = period;
        _isPopulation = isPopulation;
        _buffer = new RingBuffer(period);
        Name = $"Skew({period})";
        WarmupPeriod = period;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            if (_buffer.IsFull)
            {
                double oldVal = _buffer.Oldest;
                _sum -= oldVal;
                _sumSq -= oldVal * oldVal;
                _sumCu -= oldVal * oldVal * oldVal;
            }

            _buffer.Add(input.Value);
            double val = input.Value;
            _sum += val;
            _sumSq += val * val;
            _sumCu += val * val * val;

            _updateCount++;
            if (_updateCount % ResyncInterval == 0)
            {
                Resync();
            }
        }
        else
        {
            double oldNewest = _buffer.Newest;
            _buffer.UpdateNewest(input.Value);
            
            double val = input.Value;
            _sum = _sum - oldNewest + val;
            _sumSq = _sumSq - (oldNewest * oldNewest) + (val * val);
            _sumCu = _sumCu - (oldNewest * oldNewest * oldNewest) + (val * val * val);
        }

        double skew = 0;
        if (_buffer.Count >= 3)
        {
            double n = _buffer.Count;
            double mean = _sum / n;
            
            // Calculate 2nd moment (Variance)
            // m2 = Sum((x-mean)^2) / n = (SumSq - Sum^2/n) / n
            double m2Numerator = _sumSq - (_sum * _sum) / n;
            if (m2Numerator < Epsilon) m2Numerator = 0;
            double m2 = m2Numerator / n;

            // Calculate 3rd moment
            // m3 = Sum((x-mean)^3) / n
            // Sum((x-mean)^3) = Sum(x^3 - 3x^2*mean + 3x*mean^2 - mean^3)
            // = Sum(x^3) - 3*mean*Sum(x^2) + 3*mean^2*Sum(x) - n*mean^3
            // = SumCu - 3*mean*SumSq + 3*mean^2*Sum - n*mean^3
            // Since Sum = n*mean:
            // = SumCu - 3*mean*SumSq + 2*n*mean^3
            
            double m3Numerator = _sumCu - 3 * mean * _sumSq + 2 * n * mean * mean * mean;
            double m3 = m3Numerator / n;

            if (m2 > Epsilon)
            {
                // Population Skewness = m3 / m2^(3/2)
                double g1 = m3 / (m2 * Math.Sqrt(m2));

                if (_isPopulation)
                {
                    skew = g1;
                }
                else
                {
                    // Sample Skewness = [sqrt(n(n-1)) / (n-2)] * g1
                    double correction = Math.Sqrt(n * (n - 1)) / (n - 2);
                    skew = correction * g1;
                }
            }
        }

        Last = new TValue(input.Time, skew);
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

        // Prime the state
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
        _sum = 0;
        _sumSq = 0;
        _sumCu = 0;
        _updateCount = 0;
        Last = default;
    }

    private void Resync()
    {
        double sum = 0;
        double sumSq = 0;
        double sumCu = 0;
        var span = _buffer.GetSpan();
        for (int i = 0; i < span.Length; i++)
        {
            double val = span[i];
            sum += val;
            sumSq += val * val;
            sumCu += val * val * val;
        }
        _sum = sum;
        _sumSq = sumSq;
        _sumCu = sumCu;
    }

    public override void Prime(ReadOnlySpan<double> source)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.UtcNow, value));
        }
    }

    public static TSeries Calculate(TSeries source, int period, bool isPopulation = false)
    {
        var skew = new Skew(period, isPopulation);
        return skew.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, bool isPopulation = false)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (period < 3)
            throw new ArgumentException("Period must be greater than or equal to 3", nameof(period));

        int len = source.Length;
        if (len == 0) return;

        // Try SIMD path for large, clean datasets
        // SIMD overhead amortizes well for datasets >= 256 elements
        const int SimdThreshold = 256;
        if (len >= SimdThreshold && Avx2.IsSupported && !source.ContainsNonFinite())
        {
            CalculateAvx2Core(source, output, period, isPopulation);
            return;
        }

        // Scalar path
        CalculateScalarCore(source, output, period, isPopulation);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period, bool isPopulation)
    {
        int len = source.Length;
        double sum = 0;
        double sumSq = 0;
        double sumCu = 0;
        
        int i = 0;

        // Warmup phase
        int warmupEnd = Math.Min(period, len);
        for (; i < warmupEnd; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val)) val = 0;

            sum += val;
            sumSq += val * val;
            sumCu += val * val * val;

            double n = i + 1;
            output[i] = (n >= 3) ? CalculateSkewFromSums(sum, sumSq, sumCu, n, isPopulation) : 0;
        }

        // Sliding window phase
        int tickCount = period;
        for (; i < len; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val)) val = 0;

            double oldVal = source[i - period];
            if (!double.IsFinite(oldVal)) oldVal = 0;
            
            sum = sum - oldVal + val;
            sumSq = sumSq - (oldVal * oldVal) + (val * val);
            sumCu = sumCu - (oldVal * oldVal * oldVal) + (val * val * val);
            
            output[i] = CalculateSkewFromSums(sum, sumSq, sumCu, period, isPopulation);

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcSum = 0;
                double recalcSumSq = 0;
                double recalcSumCu = 0;
                int startIdx = i - period + 1;
                for (int k = 0; k < period; k++)
                {
                    double v = source[startIdx + k];
                    if (!double.IsFinite(v)) v = 0;
                    recalcSum += v;
                    recalcSumSq += v * v;
                    recalcSumCu += v * v * v;
                }
                sum = recalcSum;
                sumSq = recalcSumSq;
                sumCu = recalcSumCu;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateSkewFromSums(double sum, double sumSq, double sumCu, double n, bool isPopulation)
    {
        double mean = sum / n;
        
        double m2Numerator = sumSq - (sum * sum) / n;
        if (m2Numerator < Epsilon) return 0;
        double m2 = m2Numerator / n;

        double m3Numerator = sumCu - 3 * mean * sumSq + 2 * n * mean * mean * mean;
        double m3 = m3Numerator / n;

        if (m2 <= Epsilon) return 0;

        double g1 = m3 / (m2 * Math.Sqrt(m2));

        if (isPopulation)
        {
            return g1;
        }

        double correction = Math.Sqrt(n * (n - 1)) / (n - 2);
        return correction * g1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WarmupSkew(int period, bool isPopulation, ref double srcRef, ref double outRef, out double sum, out double sumSq, out double sumCu)
    {
        sum = 0;
        sumSq = 0;
        sumCu = 0;
        for (int i = 0; i < period; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            sum += val;
            sumSq += val * val;
            sumCu += val * val * val;
            
            double n = i + 1;
            Unsafe.Add(ref outRef, i) = (n >= 3) ? CalculateSkewFromSums(sum, sumSq, sumCu, n, isPopulation) : 0;
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
        double n = period;
        double correction = isPopulation ? 1.0 : Math.Sqrt(n * (n - 1)) / (n - 2);

        WarmupSkew(period, isPopulation, ref srcRef, ref outRef, out double sum, out double sumSq, out double sumCu);

        if (len <= period) return;

        var vInvN = Vector256.Create(invN);
        var vN = Vector256.Create(n);
        var vCorrection = Vector256.Create(correction);
        var vThree = Vector256.Create(3.0);
        var vTwo = Vector256.Create(2.0);
        var vEpsilon = Vector256.Create(Epsilon);
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

            // Delta for SumCu
            var vNewCu = Avx.Multiply(vNewSq, vNew);
            var vOldCu = Avx.Multiply(vOldSq, vOld);
            var vDeltaCu = Avx.Subtract(vNewCu, vOldCu);

            // Prefix sum for Sum
            // Shift 1: [0, d0, d1, d2]
            var vShift1 = Avx2.Permute4x64(vDelta.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
            vShift1 = Avx.Blend(vZero, vShift1, 0b_1110);
            var vP1 = Avx.Add(vDelta, vShift1);
            
            // Shift 2: [0, 0, d0, d0+d1]
            var vShift2 = Avx2.Permute4x64(vP1.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
            vShift2 = Avx.Blend(vZero, vShift2, 0b_1100);
            var vP2 = Avx.Add(vP1, vShift2);
            
            var vSums = Avx.Add(Vector256.Create(sum), vP2);

            // Prefix sum for SumSq
            var vShiftSq1 = Avx2.Permute4x64(vDeltaSq.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftSq1 = Avx.Blend(vZero, vShiftSq1, 0b_1110);
            var vP1Sq = Avx.Add(vDeltaSq, vShiftSq1);
            
            var vShiftSq2 = Avx2.Permute4x64(vP1Sq.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftSq2 = Avx.Blend(vZero, vShiftSq2, 0b_1100);
            var vP2Sq = Avx.Add(vP1Sq, vShiftSq2);
            
            var vSumSqs = Avx.Add(Vector256.Create(sumSq), vP2Sq);

            // Prefix sum for SumCu
            var vShiftCu1 = Avx2.Permute4x64(vDeltaCu.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftCu1 = Avx.Blend(vZero, vShiftCu1, 0b_1110);
            var vP1Cu = Avx.Add(vDeltaCu, vShiftCu1);
            
            var vShiftCu2 = Avx2.Permute4x64(vP1Cu.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftCu2 = Avx.Blend(vZero, vShiftCu2, 0b_1100);
            var vP2Cu = Avx.Add(vP1Cu, vShiftCu2);
            
            var vSumCus = Avx.Add(Vector256.Create(sumCu), vP2Cu);

            // Calculate Skewness
            var vMean = Avx.Multiply(vSums, vInvN);
            var vMeanSq = Avx.Multiply(vMean, vMean);
            var vMeanCu = Avx.Multiply(vMeanSq, vMean);

            // m2 = (SumSq - Sum^2/n) / n
            var vSumSquared = Avx.Multiply(vSums, vSums);
            var vM2Num = Fma.IsSupported
                ? Fma.MultiplyAddNegated(vSumSquared, vInvN, vSumSqs)
                : Avx.Subtract(vSumSqs, Avx.Multiply(vSumSquared, vInvN));

            vM2Num = Avx.Max(vZero, vM2Num);
            var vM2 = Avx.Multiply(vM2Num, vInvN);

            // m3 = (SumCu - 3*mean*SumSq + 2*n*mean^3) / n
            var vTerm2 = Avx.Multiply(vThree, Avx.Multiply(vMean, vSumSqs));
            var vNMeanCu = Avx.Multiply(vN, vMeanCu);

            var vM3Num = Fma.IsSupported
                ? Fma.MultiplyAdd(vTwo, vNMeanCu, Avx.Subtract(vSumCus, vTerm2))
                : Avx.Add(Avx.Subtract(vSumCus, vTerm2), Avx.Multiply(vTwo, vNMeanCu));

            var vM3 = Avx.Multiply(vM3Num, vInvN);

            // g1 = m3 / (m2 * sqrt(m2))
            var vM2Sqrt = Avx.Sqrt(vM2);
            var vDenom = Avx.Multiply(vM2, vM2Sqrt);
            
            // Check for small m2
            var vMask = Avx.Compare(vM2, vEpsilon, FloatComparisonMode.OrderedGreaterThanNonSignaling);
            
            var vG1 = Avx.Divide(vM3, vDenom);
            var vSkew = Avx.Multiply(vG1, vCorrection);
            
            // Apply mask
            vSkew = Avx.BlendVariable(vZero, vSkew, vMask);

            Vector256.StoreUnsafe(vSkew, ref Unsafe.Add(ref outRef, i));

            sum = vSums.GetElement(3);
            sumSq = vSumSqs.GetElement(3);
            sumCu = vSumCus.GetElement(3);

            tickCount += VectorWidth;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcSum = 0;
                double recalcSumSq = 0;
                double recalcSumCu = 0;
                int startIdx = i + VectorWidth - period;
                for (int k = 0; k < period; k++)
                {
                    double v = Unsafe.Add(ref srcRef, startIdx + k);
                    recalcSum += v;
                    recalcSumSq += v * v;
                    recalcSumCu += v * v * v;
                }
                sum = recalcSum;
                sumSq = recalcSumSq;
                sumCu = recalcSumCu;
            }
        }

        for (int i = simdEnd; i < len; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            double oldVal = Unsafe.Add(ref srcRef, i - period);
            
            sum = sum - oldVal + val;
            sumSq = sumSq - (oldVal * oldVal) + (val * val);
            sumCu = sumCu - (oldVal * oldVal * oldVal) + (val * val * val);
            
            Unsafe.Add(ref outRef, i) = CalculateSkewFromSums(sum, sumSq, sumCu, n, isPopulation);
        }
    }
}
