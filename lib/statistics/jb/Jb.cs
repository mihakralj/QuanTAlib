using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// JB: Jarque-Bera Test Statistic
/// </summary>
/// <remarks>
/// The Jarque-Bera test measures how far a distribution deviates from normality
/// by examining skewness and kurtosis. Under the null hypothesis of normality,
/// JB ~ χ²(2). Large values reject normality.
///
/// Formula:
///   JB = (n / 6) × (S² + EK² / 4)
///   where S = skewness = m₃ / m₂^(3/2)
///         EK = excess kurtosis = (m₄ / m₂²) − 3
///         mₖ = k-th central moment = Σ(xᵢ − x̄)ᵏ / n
///
/// O(1) streaming via running sums of x, x², x³, x⁴ with periodic resync
/// to limit floating-point drift.
///
/// Critical values (χ² with 2 df):
///   10% → 4.605, 5% → 5.991, 1% → 9.210
///
/// IsHot:
///   Becomes true when the buffer reaches full period length.
/// </remarks>
[SkipLocalsInit]
public sealed class Jb : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;
    private bool _disposed;

    private double _sum;
    private double _sumSq;
    private double _sumCu;
    private double _sumQu;
    private double _p_sum;
    private double _p_sumSq;
    private double _p_sumCu;
    private double _p_sumQu;
    private double _lastValidValue;
    private double _p_lastValidValue;
    private int _updateCount;

    private const int ResyncInterval = 1000;
    private const double Epsilon = 1e-10;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>Creates a new JB indicator with the specified period.</summary>
    /// <param name="period">The lookback period (must be >= 3).</param>
    public Jb(int period)
    {
        if (period < 3)
        {
            throw new ArgumentException("Period must be at least 3.", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Jb({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    public Jb(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        source.Pub += _handler;
    }

    public Jb(TSeries source, int period) : this(period)
    {
        _source = source;
        source.Pub += _handler;
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _buffer.Clear();
        _sum = 0;
        _sumSq = 0;
        _sumCu = 0;
        _sumQu = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        _updateCount = 0;

        int warmupLength = Math.Min(source.Length, WarmupPeriod);
        int startIndex = source.Length - warmupLength;

        for (int i = startIndex; i < source.Length; i++)
        {
            Update(new TValue(DateTime.MinValue, source[i]));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        // NaN/Infinity guard — substitute last valid
        if (!double.IsFinite(value))
        {
            value = _lastValidValue;
        }
        else
        {
            if (isNew)
            {
                _p_lastValidValue = _lastValidValue;
            }
            _lastValidValue = value;
        }

        if (isNew)
        {
            // Save state for rollback
            _p_sum = _sum;
            _p_sumSq = _sumSq;
            _p_sumCu = _sumCu;
            _p_sumQu = _sumQu;

            if (_buffer.IsFull)
            {
                double old = _buffer.Oldest;
                double oldSq = old * old;
                _sum -= old;
                _sumSq -= oldSq;
                _sumCu -= oldSq * old;
                _sumQu -= oldSq * oldSq;
            }

            _buffer.Add(value);
            double vSq = value * value;
            _sum += value;
            _sumSq += vSq;
            _sumCu += vSq * value;
            _sumQu += vSq * vSq;

            _updateCount++;
            if (_updateCount % ResyncInterval == 0)
            {
                Resync();
            }
        }
        else
        {
            // Restore previous state
            _lastValidValue = _p_lastValidValue;
            _sum = _p_sum;
            _sumSq = _p_sumSq;
            _sumCu = _p_sumCu;
            _sumQu = _p_sumQu;

            if (_buffer.Count > 0)
            {
                _buffer.UpdateNewest(value);
                Resync();
            }
            else
            {
                _buffer.Add(value);
                double vSq = value * value;
                _sum += value;
                _sumSq += vSq;
                _sumCu += vSq * value;
                _sumQu += vSq * vSq;
            }

            // Re-apply NaN guard for corrected value
            if (double.IsFinite(input.Value))
            {
                _lastValidValue = input.Value;
            }
        }

        double jb = CalculateJbFromSums(_sum, _sumSq, _sumCu, _sumQu, _buffer.Count);

        Last = new TValue(input.Time, jb);
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

        // Reset running state before priming
        _buffer.Clear();
        _sum = 0;
        _sumSq = 0;
        _sumCu = 0;
        _sumQu = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        _updateCount = 0;

        // Prime the state
        int primeStart = Math.Max(0, len - _period);
        for (int i = primeStart; i < len; i++)
        {
            Update(source[i]);
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var jb = new Jb(period);
        return jb.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }
        if (period < 3)
        {
            throw new ArgumentException("Period must be at least 3.", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Try SIMD path for large, clean datasets
        const int SimdThreshold = 256;
        if (len >= SimdThreshold && Avx2.IsSupported && !source.ContainsNonFinite())
        {
            CalculateAvx2Core(source, output, period);
            return;
        }

        // Scalar path
        CalculateScalarCore(source, output, period);
    }

    public static (TSeries Results, Jb Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Jb(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _sum = 0;
        _sumSq = 0;
        _sumCu = 0;
        _sumQu = 0;
        _p_sum = 0;
        _p_sumSq = 0;
        _p_sumCu = 0;
        _p_sumQu = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        _updateCount = 0;
        Last = default;
    }

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

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Private helpers
    /////////////////////////////////////////////////////////////////////////////////////////////////

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateJbFromSums(double sum, double sumSq, double sumCu, double sumQu, double n)
    {
        if (n < 3)
        {
            return 0;
        }

        double mean = sum / n;
        double meanSq = mean * mean;

        // m₂ = (ΣxÌ² - Σx²/n) / n
        double m2Numerator = sumSq - (sum * sum) / n;
        if (m2Numerator < Epsilon)
        {
            return 0;
        }
        double m2 = m2Numerator / n;

        if (m2 <= Epsilon)
        {
            return 0;
        }

        // m₃ = (Σx³ - 3·mean·Σx² + 2·n·mean³) / n
        double m3Numerator = sumCu - 3 * mean * sumSq + 2 * n * meanSq * mean;
        double m3 = m3Numerator / n;

        // m₄ = (Σx⁴ - 4·mean·Σx³ + 6·mean²·Σx² - 3·n·mean⁴) / n
        double m4Numerator = sumQu - 4 * mean * sumCu + 6 * meanSq * sumSq - 3 * n * meanSq * meanSq;
        double m4 = m4Numerator / n;

        // Skewness = m₃ / m₂^(3/2)
        double m2Sqrt = Math.Sqrt(m2);
        double skewness = m3 / (m2 * m2Sqrt);

        // Excess Kurtosis = (m₄ / m₂²) - 3
        double excessKurtosis = (m4 / (m2 * m2)) - 3.0;

        // JB = (n/6) × (S² + EK²/4)
        // skipcq: CS-R1140 — FMA for precision in JB formula
        return (n / 6.0) * Math.FusedMultiplyAdd(skewness, skewness, excessKurtosis * excessKurtosis / 4.0);
    }

    private void Resync()
    {
        double sum = 0, sumSq = 0, sumCu = 0, sumQu = 0;
        var span = _buffer.GetSpan();
        for (int i = 0; i < span.Length; i++)
        {
            double val = span[i];
            double vSq = val * val;
            sum += val;
            sumSq += vSq;
            sumCu += vSq * val;
            sumQu += vSq * vSq;
        }
        _sum = sum;
        _sumSq = sumSq;
        _sumCu = sumCu;
        _sumQu = sumQu;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;

        // Pre-process source: replace NaN/Infinity with lastValid so sliding-window
        // subtraction always uses the identical substituted value used during warmup.
        const int StackallocThreshold = 256;
        double[]? rented = null;
        scoped Span<double> sanitized;
        if (len <= StackallocThreshold)
        {
            sanitized = stackalloc double[len];
        }
        else
        {
            rented = ArrayPool<double>.Shared.Rent(len);
            sanitized = rented.AsSpan(0, len);
        }

        try
        {
            double lastValid = 0;
            for (int j = 0; j < len; j++)
            {
                double val = source[j];
                if (!double.IsFinite(val))
                {
                    val = lastValid;
                }
                else
                {
                    lastValid = val;
                }
                sanitized[j] = val;
            }

            double sum = 0, sumSq = 0, sumCu = 0, sumQu = 0;
            int i = 0;

            // Warmup phase
            int warmupEnd = Math.Min(period, len);
            for (; i < warmupEnd; i++)
            {
                double val = sanitized[i];
                double vSq = val * val;
                sum += val;
                sumSq += vSq;
                sumCu += vSq * val;
                sumQu += vSq * vSq;

                output[i] = CalculateJbFromSums(sum, sumSq, sumCu, sumQu, i + 1);
            }

            // Sliding window phase
            int tickCount = period;
            for (; i < len; i++)
            {
                double val = sanitized[i];
                double oldVal = sanitized[i - period];

                double vSq = val * val;
                double oSq = oldVal * oldVal;
                sum = sum - oldVal + val;
                sumSq = sumSq - oSq + vSq;
                sumCu = sumCu - (oSq * oldVal) + (vSq * val);
                sumQu = sumQu - (oSq * oSq) + (vSq * vSq);

                output[i] = CalculateJbFromSums(sum, sumSq, sumCu, sumQu, period);

                tickCount++;
                if (tickCount >= ResyncInterval)
                {
                    tickCount = 0;
                    ResyncFromSanitized(sanitized, i, period, ref sum, ref sumSq, ref sumCu, ref sumQu);
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ResyncFromSanitized(ReadOnlySpan<double> sanitized, int endIndex, int period,
        ref double sum, ref double sumSq, ref double sumCu, ref double sumQu)
    {
        double s = 0, sSq = 0, sCu = 0, sQu = 0;
        int startIdx = endIndex - period + 1;
        for (int k = 0; k < period; k++)
        {
            double v = sanitized[startIdx + k];
            double vSq = v * v;
            s += v;
            sSq += vSq;
            sCu += vSq * v;
            sQu += vSq * vSq;
        }
        sum = s;
        sumSq = sSq;
        sumCu = sCu;
        sumQu = sQu;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WarmupJb(int period, ref double srcRef, ref double outRef,
        out double sum, out double sumSq, out double sumCu, out double sumQu)
    {
        sum = 0; sumSq = 0; sumCu = 0; sumQu = 0;
        for (int i = 0; i < period; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            double vSq = val * val;
            sum += val;
            sumSq += vSq;
            sumCu += vSq * val;
            sumQu += vSq * vSq;

            Unsafe.Add(ref outRef, i) = CalculateJbFromSums(sum, sumSq, sumCu, sumQu, i + 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateAvx2Core(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int VectorWidth = 4;

        ref double srcRef = ref MemoryMarshal.GetReference(source);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        WarmupJb(period, ref srcRef, ref outRef, out double sum, out double sumSq, out double sumCu, out double sumQu);

        if (len <= period)
        {
            return;
        }

        double invN = 1.0 / period;
        double n = period;

        var vInvN = Vector256.Create(invN);
        var vN = Vector256.Create(n);
        var vThree = Vector256.Create(3.0);
        var vTwo = Vector256.Create(2.0);
        var vFour = Vector256.Create(4.0);
        var vSix = Vector256.Create(6.0);
        var vEpsilon = Vector256.Create(Epsilon);
        var vZero = Vector256<double>.Zero;

        int simdEnd = period + ((len - period) / VectorWidth) * VectorWidth;
        int tickCount = period;

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

            // Prefix sums for Sum
            var vShift1 = Avx2.Permute4x64(vDelta.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
            vShift1 = Avx.Blend(vZero, vShift1, 0b_1110);
            var vP1 = Avx.Add(vDelta, vShift1);
            var vShift2 = Avx2.Permute4x64(vP1.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
            vShift2 = Avx.Blend(vZero, vShift2, 0b_1100);
            var vSums = Avx.Add(Vector256.Create(sum), Avx.Add(vP1, vShift2));

            // Prefix sums for SumSq
            var vShiftSq1 = Avx2.Permute4x64(vDeltaSq.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftSq1 = Avx.Blend(vZero, vShiftSq1, 0b_1110);
            var vP1Sq = Avx.Add(vDeltaSq, vShiftSq1);
            var vShiftSq2 = Avx2.Permute4x64(vP1Sq.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftSq2 = Avx.Blend(vZero, vShiftSq2, 0b_1100);
            var vSumSqs = Avx.Add(Vector256.Create(sumSq), Avx.Add(vP1Sq, vShiftSq2));

            // Prefix sums for SumCu
            var vShiftCu1 = Avx2.Permute4x64(vDeltaCu.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftCu1 = Avx.Blend(vZero, vShiftCu1, 0b_1110);
            var vP1Cu = Avx.Add(vDeltaCu, vShiftCu1);
            var vShiftCu2 = Avx2.Permute4x64(vP1Cu.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftCu2 = Avx.Blend(vZero, vShiftCu2, 0b_1100);
            var vSumCus = Avx.Add(Vector256.Create(sumCu), Avx.Add(vP1Cu, vShiftCu2));

            // Prefix sums for SumQu
            var vShiftQu1 = Avx2.Permute4x64(vDeltaQu.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftQu1 = Avx.Blend(vZero, vShiftQu1, 0b_1110);
            var vP1Qu = Avx.Add(vDeltaQu, vShiftQu1);
            var vShiftQu2 = Avx2.Permute4x64(vP1Qu.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftQu2 = Avx.Blend(vZero, vShiftQu2, 0b_1100);
            var vSumQus = Avx.Add(Vector256.Create(sumQu), Avx.Add(vP1Qu, vShiftQu2));

            // Calculate JB for 4 lanes
            var vMean = Avx.Multiply(vSums, vInvN);
            var vMeanSq = Avx.Multiply(vMean, vMean);
            var vMeanCu = Avx.Multiply(vMeanSq, vMean);
            var vMeanQu = Avx.Multiply(vMeanSq, vMeanSq);

            // m₂ = (SumSq − Sum²/n) / n
            var vSumSquared = Avx.Multiply(vSums, vSums);
            var vM2Num = Fma.IsSupported
                ? Fma.MultiplyAddNegated(vSumSquared, vInvN, vSumSqs)
                : Avx.Subtract(vSumSqs, Avx.Multiply(vSumSquared, vInvN));
            vM2Num = Avx.Max(vZero, vM2Num);
            var vM2 = Avx.Multiply(vM2Num, vInvN);

            // m₃ = (SumCu − 3·mean·SumSq + 2·n·mean³) / n
            var vTerm3_2 = Avx.Multiply(vThree, Avx.Multiply(vMean, vSumSqs));
            var vNMeanCu = Avx.Multiply(vN, vMeanCu);
            var vM3Num = Fma.IsSupported
                ? Fma.MultiplyAdd(vTwo, vNMeanCu, Avx.Subtract(vSumCus, vTerm3_2))
                : Avx.Add(Avx.Subtract(vSumCus, vTerm3_2), Avx.Multiply(vTwo, vNMeanCu));
            var vM3 = Avx.Multiply(vM3Num, vInvN);

            // m₄ = (SumQu − 4·mean·SumCu + 6·mean²·SumSq − 3·n·mean⁴) / n
            var vTerm4_1 = Avx.Multiply(vFour, Avx.Multiply(vMean, vSumCus));
            var vTerm4_2 = Avx.Multiply(vSix, Avx.Multiply(vMeanSq, vSumSqs));
            var vTerm4_3 = Avx.Multiply(vThree, Avx.Multiply(vN, vMeanQu));
            var vM4Num = Avx.Add(Avx.Subtract(Avx.Subtract(vSumQus, vTerm4_1), vTerm4_3), vTerm4_2);
            var vM4 = Avx.Multiply(vM4Num, vInvN);

            // Skewness = m₃ / (m₂ · √m₂)
            var vM2Sqrt = Avx.Sqrt(vM2);
            var vSkewDenom = Avx.Multiply(vM2, vM2Sqrt);
            var vSkew = Avx.Divide(vM3, vSkewDenom);

            // Excess Kurtosis = (m₄ / m₂²) − 3
            var vM2Sq = Avx.Multiply(vM2, vM2);
            var vKurt = Avx.Subtract(Avx.Divide(vM4, vM2Sq), vThree);

            // JB = (n/6) × (S² + EK²/4)
            var vSkewSq = Avx.Multiply(vSkew, vSkew);
            var vKurtSq = Avx.Multiply(vKurt, vKurt);
            var vKurtTerm = Avx.Divide(vKurtSq, vFour);
            var vJbInner = Avx.Add(vSkewSq, vKurtTerm);
            var vNOver6 = Avx.Divide(vN, vSix);
            var vJb = Avx.Multiply(vNOver6, vJbInner);

            // Mask: zero out where m₂ is too small
            var vMask = Avx.Compare(vM2, vEpsilon, FloatComparisonMode.OrderedGreaterThanNonSignaling);
            vJb = Avx.BlendVariable(vZero, vJb, vMask);

            // Clamp negative JB to zero (numerical noise)
            vJb = Avx.Max(vZero, vJb);

            vJb.StoreUnsafe(ref Unsafe.Add(ref outRef, i));

            sum = vSums.GetElement(3);
            sumSq = vSumSqs.GetElement(3);
            sumCu = vSumCus.GetElement(3);
            sumQu = vSumQus.GetElement(3);

            tickCount += VectorWidth;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double s = 0, sSq = 0, sCu = 0, sQu = 0;
                int startIdx = i + VectorWidth - period;
                for (int k = 0; k < period; k++)
                {
                    double v = Unsafe.Add(ref srcRef, startIdx + k);
                    double v2 = v * v;
                    s += v;
                    sSq += v2;
                    sCu += v2 * v;
                    sQu += v2 * v2;
                }
                sum = s;
                sumSq = sSq;
                sumCu = sCu;
                sumQu = sQu;
            }
        }

        // Scalar tail
        for (int i = simdEnd; i < len; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            double oldVal = Unsafe.Add(ref srcRef, i - period);
            double vSq = val * val;
            double oSq = oldVal * oldVal;

            sum = sum - oldVal + val;
            sumSq = sumSq - oSq + vSq;
            sumCu = sumCu - (oSq * oldVal) + (vSq * val);
            sumQu = sumQu - (oSq * oSq) + (vSq * vSq);

            Unsafe.Add(ref outRef, i) = CalculateJbFromSums(sum, sumSq, sumCu, sumQu, n);
        }
    }
}
