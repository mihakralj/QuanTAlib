using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// Kurtosis: Measures the tailedness (heaviness of tails) of the probability distribution
/// of a real-valued random variable.
/// </summary>
/// <remarks>
/// This implementation calculates excess kurtosis (kurtosis - 3), so a normal distribution
/// has excess kurtosis of 0.
///
/// Interpretation:
/// - Positive (leptokurtic): Heavier tails than normal, more extreme events
/// - Zero (mesokurtic): Normal distribution tail behavior
/// - Negative (platykurtic): Lighter tails than normal, fewer extreme events
///
/// Formula (population excess kurtosis):
///   g₂ = m₄ / m₂² - 3
///
/// where:
///   m₂ = (1/n) Σ(xᵢ - μ)²  (second central moment)
///   m₄ = (1/n) Σ(xᵢ - μ)⁴  (fourth central moment)
///
/// Sample excess kurtosis applies Fisher's correction:
///   G₂ = ((n-1)/((n-2)(n-3))) * ((n+1)*g₂ + 6)
///
/// Implementation uses O(1) running sums of powers (x, x², x³, x⁴) with Kahan
/// compensated summation for numerical stability over long streams, eliminating
/// the need for periodic resynchronization.
/// </remarks>
[SkipLocalsInit]
public sealed class Kurtosis : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly bool _isPopulation;
    private double _sum;
    private double _sumSq;
    private double _sumCu;
    private double _sumQu;
    private double _sumComp;
    private double _sumSqComp;
    private double _sumCuComp;
    private double _sumQuComp;
    private double _p_sumComp;
    private double _p_sumSqComp;
    private double _p_sumCuComp;
    private double _p_sumQuComp;
    private const double Epsilon = 1e-10;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates a new Kurtosis indicator.
    /// </summary>
    /// <param name="period">The lookback period (must be >= 4).</param>
    /// <param name="isPopulation">If true, calculates Population Kurtosis. If false, Sample Kurtosis (default).</param>
    public Kurtosis(int period, bool isPopulation = false)
    {
        if (period < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 4 for Kurtosis.");
        }
        _period = period;
        _isPopulation = isPopulation;
        _buffer = new RingBuffer(period);
        Name = $"Kurtosis({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates a chained Kurtosis indicator.
    /// </summary>
    /// <param name="source">The source indicator to chain from.</param>
    /// <param name="period">The lookback period.</param>
    /// <param name="isPopulation">If true, calculates Population Kurtosis.</param>
    public Kurtosis(ITValuePublisher source, int period, bool isPopulation = false) : this(period, isPopulation)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Pub += HandleInput;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleInput(object? sender, in TValueEventArgs e)
    {
        Update(e.Value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // Snapshot current state for rollback
        double p_sum = _sum;
        double p_sumSq = _sumSq;
        double p_sumCu = _sumCu;
        double p_sumQu = _sumQu;
        _p_sumComp = _sumComp;
        _p_sumSqComp = _sumSqComp;
        _p_sumCuComp = _sumCuComp;
        _p_sumQuComp = _sumQuComp;

        if (isNew)
        {
            if (_buffer.IsFull)
            {
                double oldVal = _buffer.Oldest;
                double oldSq = oldVal * oldVal;

                // Kahan subtract oldVal from _sum
                double y = -oldVal - _sumComp;
                double t = _sum + y;
                _sumComp = (t - _sum) - y;
                _sum = t;

                // Kahan subtract oldSq from _sumSq
                y = -oldSq - _sumSqComp;
                t = _sumSq + y;
                _sumSqComp = (t - _sumSq) - y;
                _sumSq = t;

                // Kahan subtract oldCu from _sumCu
                y = -(oldSq * oldVal) - _sumCuComp;
                t = _sumCu + y;
                _sumCuComp = (t - _sumCu) - y;
                _sumCu = t;

                // Kahan subtract oldQu from _sumQu
                y = -(oldSq * oldSq) - _sumQuComp;
                t = _sumQu + y;
                _sumQuComp = (t - _sumQu) - y;
                _sumQu = t;
            }

            double val = input.Value;
            if (!double.IsFinite(val))
            {
                val = _buffer.Count > 0 ? _buffer.Newest : 0;
            }
            _buffer.Add(val);
            double valSq = val * val;

            // Kahan add val to _sum
            {
                double y = val - _sumComp;
                double t = _sum + y;
                _sumComp = (t - _sum) - y;
                _sum = t;
            }

            // Kahan add valSq to _sumSq
            {
                double y = valSq - _sumSqComp;
                double t = _sumSq + y;
                _sumSqComp = (t - _sumSq) - y;
                _sumSq = t;
            }

            // Kahan add valCu to _sumCu
            {
                double y = (valSq * val) - _sumCuComp;
                double t = _sumCu + y;
                _sumCuComp = (t - _sumCu) - y;
                _sumCu = t;
            }

            // Kahan add valQu to _sumQu
            {
                double y = (valSq * valSq) - _sumQuComp;
                double t = _sumQu + y;
                _sumQuComp = (t - _sumQu) - y;
                _sumQu = t;
            }
        }
        else
        {
            // Restore previous state before applying correction
            _sum = p_sum;
            _sumSq = p_sumSq;
            _sumCu = p_sumCu;
            _sumQu = p_sumQu;
            _sumComp = _p_sumComp;
            _sumSqComp = _p_sumSqComp;
            _sumCuComp = _p_sumCuComp;
            _sumQuComp = _p_sumQuComp;

            double oldNewest = _buffer.Newest;
            _buffer.UpdateNewest(input.Value);

            double val = input.Value;
            double valSq = val * val;
            double oldSq = oldNewest * oldNewest;

            // Kahan subtract old + add new for _sum
            {
                double y = (-oldNewest + val) - _sumComp;
                double t = _sum + y;
                _sumComp = (t - _sum) - y;
                _sum = t;
            }

            // Kahan subtract old + add new for _sumSq
            {
                double y = (-oldSq + valSq) - _sumSqComp;
                double t = _sumSq + y;
                _sumSqComp = (t - _sumSq) - y;
                _sumSq = t;
            }

            // Kahan subtract old + add new for _sumCu
            {
                double y = (-(oldSq * oldNewest) + (valSq * val)) - _sumCuComp;
                double t = _sumCu + y;
                _sumCuComp = (t - _sumCu) - y;
                _sumCu = t;
            }

            // Kahan subtract old + add new for _sumQu
            {
                double y = (-(oldSq * oldSq) + (valSq * valSq)) - _sumQuComp;
                double t = _sumQu + y;
                _sumQuComp = (t - _sumQu) - y;
                _sumQu = t;
            }
        }

        double kurtosis = 0;
        if (_buffer.Count >= 4)
        {
            double n = _buffer.Count;
            double mean = _sum / n;

            // Second central moment (variance): m₂ = Σ(x-μ)²/n
            // = (SumSq - Sum²/n) / n
            double m2Numerator = _sumSq - ((_sum * _sum) / n);
            if (m2Numerator < Epsilon)
            {
                m2Numerator = 0;
            }

            double m2 = m2Numerator / n;

            if (m2 > Epsilon)
            {
                // Fourth central moment: m₄ = Σ(x-μ)⁴/n
                // Expanding (x-μ)⁴ = x⁴ - 4x³μ + 6x²μ² - 4xμ³ + μ⁴
                // m₄ = SumQu/n - 4·mean·SumCu/n + 6·mean²·SumSq/n - 3·mean⁴
                // Note: last term -4·mean³·Sum/n + mean⁴ = -4·mean⁴ + mean⁴ = -3·mean⁴
                double meanSq = mean * mean;
                double m4 = (_sumQu / n)
                    - (4.0 * mean * _sumCu / n)
                    + (6.0 * meanSq * _sumSq / n)
                    - (3.0 * meanSq * meanSq);

                // Population excess kurtosis: g₂ = m₄/m₂² - 3
                double g2 = (m4 / (m2 * m2)) - 3.0;

                if (_isPopulation)
                {
                    kurtosis = g2;
                }
                else
                {
                    // Sample excess kurtosis (Fisher's correction):
                    // G₂ = ((n-1)/((n-2)(n-3))) · ((n+1)·g₂ + 6)
                    double denom = (n - 2.0) * (n - 3.0);
                    if (Math.Abs(denom) > Epsilon)
                    {
                        kurtosis = ((n - 1.0) / denom) * (((n + 1.0) * g2) + 6.0);
                    }
                }
            }
        }

        Last = new TValue(input.Time, kurtosis);
        PubEvent(Last);
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

        Batch(source.Values, vSpan, _period, _isPopulation);
        source.Times.CopyTo(tSpan);

        // Reset running state before priming
        _buffer.Clear();
        _sum = 0;
        _sumSq = 0;
        _sumCu = 0;
        _sumQu = 0;
        _sumComp = 0;
        _sumSqComp = 0;
        _sumCuComp = 0;
        _sumQuComp = 0;

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
        _sumQu = 0;
        _sumComp = 0;
        _sumSqComp = 0;
        _sumCuComp = 0;
        _sumQuComp = 0;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        DateTime ts = DateTime.MinValue;
        foreach (double value in source)
        {
            Update(new TValue(ts, value));
            if (step.HasValue)
            {
                ts = ts.Add(step.Value);
            }
        }
    }

    public static TSeries Batch(TSeries source, int period, bool isPopulation = false)
    {
        var kurtosis = new Kurtosis(period, isPopulation);
        return kurtosis.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, bool isPopulation = false)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period < 4)
        {
            throw new ArgumentException("Period must be greater than or equal to 4", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // SIMD path for large, clean datasets
        const int SimdThreshold = 256;
        if (len >= SimdThreshold && Avx2.IsSupported && !source.ContainsNonFinite())
        {
            CalculateAvx2Core(source, output, period, isPopulation);
            return;
        }

        // Scalar path
        CalculateScalarCore(source, output, period, isPopulation);
    }

    public static (TSeries Results, Kurtosis Indicator) Calculate(TSeries source, int period, bool isPopulation = false)
    {
        var indicator = new Kurtosis(period, isPopulation);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateKurtosisFromSums(double sum, double sumSq, double sumCu, double sumQu, double n, bool isPopulation)
    {
        double mean = sum / n;

        double m2Numerator = sumSq - ((sum * sum) / n);
        if (m2Numerator < Epsilon)
        {
            return 0;
        }

        double m2 = m2Numerator / n;

        if (m2 <= Epsilon)
        {
            return 0;
        }

        // Fourth central moment via raw moments
        double meanSq = mean * mean;
        double m4 = (sumQu / n)
            - (4.0 * mean * sumCu / n)
            + (6.0 * meanSq * sumSq / n)
            - (3.0 * meanSq * meanSq);

        double g2 = (m4 / (m2 * m2)) - 3.0;

        if (isPopulation)
        {
            return g2;
        }

        // Fisher's correction for sample excess kurtosis
        double denom = (n - 2.0) * (n - 3.0);
        if (Math.Abs(denom) < Epsilon)
        {
            return 0;
        }

        return ((n - 1.0) / denom) * (((n + 1.0) * g2) + 6.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period, bool isPopulation)
    {
        int len = source.Length;
        double sum = 0;
        double sumSq = 0;
        double sumCu = 0;
        double sumQu = 0;
        double sumC = 0, sqC = 0, cuC = 0, quC = 0; // Kahan compensation

        int i = 0;

        // Warmup phase
        int warmupEnd = Math.Min(period, len);
        for (; i < warmupEnd; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
            {
                val = 0;
            }

            double valSq = val * val;
            sum += val;
            sumSq += valSq;
            sumCu += valSq * val;
            sumQu += valSq * valSq;

            double n = i + 1;
            output[i] = (n >= 4) ? CalculateKurtosisFromSums(sum, sumSq, sumCu, sumQu, n, isPopulation) : 0;
        }

        // Sliding window phase — Kahan compensated
        for (; i < len; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
            {
                val = 0;
            }

            double oldVal = source[i - period];
            if (!double.IsFinite(oldVal))
            {
                oldVal = 0;
            }

            double valSq = val * val;
            double oldSq = oldVal * oldVal;

            // Kahan sum
            {
                double y = (val - oldVal) - sumC;
                double t = sum + y;
                sumC = (t - sum) - y;
                sum = t;
            }
            // Kahan sumSq
            {
                double y = (valSq - oldSq) - sqC;
                double t = sumSq + y;
                sqC = (t - sumSq) - y;
                sumSq = t;
            }
            // Kahan sumCu
            {
                double y = ((valSq * val) - (oldSq * oldVal)) - cuC;
                double t = sumCu + y;
                cuC = (t - sumCu) - y;
                sumCu = t;
            }
            // Kahan sumQu
            {
                double y = ((valSq * valSq) - (oldSq * oldSq)) - quC;
                double t = sumQu + y;
                quC = (t - sumQu) - y;
                sumQu = t;
            }

            output[i] = CalculateKurtosisFromSums(sum, sumSq, sumCu, sumQu, period, isPopulation);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WarmupKurtosis(int period, bool isPopulation, ref double srcRef, ref double outRef, out double sum, out double sumSq, out double sumCu, out double sumQu)
    {
        sum = 0;
        sumSq = 0;
        sumCu = 0;
        sumQu = 0;
        for (int i = 0; i < period; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            double valSq = val * val;
            sum += val;
            sumSq += valSq;
            sumCu += valSq * val;
            sumQu += valSq * valSq;

            double n = i + 1;
            Unsafe.Add(ref outRef, i) = (n >= 4) ? CalculateKurtosisFromSums(sum, sumSq, sumCu, sumQu, n, isPopulation) : 0;
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

        WarmupKurtosis(period, isPopulation, ref srcRef, ref outRef, out double sum, out double sumSq, out double sumCu, out double sumQu);

        if (len <= period)
        {
            return;
        }

        var vInvN = Vector256.Create(invN);
        var vThree = Vector256.Create(3.0);
        var vFour = Vector256.Create(4.0);
        var vSix = Vector256.Create(6.0);
        var vEpsilon = Vector256.Create(Epsilon);
        var vZero = Vector256<double>.Zero;

        // Fisher's correction constants
        double fisherNum = isPopulation ? 1.0 : (n - 1.0);
        double fisherDenom = isPopulation ? 1.0 : ((n - 2.0) * (n - 3.0));
        double fisherNp1 = isPopulation ? 1.0 : (n + 1.0);
        double fisherAdd = isPopulation ? 0.0 : 6.0;
        var vFisherScale = Vector256.Create(isPopulation ? 1.0 : fisherNum / fisherDenom);
        var vFisherNp1 = Vector256.Create(isPopulation ? 1.0 : fisherNp1);
        var vFisherAdd = Vector256.Create(fisherAdd);

        int simdEnd = period + (((len - period) / VectorWidth) * VectorWidth);

        for (int i = period; i < simdEnd; i += VectorWidth)
        {
            var vNew = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
            var vOld = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - period));

            // Deltas for Sum
            var vDelta = Avx.Subtract(vNew, vOld);

            // Deltas for SumSq
            var vNewSq = Avx.Multiply(vNew, vNew);
            var vOldSq = Avx.Multiply(vOld, vOld);
            var vDeltaSq = Avx.Subtract(vNewSq, vOldSq);

            // Deltas for SumCu
            var vNewCu = Avx.Multiply(vNewSq, vNew);
            var vOldCu = Avx.Multiply(vOldSq, vOld);
            var vDeltaCu = Avx.Subtract(vNewCu, vOldCu);

            // Deltas for SumQu
            var vNewQu = Avx.Multiply(vNewSq, vNewSq);
            var vOldQu = Avx.Multiply(vOldSq, vOldSq);
            var vDeltaQu = Avx.Subtract(vNewQu, vOldQu);

            // Prefix sum for Sum
            var vShift1 = Avx2.Permute4x64(vDelta.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131 - SIMD prefix sum pattern requires specific permutation
            vShift1 = Avx.Blend(vZero, vShift1, 0b_1110);
            var vP1 = Avx.Add(vDelta, vShift1);
            var vShift2 = Avx2.Permute4x64(vP1.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131 - SIMD prefix sum pattern requires specific permutation
            vShift2 = Avx.Blend(vZero, vShift2, 0b_1100);
            var vP2 = Avx.Add(vP1, vShift2);
            var vSums = Avx.Add(Vector256.Create(sum), vP2);

            // Prefix sum for SumSq
            var vShiftSq1 = Avx2.Permute4x64(vDeltaSq.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131 - SIMD prefix sum pattern requires specific permutation
            vShiftSq1 = Avx.Blend(vZero, vShiftSq1, 0b_1110);
            var vP1Sq = Avx.Add(vDeltaSq, vShiftSq1);
            var vShiftSq2 = Avx2.Permute4x64(vP1Sq.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131 - SIMD prefix sum pattern requires specific permutation
            vShiftSq2 = Avx.Blend(vZero, vShiftSq2, 0b_1100);
            var vP2Sq = Avx.Add(vP1Sq, vShiftSq2);
            var vSumSqs = Avx.Add(Vector256.Create(sumSq), vP2Sq);

            // Prefix sum for SumCu
            var vShiftCu1 = Avx2.Permute4x64(vDeltaCu.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131 - SIMD prefix sum pattern requires specific permutation
            vShiftCu1 = Avx.Blend(vZero, vShiftCu1, 0b_1110);
            var vP1Cu = Avx.Add(vDeltaCu, vShiftCu1);
            var vShiftCu2 = Avx2.Permute4x64(vP1Cu.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131 - SIMD prefix sum pattern requires specific permutation
            vShiftCu2 = Avx.Blend(vZero, vShiftCu2, 0b_1100);
            var vP2Cu = Avx.Add(vP1Cu, vShiftCu2);
            var vSumCus = Avx.Add(Vector256.Create(sumCu), vP2Cu);

            // Prefix sum for SumQu
            var vShiftQu1 = Avx2.Permute4x64(vDeltaQu.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131 - SIMD prefix sum pattern requires specific permutation
            vShiftQu1 = Avx.Blend(vZero, vShiftQu1, 0b_1110);
            var vP1Qu = Avx.Add(vDeltaQu, vShiftQu1);
            var vShiftQu2 = Avx2.Permute4x64(vP1Qu.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131 - SIMD prefix sum pattern requires specific permutation
            vShiftQu2 = Avx.Blend(vZero, vShiftQu2, 0b_1100);
            var vP2Qu = Avx.Add(vP1Qu, vShiftQu2);
            var vSumQus = Avx.Add(Vector256.Create(sumQu), vP2Qu);

            // Calculate Kurtosis
            var vMean = Avx.Multiply(vSums, vInvN);
            var vMeanSq = Avx.Multiply(vMean, vMean);

            // m2 = (SumSq - Sum²/n) / n
            var vSumSquared = Avx.Multiply(vSums, vSums);
            var vM2Num = Fma.IsSupported
                ? Fma.MultiplyAddNegated(vSumSquared, vInvN, vSumSqs)
                : Avx.Subtract(vSumSqs, Avx.Multiply(vSumSquared, vInvN));
            vM2Num = Avx.Max(vZero, vM2Num);
            var vM2 = Avx.Multiply(vM2Num, vInvN);

            // m4 = SumQu/n - 4·mean·SumCu/n + 6·mean²·SumSq/n - 3·mean⁴
            var vTerm1 = Avx.Multiply(vSumQus, vInvN);
            var vTerm2 = Avx.Multiply(vFour, Avx.Multiply(vMean, Avx.Multiply(vSumCus, vInvN)));
            var vTerm3 = Avx.Multiply(vSix, Avx.Multiply(vMeanSq, Avx.Multiply(vSumSqs, vInvN)));
            var vTerm4 = Avx.Multiply(vThree, Avx.Multiply(vMeanSq, vMeanSq));

            var vM4 = Avx.Subtract(Avx.Add(Avx.Subtract(vTerm1, vTerm2), vTerm3), vTerm4);

            // g2 = m4 / m2² - 3
            var vM2Sq = Avx.Multiply(vM2, vM2);
            var vG2 = Avx.Subtract(Avx.Divide(vM4, vM2Sq), vThree);

            // Apply Fisher's correction: scale * (np1 * g2 + 6)
            Vector256<double> vResult;
            if (isPopulation)
            {
                vResult = vG2;
            }
            else
            {
                var vCorrected = Fma.IsSupported
                    ? Fma.MultiplyAdd(vFisherNp1, vG2, vFisherAdd)
                    : Avx.Add(Avx.Multiply(vFisherNp1, vG2), vFisherAdd);
                vResult = Avx.Multiply(vFisherScale, vCorrected);
            }

            // Mask: zero out where m2 <= epsilon
            var vMask = Avx.Compare(vM2, vEpsilon, FloatComparisonMode.OrderedGreaterThanNonSignaling);
            vResult = Avx.BlendVariable(vZero, vResult, vMask);

            vResult.StoreUnsafe(ref Unsafe.Add(ref outRef, i));

            sum = vSums.GetElement(3);
            sumSq = vSumSqs.GetElement(3);
            sumCu = vSumCus.GetElement(3);
            sumQu = vSumQus.GetElement(3);
        }

        for (int i = simdEnd; i < len; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            double oldVal = Unsafe.Add(ref srcRef, i - period);

            double valSq = val * val;
            double oldSq = oldVal * oldVal;
            sum = sum - oldVal + val;
            sumSq = sumSq - oldSq + valSq;
            sumCu = Math.FusedMultiplyAdd(valSq, val, Math.FusedMultiplyAdd(-oldSq, oldVal, sumCu));
            sumQu = Math.FusedMultiplyAdd(valSq, valSq, Math.FusedMultiplyAdd(-oldSq, oldSq, sumQu));

            Unsafe.Add(ref outRef, i) = CalculateKurtosisFromSums(sum, sumSq, sumCu, sumQu, n, isPopulation);
        }
    }
}
