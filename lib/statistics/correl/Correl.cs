using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
using static System.Math;

namespace QuanTAlib;

/// <summary>
/// Correlation: Calculates Pearson's correlation coefficient between two price series
/// using a streaming single-pass algorithm with circular buffers and Kahan compensated
/// summation for numerical stability over long streams.
/// </summary>
/// <remarks>
/// The Pearson correlation coefficient measures the linear relationship between two variables.
/// It ranges from -1 (perfect negative correlation) to +1 (perfect positive correlation).
///
/// Algorithm:
/// 1. Maintain running sums: Σx, Σy, Σx², Σy², Σxy
/// 2. Calculate means: μx = Σx/n, μy = Σy/n
/// 3. Calculate variances: σx² = Σx²/n - μx², σy² = Σy²/n - μy²
/// 4. Calculate covariance: cov(x,y) = Σxy/n - μx×μy
/// 5. Correlation: r = cov(x,y) / (σx × σy)
///
/// Interpretation:
/// - r = +1: Perfect positive linear relationship
/// - r = -1: Perfect negative linear relationship
/// - r = 0: No linear relationship
/// - |r| > 0.7: Strong correlation
/// - 0.3 < |r| < 0.7: Moderate correlation
/// - |r| < 0.3: Weak correlation
/// </remarks>
[SkipLocalsInit]
public sealed class Correl : AbstractBase
{
    private readonly RingBuffer _bufferX;
    private readonly RingBuffer _bufferY;

    // Running sums for O(1) statistics
    private double _sumX, _sumY;
    private double _sumX2, _sumY2;
    private double _sumXY;

    // Kahan compensation terms
    private double _sumXComp, _sumYComp;
    private double _sumX2Comp, _sumY2Comp;
    private double _sumXYComp;

    // Previous compensation state for rollback
    private double _p_sumXComp, _p_sumYComp;
    private double _p_sumX2Comp, _p_sumY2Comp;
    private double _p_sumXYComp;

    // Last valid values for NaN handling
    private double _lastValidX, _lastValidY;
    private double _p_lastValidX, _p_lastValidY;

    private const double Epsilon = 1e-10;

    /// <inheritdoc />
    public override bool IsHot => _bufferX.Count >= WarmupPeriod;

    /// <summary>
    /// Creates a new Correl indicator.
    /// </summary>
    /// <param name="period">Lookback period for calculation (must be > 1)</param>
    public Correl(int period = 20)
    {
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        _bufferX = new RingBuffer(period);
        _bufferY = new RingBuffer(period);

        Name = $"Correl({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Updates the Correlation indicator with new values from both series.
    /// </summary>
    /// <param name="seriesX">First series value</param>
    /// <param name="seriesY">Second series value</param>
    /// <param name="isNew">Whether this is a new bar</param>
    /// <returns>The Pearson correlation coefficient (-1 to +1)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue seriesX, TValue seriesY, bool isNew = true)
    {
        if (isNew)
        {
            _p_lastValidX = _lastValidX;
            _p_lastValidY = _lastValidY;
            _p_sumXComp = _sumXComp;
            _p_sumYComp = _sumYComp;
            _p_sumX2Comp = _sumX2Comp;
            _p_sumY2Comp = _sumY2Comp;
            _p_sumXYComp = _sumXYComp;
        }
        else
        {
            _lastValidX = _p_lastValidX;
            _lastValidY = _p_lastValidY;
            _sumXComp = _p_sumXComp;
            _sumYComp = _p_sumYComp;
            _sumX2Comp = _p_sumX2Comp;
            _sumY2Comp = _p_sumY2Comp;
            _sumXYComp = _p_sumXYComp;
        }

        double x = SanitizeX(seriesX.Value);
        double y = SanitizeY(seriesY.Value);

        if (isNew)
        {
            ProcessNewBar(x, y);
        }
        else
        {
            ProcessBarCorrection(x, y);
        }

        double correlation = CalculateCorrel();

        Last = new TValue(seriesX.Time, correlation);
        PubEvent(Last);
        return Last;
    }

    /// <summary>
    /// Updates with raw double values.
    /// </summary>
    /// <remarks>
    /// Stamps both inputs with <c>DateTime.UtcNow</c> as their timestamp. For
    /// deterministic or replay-safe sequences use
    /// <see cref="Update(TValue, TValue, bool)"/> with explicit timestamps instead.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double seriesX, double seriesY, bool isNew = true)
    {
        DateTime now = DateTime.UtcNow;
        return Update(new TValue(now, seriesX), new TValue(now, seriesY), isNew);
    }
    /// <summary>Not supported. This indicator requires two inputs; use <see cref="Update(TValue, TValue, bool)"/> instead.</summary>
    /// <remarks>Not supported for bi-input indicator. Use Update(seriesX, seriesY) instead.</remarks>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("Correl requires two inputs (seriesX and seriesY). Use Update(seriesX, seriesY).");
    }
    /// <summary>Not supported. This indicator requires two inputs; use <see cref="Batch(TSeries, TSeries, int)"/> instead.</summary>
    /// <remarks>Not supported for bi-input indicator. Use Calculate(seriesX, seriesY, period) instead.</remarks>
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("Correl requires two inputs. Use Batch(seriesX, seriesY, period).");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double SanitizeX(double value)
    {
        if (double.IsFinite(value))
        {
            _lastValidX = value;
            return value;
        }
        return double.IsFinite(_lastValidX) ? _lastValidX : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double SanitizeY(double value)
    {
        if (double.IsFinite(value))
        {
            _lastValidY = value;
            return value;
        }
        return double.IsFinite(_lastValidY) ? _lastValidY : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessNewBar(double x, double y)
    {
        // Remove oldest values if buffer is full
        if (_bufferX.IsFull)
        {
            double oldX = _bufferX.Oldest;
            double oldY = _bufferY.Oldest;

            // Kahan subtract oldX from _sumX
            {
                double yk = -oldX - _sumXComp;
                double t = _sumX + yk;
                _sumXComp = (t - _sumX) - yk;
                _sumX = t;
            }
            // Kahan subtract oldY from _sumY
            {
                double yk = -oldY - _sumYComp;
                double t = _sumY + yk;
                _sumYComp = (t - _sumY) - yk;
                _sumY = t;
            }
            // Kahan subtract oldX² from _sumX2
            {
                double yk = -(oldX * oldX) - _sumX2Comp;
                double t = _sumX2 + yk;
                _sumX2Comp = (t - _sumX2) - yk;
                _sumX2 = t;
            }
            // Kahan subtract oldY² from _sumY2
            {
                double yk = -(oldY * oldY) - _sumY2Comp;
                double t = _sumY2 + yk;
                _sumY2Comp = (t - _sumY2) - yk;
                _sumY2 = t;
            }
            // Kahan subtract oldX*oldY from _sumXY
            {
                double yk = -(oldX * oldY) - _sumXYComp;
                double t = _sumXY + yk;
                _sumXYComp = (t - _sumXY) - yk;
                _sumXY = t;
            }
        }

        // Add new values
        _bufferX.Add(x);
        _bufferY.Add(y);

        // Kahan add x to _sumX
        {
            double yk = x - _sumXComp;
            double t = _sumX + yk;
            _sumXComp = (t - _sumX) - yk;
            _sumX = t;
        }
        // Kahan add y to _sumY
        {
            double yk = y - _sumYComp;
            double t = _sumY + yk;
            _sumYComp = (t - _sumY) - yk;
            _sumY = t;
        }
        // Kahan add x² to _sumX2
        {
            double yk = (x * x) - _sumX2Comp;
            double t = _sumX2 + yk;
            _sumX2Comp = (t - _sumX2) - yk;
            _sumX2 = t;
        }
        // Kahan add y² to _sumY2
        {
            double yk = (y * y) - _sumY2Comp;
            double t = _sumY2 + yk;
            _sumY2Comp = (t - _sumY2) - yk;
            _sumY2 = t;
        }
        // Kahan add x*y to _sumXY
        {
            double yk = (x * y) - _sumXYComp;
            double t = _sumXY + yk;
            _sumXYComp = (t - _sumXY) - yk;
            _sumXY = t;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBarCorrection(double x, double y)
    {
        if (_bufferX.Count == 0)
        {
            // Nothing to correct yet; no current bar exists
            return;
        }

        // Get the current newest values (which are wrong and need to be corrected)
        double oldX = _bufferX.Newest;
        double oldY = _bufferY.Newest;

        // Kahan subtract old + add new for _sumX
        {
            double yk = (-oldX + x) - _sumXComp;
            double t = _sumX + yk;
            _sumXComp = (t - _sumX) - yk;
            _sumX = t;
        }
        // Kahan subtract old + add new for _sumY
        {
            double yk = (-oldY + y) - _sumYComp;
            double t = _sumY + yk;
            _sumYComp = (t - _sumY) - yk;
            _sumY = t;
        }
        // Kahan subtract old² + add new² for _sumX2
        {
            double yk = (-(oldX * oldX) + (x * x)) - _sumX2Comp;
            double t = _sumX2 + yk;
            _sumX2Comp = (t - _sumX2) - yk;
            _sumX2 = t;
        }
        // Kahan subtract old² + add new² for _sumY2
        {
            double yk = (-(oldY * oldY) + (y * y)) - _sumY2Comp;
            double t = _sumY2 + yk;
            _sumY2Comp = (t - _sumY2) - yk;
            _sumY2 = t;
        }
        // Kahan subtract old*old + add new*new for _sumXY
        {
            double yk = (-(oldX * oldY) + (x * y)) - _sumXYComp;
            double t = _sumXY + yk;
            _sumXYComp = (t - _sumXY) - yk;
            _sumXY = t;
        }

        // Update the buffer values
        _bufferX.UpdateNewest(x);
        _bufferY.UpdateNewest(y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateCorrel()
        => CorrelFromSums(_sumX, _sumY, _sumX2, _sumY2, _sumXY, _bufferX.Count);

    /// <summary>
    /// Computes the Pearson correlation coefficient from running window sums.
    /// Shared by the streaming and batch (scalar + SIMD) paths so they cannot drift apart.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CorrelFromSums(double sumX, double sumY, double sumX2, double sumY2, double sumXY, int n)
    {
        if (n < 2)
        {
            return double.NaN;
        }

        // Calculate means
        double meanX = sumX / n;
        double meanY = sumY / n;

        // Calculate variances (population variance)
        double varX = Max(0.0, (sumX2 / n) - (meanX * meanX));
        double varY = Max(0.0, (sumY2 / n) - (meanY * meanY));

        // Calculate covariance
        double cov = (sumXY / n) - (meanX * meanY);

        // Calculate standard deviations and correlation
        double stdX = Sqrt(varX);
        double stdY = Sqrt(varY);
        double denominator = stdX * stdY;
        if (Abs(denominator) < Epsilon)
        {
            return double.NaN;
        }

        // Clamp to [-1, 1] range to handle floating point precision issues
        return Max(-1.0, Min(1.0, cov / denominator));
    }

    /// <summary>Not supported. This indicator requires two input spans.</summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("Correl requires two inputs.");
    }

    /// <inheritdoc />
    public override void Reset()
    {
        _bufferX.Clear();
        _bufferY.Clear();

        _sumX = 0;
        _sumY = 0;
        _sumX2 = 0;
        _sumY2 = 0;
        _sumXY = 0;

        _sumXComp = 0;
        _sumYComp = 0;
        _sumX2Comp = 0;
        _sumY2Comp = 0;
        _sumXYComp = 0;

        _lastValidX = 0;
        _lastValidY = 0;
        _p_lastValidX = 0;
        _p_lastValidY = 0;

        Last = default;
    }

    /// <summary>
    /// Calculates correlation for two time series.
    /// </summary>
    public static TSeries Batch(TSeries seriesX, TSeries seriesY, int period = 20)
        => Calculate(seriesX, seriesY, period).Results;

    /// <summary>
    /// Static batch calculation for span-based processing.
    /// Uses SIMD (AVX-512/AVX2) for large, finite inputs and a Kahan-compensated
    /// scalar fallback (with NaN sanitization) otherwise.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> seriesX,
        ReadOnlySpan<double> seriesY,
        Span<double> output,
        int period = 20)
    {
        if (seriesX.Length != seriesY.Length)
        {
            throw new ArgumentException("Series must have the same length", nameof(seriesY));
        }

        if (seriesX.Length != output.Length)
        {
            throw new ArgumentException("Output must have the same length as input", nameof(output));
        }

        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        int len = seriesX.Length;
        if (len == 0)
        {
            return;
        }

#if NET5_0_OR_GREATER
        const int SimdThreshold = 256;
        if (len >= SimdThreshold && !seriesX.ContainsNonFinite() && !seriesY.ContainsNonFinite())
        {
            if (Avx512F.IsSupported)
            {
                CalculateAvx512Core(seriesX, seriesY, output, period);
                return;
            }

            if (Avx2.IsSupported)
            {
                CalculateAvx2Core(seriesX, seriesY, output, period);
                return;
            }
        }
#endif

        CalculateScalarCore(seriesX, seriesY, output, period);
    }

    /// <summary>
    /// Kahan-compensated scalar batch path. Replicates the streaming semantics
    /// (last-valid-value NaN sanitization, growing warmup window) without the
    /// per-tick RingBuffer/event/DateTime overhead.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> seriesX, ReadOnlySpan<double> seriesY, Span<double> output, int period)
    {
        int len = seriesX.Length;

        const int StackAllocThreshold = 256;
        double[]? rented = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period * 2) : null;
        Span<double> bufX = rented is not null ? rented.AsSpan(0, period) : stackalloc double[period];
        Span<double> bufY = rented is not null ? rented.AsSpan(period, period) : stackalloc double[period];

        double sumX = 0, sumY = 0, sumX2 = 0, sumY2 = 0, sumXY = 0;
        double cX = 0, cY = 0, cX2 = 0, cY2 = 0, cXY = 0;
        double lastValidX = 0, lastValidY = 0;
        int count = 0;
        int bufIdx = 0;

        try
        {
            for (int i = 0; i < len; i++)
            {
                double x = seriesX[i];
                if (double.IsFinite(x))
                {
                    lastValidX = x;
                }
                else
                {
                    x = lastValidX;
                }

                double y = seriesY[i];
                if (double.IsFinite(y))
                {
                    lastValidY = y;
                }
                else
                {
                    y = lastValidY;
                }

                if (count == period)
                {
                    double oldX = bufX[bufIdx];
                    double oldY = bufY[bufIdx];

                    // Kahan subtract the leaving values from the running sums
                    double yk = -oldX - cX; double t = sumX + yk; cX = (t - sumX) - yk; sumX = t;
                    yk = -oldY - cY; t = sumY + yk; cY = (t - sumY) - yk; sumY = t;
                    yk = -(oldX * oldX) - cX2; t = sumX2 + yk; cX2 = (t - sumX2) - yk; sumX2 = t;
                    yk = -(oldY * oldY) - cY2; t = sumY2 + yk; cY2 = (t - sumY2) - yk; sumY2 = t;
                    yk = -(oldX * oldY) - cXY; t = sumXY + yk; cXY = (t - sumXY) - yk; sumXY = t;
                }

                bufX[bufIdx] = x;
                bufY[bufIdx] = y;
                bufIdx++;
                if (bufIdx >= period)
                {
                    bufIdx = 0;
                }

                // Kahan add the new values to the running sums
                double ak = x - cX; double at = sumX + ak; cX = (at - sumX) - ak; sumX = at;
                ak = y - cY; at = sumY + ak; cY = (at - sumY) - ak; sumY = at;
                ak = (x * x) - cX2; at = sumX2 + ak; cX2 = (at - sumX2) - ak; sumX2 = at;
                ak = (y * y) - cY2; at = sumY2 + ak; cY2 = (at - sumY2) - ak; sumY2 = at;
                ak = (x * y) - cXY; at = sumXY + ak; cXY = (at - sumXY) - ak; sumXY = at;

                if (count < period)
                {
                    count++;
                }

                output[i] = CorrelFromSums(sumX, sumY, sumX2, sumY2, sumXY, count);
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

#if NET5_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<double> PrefixSum512(Vector512<double> v)
    {
        var s1 = Vector512.Create(0.0, v.GetElement(0), v.GetElement(1), v.GetElement(2), v.GetElement(3), v.GetElement(4), v.GetElement(5), v.GetElement(6));
        var p1 = Avx512F.Add(v, s1);
        var s2 = Vector512.Create(0.0, 0.0, p1.GetElement(0), p1.GetElement(1), p1.GetElement(2), p1.GetElement(3), p1.GetElement(4), p1.GetElement(5));
        var p2 = Avx512F.Add(p1, s2);
        var s4 = Vector512.Create(0.0, 0.0, 0.0, 0.0, p2.GetElement(0), p2.GetElement(1), p2.GetElement(2), p2.GetElement(3));
        return Avx512F.Add(p2, s4);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateAvx512Core(ReadOnlySpan<double> seriesX, ReadOnlySpan<double> seriesY, Span<double> output, int period)
    {
        int len = seriesX.Length;
        const int VectorWidth = 8;

        ref double xRef = ref MemoryMarshal.GetReference(seriesX);
        ref double yRef = ref MemoryMarshal.GetReference(seriesY);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        double invPeriod = 1.0 / period;

        double sumX = 0, sumY = 0, sumX2 = 0, sumY2 = 0, sumXY = 0;
        int warmupEnd = Min(period, len);
        for (int i = 0; i < warmupEnd; i++)
        {
            double x = Unsafe.Add(ref xRef, i);
            double y = Unsafe.Add(ref yRef, i);
            sumX += x;
            sumY += y;
            sumX2 += x * x;
            sumY2 += y * y;
            sumXY += x * y;
            Unsafe.Add(ref outRef, i) = CorrelFromSums(sumX, sumY, sumX2, sumY2, sumXY, i + 1);
        }

        if (len <= period)
        {
            return;
        }

        var vInvPeriod = Vector512.Create(invPeriod);
        var vZero = Vector512<double>.Zero;
        var vOne = Vector512.Create(1.0);
        var vMinusOne = Vector512.Create(-1.0);
        var vEpsilon = Vector512.Create(Epsilon);
        var vNan = Vector512.Create(double.NaN);

        int simdEnd = period + ((len - period) / VectorWidth * VectorWidth);

        for (int i = period; i < simdEnd; i += VectorWidth)
        {
            var vNewX = Vector512.LoadUnsafe(ref Unsafe.Add(ref xRef, i));
            var vOldX = Vector512.LoadUnsafe(ref Unsafe.Add(ref xRef, i - period));
            var vNewY = Vector512.LoadUnsafe(ref Unsafe.Add(ref yRef, i));
            var vOldY = Vector512.LoadUnsafe(ref Unsafe.Add(ref yRef, i - period));

            var vSumX = Avx512F.Add(PrefixSum512(Avx512F.Subtract(vNewX, vOldX)), Vector512.Create(sumX));
            var vSumY = Avx512F.Add(PrefixSum512(Avx512F.Subtract(vNewY, vOldY)), Vector512.Create(sumY));
            var vSumX2 = Avx512F.Add(PrefixSum512(Avx512F.Subtract(Avx512F.Multiply(vNewX, vNewX), Avx512F.Multiply(vOldX, vOldX))), Vector512.Create(sumX2));
            var vSumY2 = Avx512F.Add(PrefixSum512(Avx512F.Subtract(Avx512F.Multiply(vNewY, vNewY), Avx512F.Multiply(vOldY, vOldY))), Vector512.Create(sumY2));
            var vSumXY = Avx512F.Add(PrefixSum512(Avx512F.Subtract(Avx512F.Multiply(vNewX, vNewY), Avx512F.Multiply(vOldX, vOldY))), Vector512.Create(sumXY));

            sumX = vSumX.GetElement(VectorWidth - 1);
            sumY = vSumY.GetElement(VectorWidth - 1);
            sumX2 = vSumX2.GetElement(VectorWidth - 1);
            sumY2 = vSumY2.GetElement(VectorWidth - 1);
            sumXY = vSumXY.GetElement(VectorWidth - 1);

            var vMeanX = Avx512F.Multiply(vSumX, vInvPeriod);
            var vMeanY = Avx512F.Multiply(vSumY, vInvPeriod);
            var vVarX = Avx512F.Max(vZero, Avx512F.FusedMultiplySubtract(vSumX2, vInvPeriod, Avx512F.Multiply(vMeanX, vMeanX)));
            var vVarY = Avx512F.Max(vZero, Avx512F.FusedMultiplySubtract(vSumY2, vInvPeriod, Avx512F.Multiply(vMeanY, vMeanY)));
            var vCov = Avx512F.FusedMultiplySubtract(vSumXY, vInvPeriod, Avx512F.Multiply(vMeanX, vMeanY));
            var vDenom = Avx512F.Sqrt(Avx512F.Multiply(vVarX, vVarY));

            var vR = Avx512F.Divide(vCov, vDenom);
            vR = Avx512F.Min(vOne, Avx512F.Max(vMinusOne, vR));

            var vValid = Avx512F.Compare(vDenom, vEpsilon, FloatComparisonMode.OrderedGreaterThanOrEqualNonSignaling);
            Avx512F.BlendVariable(vR, vNan, vValid).StoreUnsafe(ref Unsafe.Add(ref outRef, i));
        }

        for (int i = simdEnd; i < len; i++)
        {
            double x = Unsafe.Add(ref xRef, i);
            double y = Unsafe.Add(ref yRef, i);
            double oldX = Unsafe.Add(ref xRef, i - period);
            double oldY = Unsafe.Add(ref yRef, i - period);
            sumX += x - oldX;
            sumY += y - oldY;
            sumX2 += (x * x) - (oldX * oldX);
            sumY2 += (y * y) - (oldY * oldY);
            sumXY += (x * y) - (oldX * oldY);
            Unsafe.Add(ref outRef, i) = CorrelFromSums(sumX, sumY, sumX2, sumY2, sumXY, period);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> PrefixSum256(Vector256<double> v)
    {
        var zero = Vector256<double>.Zero;
        var s1 = Avx2.Permute4x64(v.AsUInt64(), 0b_10_01_00_00).AsDouble();
        s1 = Avx.Blend(zero, s1, 0b_1110);
        var p1 = Avx.Add(v, s1);
        var s2 = Avx2.Permute4x64(p1.AsUInt64(), 0b_01_00_00_00).AsDouble();
        s2 = Avx.Blend(zero, s2, 0b_1100);
        return Avx.Add(p1, s2);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateAvx2Core(ReadOnlySpan<double> seriesX, ReadOnlySpan<double> seriesY, Span<double> output, int period)
    {
        int len = seriesX.Length;
        const int VectorWidth = 4;

        ref double xRef = ref MemoryMarshal.GetReference(seriesX);
        ref double yRef = ref MemoryMarshal.GetReference(seriesY);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        double invPeriod = 1.0 / period;

        double sumX = 0, sumY = 0, sumX2 = 0, sumY2 = 0, sumXY = 0;
        int warmupEnd = Min(period, len);
        for (int i = 0; i < warmupEnd; i++)
        {
            double x = Unsafe.Add(ref xRef, i);
            double y = Unsafe.Add(ref yRef, i);
            sumX += x;
            sumY += y;
            sumX2 += x * x;
            sumY2 += y * y;
            sumXY += x * y;
            Unsafe.Add(ref outRef, i) = CorrelFromSums(sumX, sumY, sumX2, sumY2, sumXY, i + 1);
        }

        if (len <= period)
        {
            return;
        }

        var vInvPeriod = Vector256.Create(invPeriod);
        var vZero = Vector256<double>.Zero;
        var vOne = Vector256.Create(1.0);
        var vMinusOne = Vector256.Create(-1.0);
        var vEpsilon = Vector256.Create(Epsilon);
        var vNan = Vector256.Create(double.NaN);

        int simdEnd = period + ((len - period) / VectorWidth * VectorWidth);

        for (int i = period; i < simdEnd; i += VectorWidth)
        {
            var vNewX = Vector256.LoadUnsafe(ref Unsafe.Add(ref xRef, i));
            var vOldX = Vector256.LoadUnsafe(ref Unsafe.Add(ref xRef, i - period));
            var vNewY = Vector256.LoadUnsafe(ref Unsafe.Add(ref yRef, i));
            var vOldY = Vector256.LoadUnsafe(ref Unsafe.Add(ref yRef, i - period));

            var vSumX = Avx.Add(PrefixSum256(Avx.Subtract(vNewX, vOldX)), Vector256.Create(sumX));
            var vSumY = Avx.Add(PrefixSum256(Avx.Subtract(vNewY, vOldY)), Vector256.Create(sumY));
            var vSumX2 = Avx.Add(PrefixSum256(Avx.Subtract(Avx.Multiply(vNewX, vNewX), Avx.Multiply(vOldX, vOldX))), Vector256.Create(sumX2));
            var vSumY2 = Avx.Add(PrefixSum256(Avx.Subtract(Avx.Multiply(vNewY, vNewY), Avx.Multiply(vOldY, vOldY))), Vector256.Create(sumY2));
            var vSumXY = Avx.Add(PrefixSum256(Avx.Subtract(Avx.Multiply(vNewX, vNewY), Avx.Multiply(vOldX, vOldY))), Vector256.Create(sumXY));

            sumX = vSumX.GetElement(VectorWidth - 1);
            sumY = vSumY.GetElement(VectorWidth - 1);
            sumX2 = vSumX2.GetElement(VectorWidth - 1);
            sumY2 = vSumY2.GetElement(VectorWidth - 1);
            sumXY = vSumXY.GetElement(VectorWidth - 1);

            var vMeanX = Avx.Multiply(vSumX, vInvPeriod);
            var vMeanY = Avx.Multiply(vSumY, vInvPeriod);
            var vVarX = Avx.Max(vZero, Avx.Subtract(Avx.Multiply(vSumX2, vInvPeriod), Avx.Multiply(vMeanX, vMeanX)));
            var vVarY = Avx.Max(vZero, Avx.Subtract(Avx.Multiply(vSumY2, vInvPeriod), Avx.Multiply(vMeanY, vMeanY)));
            var vCov = Avx.Subtract(Avx.Multiply(vSumXY, vInvPeriod), Avx.Multiply(vMeanX, vMeanY));
            var vDenom = Avx.Sqrt(Avx.Multiply(vVarX, vVarY));

            var vR = Avx.Divide(vCov, vDenom);
            vR = Avx.Min(vOne, Avx.Max(vMinusOne, vR));

            var vValid = Avx.Compare(vDenom, vEpsilon, FloatComparisonMode.OrderedGreaterThanOrEqualNonSignaling);
            Avx.BlendVariable(vR, vNan, vValid).StoreUnsafe(ref Unsafe.Add(ref outRef, i));
        }

        for (int i = simdEnd; i < len; i++)
        {
            double x = Unsafe.Add(ref xRef, i);
            double y = Unsafe.Add(ref yRef, i);
            double oldX = Unsafe.Add(ref xRef, i - period);
            double oldY = Unsafe.Add(ref yRef, i - period);
            sumX += x - oldX;
            sumY += y - oldY;
            sumX2 += (x * x) - (oldX * oldX);
            sumY2 += (y * y) - (oldY * oldY);
            sumXY += (x * y) - (oldX * oldY);
            Unsafe.Add(ref outRef, i) = CorrelFromSums(sumX, sumY, sumX2, sumY2, sumXY, period);
        }
    }
#endif

    /// <summary>
    /// Calculates Pearson correlation for two time series and returns both the result series and the live indicator instance.
    /// </summary>
    public static (TSeries Results, Correl Indicator) Calculate(TSeries seriesX, TSeries seriesY, int period = 20)
    {
        if (seriesX.Count != seriesY.Count)
        {
            throw new ArgumentException("Series must have the same length", nameof(seriesY));
        }

        var indicator = new Correl(period);
        var result = new TSeries(seriesX.Count);

        var timesX = seriesX.Times;
        var valuesX = seriesX.Values;
        var valuesY = seriesY.Values;

        for (int i = 0; i < seriesX.Count; i++)
        {
            result.Add(indicator.Update(new TValue(timesX[i], valuesX[i]), new TValue(timesX[i], valuesY[i]), isNew: true));
        }

        return (result, indicator);
    }
}
