using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// Covariance: Measures the joint variability of two random variables.
/// </summary>
/// <remarks>
/// Covariance indicates the direction of the linear relationship between variables.
/// - Positive covariance: Variables tend to move in the same direction.
/// - Negative covariance: Variables tend to move in opposite directions.
/// - Zero covariance: Variables are uncorrelated.
///
/// Formula:
/// Cov(X, Y) = Sum((x - mean(x)) * (y - mean(y))) / n (Population)
/// Cov(X, Y) = Sum((x - mean(x)) * (y - mean(y))) / (n - 1) (Sample)
///
/// This implementation uses the O(1) running sum formula:
/// Cov(X, Y) = (Sum(xy) - Sum(x)*Sum(y)/n) / n (or n-1)
/// </remarks>
[SkipLocalsInit]
public sealed class Covariance : AbstractBase
{
    private readonly bool _isPopulation;
    private readonly RingBuffer _bufferX;
    private readonly RingBuffer _bufferY;

    private double _sumX;
    private double _sumY;
    private double _sumXY;
    private int _updateCount;
    private const int ResyncInterval = 1000;

    public override bool IsHot => _bufferX.IsFull;

    /// <summary>
    /// Creates a new Covariance indicator.
    /// </summary>
    /// <param name="period">The lookback period (must be >= 2).</param>
    /// <param name="isPopulation">If true, calculates Population Covariance. If false, Sample Covariance (default).</param>
    public Covariance(int period, bool isPopulation = false)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        _isPopulation = isPopulation;
        _bufferX = new RingBuffer(period);
        _bufferY = new RingBuffer(period);
        Name = $"Cov({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Updates the Covariance indicator with new values.
    /// </summary>
    /// <param name="x">The first value (TValue).</param>
    /// <param name="y">The second value (TValue).</param>
    /// <param name="isNew">Whether this is a new bar.</param>
    /// <returns>The calculated Covariance value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue x, TValue y, bool isNew = true)
    {
        if (isNew)
        {
            if (_bufferX.IsFull)
            {
                double oldX = _bufferX.Oldest;
                double oldY = _bufferY.Oldest;

                _sumX -= oldX;
                _sumY -= oldY;
                _sumXY -= oldX * oldY;
            }

            _bufferX.Add(x.Value);
            _bufferY.Add(y.Value);

            double valX = x.Value;
            double valY = y.Value;

            _sumX += valX;
            _sumY += valY;
            _sumXY += valX * valY;

            _updateCount++;
            if (_updateCount % ResyncInterval == 0)
            {
                Resync();
            }
        }
        else
        {
            double oldX = _bufferX.Newest;
            double oldY = _bufferY.Newest;

            _bufferX.UpdateNewest(x.Value);
            _bufferY.UpdateNewest(y.Value);

            double valX = x.Value;
            double valY = y.Value;

            _sumX = _sumX - oldX + valX;
            _sumY = _sumY - oldY + valY;
            _sumXY = _sumXY - (oldX * oldY) + (valX * valY);
        }

        double cov = 0;
        int n = _bufferX.Count;
        if (n >= 2)
        {
            double numerator = _sumXY - (_sumX * _sumY) / n;
            double denominator = _isPopulation ? n : (n - 1);
            cov = numerator / denominator;
        }

        Last = new TValue(x.Time, cov);
        PubEvent(Last);
        return Last;
    }

    public TValue Update(double x, double y, bool isNew = true)
    {
        return Update(new TValue(DateTime.UtcNow, x), new TValue(DateTime.UtcNow, y), isNew);
    }

    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("Covariance requires two inputs. Use Update(x, y).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("Covariance requires two inputs. Use Update(x, y).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("Covariance requires two inputs. Use Update(x, y).");
    }

    public override void Reset()
    {
        _bufferX.Clear();
        _bufferY.Clear();
        _sumX = 0;
        _sumY = 0;
        _sumXY = 0;
        _updateCount = 0;
        Last = default;
    }

    private void Resync()
    {
        double sumX = 0;
        double sumY = 0;
        double sumXY = 0;

        for (int i = 0; i < _bufferX.Count; i++)
        {
            double x = _bufferX[i];
            double y = _bufferY[i];

            sumX += x;
            sumY += y;
            sumXY += x * y;
        }

        _sumX = sumX;
        _sumY = sumY;
        _sumXY = sumXY;
    }

    public static TSeries Calculate(TSeries sourceX, TSeries sourceY, int period, bool isPopulation = false)
    {
        if (sourceX.Count != sourceY.Count)
            throw new ArgumentException("Source series must have the same length", nameof(sourceY));

        int len = sourceX.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(sourceX.Values, sourceY.Values, vSpan, period, isPopulation);
        sourceX.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> sourceX, ReadOnlySpan<double> sourceY, Span<double> output, int period, bool isPopulation = false)
    {
        if (sourceX.Length != sourceY.Length || sourceX.Length != output.Length)
            throw new ArgumentException("All spans must have the same length", nameof(output));
        if (period < 2)
            throw new ArgumentException("Period must be greater than or equal to 2", nameof(period));

        int len = sourceX.Length;
        if (len == 0) return;

        // SIMD overhead amortizes well for datasets >= 256 elements
        const int SimdThreshold = 256;
        if (len >= SimdThreshold && !sourceX.ContainsNonFinite() && !sourceY.ContainsNonFinite() && Avx2.IsSupported)
        {
            CalculateAvx2Core(sourceX, sourceY, output, period, isPopulation);
            return;
        }

        CalculateScalarCore(sourceX, sourceY, output, period, isPopulation);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> sourceX, ReadOnlySpan<double> sourceY, Span<double> output, int period, bool isPopulation)
    {
        int len = sourceX.Length;
        double sumX = 0;
        double sumY = 0;
        double sumXY = 0;

        const int StackAllocThreshold = 256;
        Span<double> bufferX = period <= StackAllocThreshold ? stackalloc double[period] : new double[period];
        Span<double> bufferY = period <= StackAllocThreshold ? stackalloc double[period] : new double[period];

        int bufferIndex = 0;
        int i = 0;

        // Warmup
        int warmupEnd = Math.Min(period, len);
        for (; i < warmupEnd; i++)
        {
            double x = sourceX[i];
            double y = sourceY[i];
            if (!double.IsFinite(x)) x = 0;
            if (!double.IsFinite(y)) y = 0;

            sumX += x;
            sumY += y;
            sumXY += x * y;
            bufferX[i] = x;
            bufferY[i] = y;

            double n = i + 1;
            if (n >= 2)
            {
                double numerator = sumXY - (sumX * sumY) / n;
                double denominator = isPopulation ? n : (n - 1);
                output[i] = numerator / denominator;
            }
            else
            {
                output[i] = 0;
            }
        }

        // Sliding window
        int tickCount = period;
        for (; i < len; i++)
        {
            double x = sourceX[i];
            double y = sourceY[i];
            if (!double.IsFinite(x)) x = 0;
            if (!double.IsFinite(y)) y = 0;

            double oldX = bufferX[bufferIndex];
            double oldY = bufferY[bufferIndex];

            sumX = sumX - oldX + x;
            sumY = sumY - oldY + y;
            sumXY = sumXY - (oldX * oldY) + (x * y);

            bufferX[bufferIndex] = x;
            bufferY[bufferIndex] = y;
            bufferIndex++;
            if (bufferIndex >= period) bufferIndex = 0;

            double n = period;
            double numerator = sumXY - (sumX * sumY) / n;
            double denominator = isPopulation ? n : (n - 1);
            output[i] = numerator / denominator;

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcSumX = 0;
                double recalcSumY = 0;
                double recalcSumXY = 0;
                for (int k = 0; k < period; k++)
                {
                    double bx = bufferX[k];
                    double by = bufferY[k];
                    recalcSumX += bx;
                    recalcSumY += by;
                    recalcSumXY += bx * by;
                }
                sumX = recalcSumX;
                sumY = recalcSumY;
                sumXY = recalcSumXY;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (double sumX, double sumY, double sumXY) WarmupCovariance(int period, bool isPopulation, ref double srcXRef, ref double srcYRef, ref double outRef)
    {
        double sumX = 0;
        double sumY = 0;
        double sumXY = 0;
        for (int i = 0; i < period; i++)
        {
            double x = Unsafe.Add(ref srcXRef, i);
            double y = Unsafe.Add(ref srcYRef, i);
            sumX += x;
            sumY += y;
            sumXY += x * y;

            double n = i + 1;
            if (n >= 2)
            {
                double num = sumXY - (sumX * sumY) / n;
                double den = isPopulation ? n : (n - 1);
                Unsafe.Add(ref outRef, i) = num / den;
            }
            else
            {
                Unsafe.Add(ref outRef, i) = 0;
            }
        }
        return (sumX, sumY, sumXY);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateAvx2Core(ReadOnlySpan<double> sourceX, ReadOnlySpan<double> sourceY, Span<double> output, int period, bool isPopulation)
    {
        int len = sourceX.Length;
        const int VectorWidth = 4;

        ref double srcXRef = ref MemoryMarshal.GetReference(sourceX);
        ref double srcYRef = ref MemoryMarshal.GetReference(sourceY);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        double invN = 1.0 / period;
        double invDenom = 1.0 / (isPopulation ? period : (period - 1));

        (double sumX, double sumY, double sumXY) = WarmupCovariance(period, isPopulation, ref srcXRef, ref srcYRef, ref outRef);

        if (len <= period) return;

        var vInvN = Vector256.Create(invN);
        var vInvDenom = Vector256.Create(invDenom);
        var vZero = Vector256<double>.Zero;

        int simdEnd = period + ((len - period) / VectorWidth) * VectorWidth;
        int tickCount = period;

        for (int i = period; i < simdEnd; i += VectorWidth)
        {
            var vNewX = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcXRef, i));
            var vOldX = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcXRef, i - period));
            var vNewY = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcYRef, i));
            var vOldY = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcYRef, i - period));

            // Delta for SumX
            var vDeltaX = Avx.Subtract(vNewX, vOldX);
            // Delta for SumY
            var vDeltaY = Avx.Subtract(vNewY, vOldY);

            // Delta for SumXY
            var vNewXY = Avx.Multiply(vNewX, vNewY);
            var vOldXY = Avx.Multiply(vOldX, vOldY);
            var vDeltaXY = Avx.Subtract(vNewXY, vOldXY);

            // Prefix sum for SumX
            var vShiftX1 = Avx2.Permute4x64(vDeltaX.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftX1 = Avx.Blend(vZero, vShiftX1, 0b_1110);
            var vP1X = Avx.Add(vDeltaX, vShiftX1);
            var vShiftX2 = Avx2.Permute4x64(vP1X.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftX2 = Avx.Blend(vZero, vShiftX2, 0b_1100);
            var vP2X = Avx.Add(vP1X, vShiftX2);
            var vSumXPrev = Vector256.Create(sumX);
            var vSumsX = Avx.Add(vSumXPrev, vP2X);

            // Prefix sum for SumY
            var vShiftY1 = Avx2.Permute4x64(vDeltaY.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftY1 = Avx.Blend(vZero, vShiftY1, 0b_1110);
            var vP1Y = Avx.Add(vDeltaY, vShiftY1);
            var vShiftY2 = Avx2.Permute4x64(vP1Y.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftY2 = Avx.Blend(vZero, vShiftY2, 0b_1100);
            var vP2Y = Avx.Add(vP1Y, vShiftY2);
            var vSumYPrev = Vector256.Create(sumY);
            var vSumsY = Avx.Add(vSumYPrev, vP2Y);

            // Prefix sum for SumXY
            var vShiftXY1 = Avx2.Permute4x64(vDeltaXY.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftXY1 = Avx.Blend(vZero, vShiftXY1, 0b_1110);
            var vP1XY = Avx.Add(vDeltaXY, vShiftXY1);
            var vShiftXY2 = Avx2.Permute4x64(vP1XY.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
            vShiftXY2 = Avx.Blend(vZero, vShiftXY2, 0b_1100);
            var vP2XY = Avx.Add(vP1XY, vShiftXY2);
            var vSumXYPrev = Vector256.Create(sumXY);
            var vSumsXY = Avx.Add(vSumXYPrev, vP2XY);

            // Calculate Covariance with FMA
            // Cov = (SumXY - (SumX*SumY)/N) / Denom
            var vSumXSumY = Avx.Multiply(vSumsX, vSumsY);
            var vNumerator = Fma.IsSupported
                ? Fma.MultiplyAddNegated(vSumXSumY, vInvN, vSumsXY)
                : Avx.Subtract(vSumsXY, Avx.Multiply(vSumXSumY, vInvN));
            var vResult = Avx.Multiply(vNumerator, vInvDenom);
            Vector256.StoreUnsafe(vResult, ref Unsafe.Add(ref outRef, i));

            sumX = vSumsX.GetElement(3);
            sumY = vSumsY.GetElement(3);
            sumXY = vSumsXY.GetElement(3);

            tickCount += VectorWidth;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcSumX = 0;
                double recalcSumY = 0;
                double recalcSumXY = 0;
                int startIdx = i + VectorWidth - period;
                for (int k = 0; k < period; k++)
                {
                    double x = Unsafe.Add(ref srcXRef, startIdx + k);
                    double y = Unsafe.Add(ref srcYRef, startIdx + k);
                    recalcSumX += x;
                    recalcSumY += y;
                    recalcSumXY += x * y;
                }
                sumX = recalcSumX;
                sumY = recalcSumY;
                sumXY = recalcSumXY;
            }
        }

        for (int i = simdEnd; i < len; i++)
        {
            double x = Unsafe.Add(ref srcXRef, i);
            double y = Unsafe.Add(ref srcYRef, i);
            if (!double.IsFinite(x)) x = 0;
            if (!double.IsFinite(y)) y = 0;

            double oldX = Unsafe.Add(ref srcXRef, i - period);
            double oldY = Unsafe.Add(ref srcYRef, i - period);
            if (!double.IsFinite(oldX)) oldX = 0;
            if (!double.IsFinite(oldY)) oldY = 0;

            sumX = sumX - oldX + x;
            sumY = sumY - oldY + y;
            sumXY = sumXY - (oldX * oldY) + (x * y);

            double numerator = sumXY - sumX * sumY * invN;
            Unsafe.Add(ref outRef, i) = numerator * invDenom;
        }
    }
}
