using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// SIMD-accelerated error computation helpers for error indicators.
/// Provides shared methods for computing absolute errors, squared errors,
/// and percentage errors with automatic SIMD/scalar fallback.
/// </summary>
public static class ErrorHelpers
{
    private const string SpanLengthMismatchMessage = "All spans must have the same length";

    /// <summary>
    /// Computes signed errors: actual - predicted (preserves sign for bias detection)
    /// Uses AVX2 SIMD when available for clean data, with scalar fallback for NaN handling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeSignedErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));

        int len = actual.Length;
        if (len == 0)
            return;

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);

        // Try SIMD path - NaN detection is integrated into the SIMD loop
        if (Avx2.IsSupported && len >= Vector256<double>.Count)
        {
            int processedCount = ComputeSignedErrorsSimdWithNaNDetection(actual, predicted, output, lastValidActual, lastValidPredicted);
            if (processedCount == len)
                return; // All processed via SIMD
            // Continue with scalar for remaining elements (NaN was detected)
            ComputeSignedErrorsScalar(actual.Slice(processedCount), predicted.Slice(processedCount), output.Slice(processedCount), lastValidActual, lastValidPredicted);
            return;
        }

        // Scalar fallback with NaN handling
        ComputeSignedErrorsScalar(actual, predicted, output, lastValidActual, lastValidPredicted);
    }

    /// <summary>
    /// Computes absolute errors: |actual - predicted|
    /// Uses AVX2 SIMD when available for clean data, with scalar fallback for NaN handling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeAbsoluteErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));

        int len = actual.Length;
        if (len == 0)
            return;

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);

        // Try SIMD path for clean data (no NaN/Inf)
        if (Avx2.IsSupported && len >= Vector256<double>.Count && IsDataClean(actual, predicted))
        {
            ComputeAbsoluteErrorsSimd(actual, predicted, output);
            return;
        }

        // Scalar fallback with NaN handling
        ComputeAbsoluteErrorsScalar(actual, predicted, output, lastValidActual, lastValidPredicted);
    }

    /// <summary>
    /// Computes squared errors: (actual - predicted)²
    /// Uses AVX2 SIMD when available for clean data, with scalar fallback for NaN handling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeSquaredErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));

        int len = actual.Length;
        if (len == 0)
            return;

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);

        // Try SIMD path for clean data (no NaN/Inf)
        if (Avx2.IsSupported && len >= Vector256<double>.Count && IsDataClean(actual, predicted))
        {
            ComputeSquaredErrorsSimd(actual, predicted, output);
            return;
        }

        // Scalar fallback with NaN handling
        ComputeSquaredErrorsScalar(actual, predicted, output, lastValidActual, lastValidPredicted);
    }

    /// <summary>
    /// Computes weighted errors: weight * |actual - predicted|
    /// Used by WRMSE and other weighted error indicators.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeWeightedErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        ReadOnlySpan<double> weights,
        Span<double> output)
    {
        if (actual.Length != predicted.Length || actual.Length != weights.Length || actual.Length != output.Length)
            throw new ArgumentException("All spans must have the same length", nameof(output));

        int len = actual.Length;
        if (len == 0)
            return;

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);
        double lastValidWeight = FindFirstValidValue(weights);

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;
        double currentValidWeight = lastValidWeight;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];
            double wgt = weights[i];

            if (double.IsFinite(act)) currentValidActual = act; else act = currentValidActual;
            if (double.IsFinite(pred)) currentValidPredicted = pred; else pred = currentValidPredicted;
            if (double.IsFinite(wgt)) currentValidWeight = wgt; else wgt = currentValidWeight;

            double diff = act - pred;
            output[i] = wgt * diff * diff;
        }
    }

    /// <summary>
    /// Computes percentage errors: |actual - predicted| / |actual| * 100
    /// Uses scalar path with NaN handling (percentage errors require division guards).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputePercentageErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double epsilon = 1e-10)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));

        int len = actual.Length;
        if (len == 0)
            return;

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) currentValidActual = act; else act = currentValidActual;
            if (double.IsFinite(pred)) currentValidPredicted = pred; else pred = currentValidPredicted;

            double absActual = Math.Abs(act);
            if (absActual < epsilon)
            {
                // Avoid division by zero - use absolute error as fallback
                output[i] = Math.Abs(act - pred);
            }
            else
            {
                output[i] = Math.Abs(act - pred) / absActual * 100.0;
            }
        }
    }

    /// <summary>
    /// Computes symmetric percentage errors: |actual - predicted| / ((|actual| + |predicted|) / 2) * 100
    /// Used by SMAPE indicator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeSymmetricPercentageErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double epsilon = 1e-10)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));

        int len = actual.Length;
        if (len == 0)
            return;

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) currentValidActual = act; else act = currentValidActual;
            if (double.IsFinite(pred)) currentValidPredicted = pred; else pred = currentValidPredicted;

            double denominator = (Math.Abs(act) + Math.Abs(pred)) / 2.0;
            if (denominator < epsilon)
            {
                output[i] = 0.0; // Both values near zero
            }
            else
            {
                output[i] = Math.Abs(act - pred) / denominator * 100.0;
            }
        }
    }

    /// <summary>
    /// Computes log-cosh errors: log(cosh(actual - predicted))
    /// Smoother alternative to squared errors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeLogCoshErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));

        int len = actual.Length;
        if (len == 0)
            return;

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) currentValidActual = act; else act = currentValidActual;
            if (double.IsFinite(pred)) currentValidPredicted = pred; else pred = currentValidPredicted;

            double diff = act - pred;
            // log(cosh(x)) ≈ |x| - log(2) for large |x|, numerically stable
            output[i] = LogCosh(diff);
        }
    }

    /// <summary>
    /// Computes Pseudo-Huber errors: δ² * (√(1 + (error/δ)²) - 1)
    /// Smooth approximation to Huber loss, also known as Charbonnier loss.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputePseudoHuberErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double delta = 1.0)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));

        int len = actual.Length;
        if (len == 0)
            return;

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);
        double deltaSquared = delta * delta;

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) currentValidActual = act; else act = currentValidActual;
            if (double.IsFinite(pred)) currentValidPredicted = pred; else pred = currentValidPredicted;

            double diff = act - pred;
            double ratio = diff / delta;
            // δ² * (√(1 + (error/δ)²) - 1)
            output[i] = deltaSquared * (Math.Sqrt(1.0 + ratio * ratio) - 1.0);
        }
    }

    /// <summary>
    /// Computes Tukey's Biweight (Bisquare) errors:
    /// ρ(x) = (c²/6) * (1 - (1 - (x/c)²)³)  for |x| ≤ c
    /// ρ(x) = c²/6                           for |x| > c
    /// Redescending M-estimator that completely rejects outliers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeTukeyBiweightErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double c = 4.685)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));

        int len = actual.Length;
        if (len == 0)
            return;

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);
        double cSquaredOver6 = (c * c) / 6.0;

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) currentValidActual = act; else act = currentValidActual;
            if (double.IsFinite(pred)) currentValidPredicted = pred; else pred = currentValidPredicted;

            double diff = act - pred;
            double absDiff = Math.Abs(diff);

            if (absDiff > c)
            {
                output[i] = cSquaredOver6;
            }
            else
            {
                double ratio = diff / c;
                double ratioSq = ratio * ratio;
                double oneMinusRatioSq = 1.0 - ratioSq;
                double cubed = oneMinusRatioSq * oneMinusRatioSq * oneMinusRatioSq;
                output[i] = cSquaredOver6 * (1.0 - cubed);
            }
        }
    }

    /// <summary>
    /// Computes Huber errors: 0.5*x² for |x| ≤ δ, δ*(|x| - 0.5*δ) otherwise
    /// Robust to outliers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeHuberErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double delta = 1.0)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));

        int len = actual.Length;
        if (len == 0)
            return;

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);
        double halfDelta = 0.5 * delta;

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) currentValidActual = act; else act = currentValidActual;
            if (double.IsFinite(pred)) currentValidPredicted = pred; else pred = currentValidPredicted;

            double diff = act - pred;
            double absDiff = Math.Abs(diff);

            if (absDiff <= delta)
            {
                output[i] = 0.5 * diff * diff;
            }
            else
            {
                output[i] = delta * (absDiff - halfDelta);
            }
        }
    }

    /// <summary>
    /// Applies rolling window mean to pre-computed errors.
    /// Uses O(1) running sum with periodic resync for floating-point drift correction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyRollingMean(
        ReadOnlySpan<double> errors,
        Span<double> output,
        int period,
        int resyncInterval = 1000)
    {
        if (errors.Length != output.Length)
            throw new ArgumentException("Spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = errors.Length;
        if (len == 0)
            return;

        const int StackAllocThreshold = 256;
        double[]? rented = null;

#pragma warning disable S1121 // Assignments should not be made from within sub-expressions
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : (rented = ArrayPool<double>.Shared.Rent(period)).AsSpan(0, period);
#pragma warning restore S1121

        try
        {
            double sum = 0;
            int bufferIndex = 0;

            // Warmup phase
            int warmupEnd = Math.Min(period, len);
            for (int i = 0; i < warmupEnd; i++)
            {
                sum += errors[i];
                buffer[i] = errors[i];
                output[i] = sum / (i + 1);
            }

            // Main loop with O(1) update
            int tickCount = 0;
            for (int i = warmupEnd; i < len; i++)
            {
                double error = errors[i];
                sum = sum - buffer[bufferIndex] + error;
                buffer[bufferIndex] = error;

                bufferIndex++;
                if (bufferIndex >= period) bufferIndex = 0;

                output[i] = sum / period;

                tickCount++;
                if (tickCount >= resyncInterval)
                {
                    tickCount = 0;
                    double recalcSum = 0;
                    for (int k = 0; k < period; k++) recalcSum += buffer[k];
                    sum = recalcSum;
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<double>.Shared.Return(rented, clearArray: false);
            }
        }
    }

    /// <summary>
    /// Applies rolling window mean with square root of result (for RMSE-style indicators).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyRollingMeanSqrt(
        ReadOnlySpan<double> squaredErrors,
        Span<double> output,
        int period,
        int resyncInterval = 1000)
    {
        if (squaredErrors.Length != output.Length)
            throw new ArgumentException("Spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = squaredErrors.Length;
        if (len == 0)
            return;

        const int StackAllocThreshold = 256;
        double[]? rented = null;

#pragma warning disable S1121 // Assignments should not be made from within sub-expressions
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : (rented = ArrayPool<double>.Shared.Rent(period)).AsSpan(0, period);
#pragma warning restore S1121

        try
        {
            double sum = 0;
            int bufferIndex = 0;

            // Warmup phase
            int warmupEnd = Math.Min(period, len);
            for (int i = 0; i < warmupEnd; i++)
            {
                sum += squaredErrors[i];
                buffer[i] = squaredErrors[i];
                output[i] = Math.Sqrt(sum / (i + 1));
            }

            // Main loop with O(1) update
            int tickCount = 0;
            for (int i = warmupEnd; i < len; i++)
            {
                double sqError = squaredErrors[i];
                sum = sum - buffer[bufferIndex] + sqError;
                buffer[bufferIndex] = sqError;

                bufferIndex++;
                if (bufferIndex >= period) bufferIndex = 0;

                output[i] = Math.Sqrt(sum / period);

                tickCount++;
                if (tickCount >= resyncInterval)
                {
                    tickCount = 0;
                    double recalcSum = 0;
                    for (int k = 0; k < period; k++) recalcSum += buffer[k];
                    sum = recalcSum;
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<double>.Shared.Return(rented, clearArray: false);
            }
        }
    }

    /// <summary>
    /// Applies rolling window weighted mean with square root of result (for WRMSE-style indicators).
    /// Computes sqrt(sum(weighted_errors) / sum(weights)) over a rolling window.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyRollingWeightedMeanSqrt(
        ReadOnlySpan<double> weightedSquaredErrors,
        ReadOnlySpan<double> weights,
        Span<double> output,
        int period,
        int resyncInterval = 1000)
    {
        if (weightedSquaredErrors.Length != output.Length || weightedSquaredErrors.Length != weights.Length)
            throw new ArgumentException("Spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = weightedSquaredErrors.Length;
        if (len == 0)
            return;

        const int StackAllocThreshold = 256;
        double[]? rentedErrors = null;
        double[]? rentedWeights = null;

#pragma warning disable S1121 // Assignments should not be made from within sub-expressions
        Span<double> errorBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : (rentedErrors = ArrayPool<double>.Shared.Rent(period)).AsSpan(0, period);

        Span<double> weightBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : (rentedWeights = ArrayPool<double>.Shared.Rent(period)).AsSpan(0, period);
#pragma warning restore S1121

        try
        {
            double sumErrors = 0;
            double sumWeights = 0;
            int bufferIndex = 0;

            // Warmup phase
            int warmupEnd = Math.Min(period, len);
            for (int i = 0; i < warmupEnd; i++)
            {
                sumErrors += weightedSquaredErrors[i];
                sumWeights += weights[i];
                errorBuffer[i] = weightedSquaredErrors[i];
                weightBuffer[i] = weights[i];
                output[i] = sumWeights > 1e-10 ? Math.Sqrt(sumErrors / sumWeights) : 0.0;
            }

            // Main loop with O(1) update
            int tickCount = 0;
            for (int i = warmupEnd; i < len; i++)
            {
                double wse = weightedSquaredErrors[i];
                double wgt = weights[i];

                sumErrors = sumErrors - errorBuffer[bufferIndex] + wse;
                sumWeights = sumWeights - weightBuffer[bufferIndex] + wgt;
                errorBuffer[bufferIndex] = wse;
                weightBuffer[bufferIndex] = wgt;

                bufferIndex++;
                if (bufferIndex >= period) bufferIndex = 0;

                output[i] = sumWeights > 1e-10 ? Math.Sqrt(sumErrors / sumWeights) : 0.0;

                tickCount++;
                if (tickCount >= resyncInterval)
                {
                    tickCount = 0;
                    double recalcSumErrors = 0;
                    double recalcSumWeights = 0;
                    for (int k = 0; k < period; k++)
                    {
                        recalcSumErrors += errorBuffer[k];
                        recalcSumWeights += weightBuffer[k];
                    }
                    sumErrors = recalcSumErrors;
                    sumWeights = recalcSumWeights;
                }
            }
        }
        finally
        {
            if (rentedErrors is not null)
            {
                ArrayPool<double>.Shared.Return(rentedErrors, clearArray: false);
            }
            if (rentedWeights is not null)
            {
                ArrayPool<double>.Shared.Return(rentedWeights, clearArray: false);
            }
        }
    }

    #region Private Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double FindFirstValidValue(ReadOnlySpan<double> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (double.IsFinite(span[i]))
                return span[i];
        }
        return 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDataClean(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted)
    {
        int len = actual.Length;

        // SIMD path for AVX-supported systems
        if (Avx.IsSupported && len >= Vector256<double>.Count)
        {
            int vectorSize = Vector256<double>.Count;
            int vectorEnd = len - (len % vectorSize);

            for (int i = 0; i < vectorEnd; i += vectorSize)
            {
                Vector256<double> actVec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(actual.Slice(i)));
                Vector256<double> predVec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(predicted.Slice(i)));

                // NaN check: x == x is false for NaN
                // Compare each vector with itself - OrderedQ returns all-ones for finite, zero for NaN
                Vector256<double> actCmp = Avx.Compare(actVec, actVec, FloatComparisonMode.OrderedNonSignaling);
                Vector256<double> predCmp = Avx.Compare(predVec, predVec, FloatComparisonMode.OrderedNonSignaling);

                // Combine: both must be all-ones (finite)
                Vector256<double> combined = Avx.And(actCmp, predCmp);

                // MoveMask returns a bitmask; all-ones means all finite (mask == 0b1111 for 4 doubles)
                int mask = Avx.MoveMask(combined);
                if (mask != 0b1111)
                    return false;
            }

            // Scalar tail
            for (int i = vectorEnd; i < len; i++)
            {
                if (!double.IsFinite(actual[i]) || !double.IsFinite(predicted[i]))
                    return false;
            }
            return true;
        }

        // Scalar fallback
        for (int i = 0; i < len; i++)
        {
            if (!double.IsFinite(actual[i]) || !double.IsFinite(predicted[i]))
                return false;
        }
        return true;
    }

    /// <summary>
    /// SIMD path with integrated NaN detection. Returns the number of elements processed.
    /// If NaN is detected, returns the index where NaN was found so caller can continue with scalar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeSignedErrorsSimdWithNaNDetection(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted)
    {
        int len = actual.Length;
        int vectorSize = Vector256<double>.Count;
        int vectorEnd = len - (len % vectorSize);

        int i = 0;
        for (; i < vectorEnd; i += vectorSize)
        {
            Vector256<double> actVec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(actual.Slice(i)));
            Vector256<double> predVec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(predicted.Slice(i)));

            // Check for NaN/Inf: x == x is false for NaN
            Vector256<double> actCmp = Avx.Compare(actVec, actVec, FloatComparisonMode.OrderedNonSignaling);
            Vector256<double> predCmp = Avx.Compare(predVec, predVec, FloatComparisonMode.OrderedNonSignaling);
            Vector256<double> combined = Avx.And(actCmp, predCmp);

            int mask = Avx.MoveMask(combined);
            if (mask != 0b1111)
            {
                // NaN detected - return current position for scalar fallback
                return i;
            }

            // No NaN - compute error
            Vector256<double> errorVec = Avx.Subtract(actVec, predVec);
            errorVec.StoreUnsafe(ref MemoryMarshal.GetReference(output.Slice(i)));
        }

        // Handle scalar remainder
        for (; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (!double.IsFinite(act) || !double.IsFinite(pred))
            {
                // Return current position - caller will handle with scalar fallback
                return i;
            }

            output[i] = act - pred;
        }

        return len;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeSignedErrorsSimd(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output)
    {
        int len = actual.Length;
        int vectorSize = Vector256<double>.Count;
        int vectorEnd = len - (len % vectorSize);

        int i = 0;
        for (; i < vectorEnd; i += vectorSize)
        {
            Vector256<double> actVec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(actual.Slice(i)));
            Vector256<double> predVec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(predicted.Slice(i)));

            // error = actual - predicted (preserves sign)
            Vector256<double> errorVec = Avx.Subtract(actVec, predVec);

            errorVec.StoreUnsafe(ref MemoryMarshal.GetReference(output.Slice(i)));
        }

        // Handle remainder with scalar
        for (; i < len; i++)
        {
            output[i] = actual[i] - predicted[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeSignedErrorsScalar(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted)
    {
        int len = actual.Length;
        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) currentValidActual = act; else act = currentValidActual;
            if (double.IsFinite(pred)) currentValidPredicted = pred; else pred = currentValidPredicted;

            output[i] = act - pred;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeAbsoluteErrorsSimd(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output)
    {
        int len = actual.Length;
        int vectorSize = Vector256<double>.Count;
        int vectorEnd = len - (len % vectorSize);

        // Create mask for absolute value (clear sign bit)
        Vector256<double> absMask = Vector256.Create(~(1L << 63)).AsDouble();

        int i = 0;
        for (; i < vectorEnd; i += vectorSize)
        {
            Vector256<double> actVec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(actual.Slice(i)));
            Vector256<double> predVec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(predicted.Slice(i)));

            // error = actual - predicted
            Vector256<double> errorVec = Avx.Subtract(actVec, predVec);

            // absError = |error| (clear sign bit)
            Vector256<double> absErrorVec = Avx.And(errorVec, absMask);

            absErrorVec.StoreUnsafe(ref MemoryMarshal.GetReference(output.Slice(i)));
        }

        // Handle remainder with scalar
        for (; i < len; i++)
        {
            output[i] = Math.Abs(actual[i] - predicted[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeAbsoluteErrorsScalar(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted)
    {
        int len = actual.Length;
        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) currentValidActual = act; else act = currentValidActual;
            if (double.IsFinite(pred)) currentValidPredicted = pred; else pred = currentValidPredicted;

            output[i] = Math.Abs(act - pred);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeSquaredErrorsSimd(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output)
    {
        int len = actual.Length;
        int vectorSize = Vector256<double>.Count;
        int vectorEnd = len - (len % vectorSize);

        int i = 0;
        for (; i < vectorEnd; i += vectorSize)
        {
            Vector256<double> actVec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(actual.Slice(i)));
            Vector256<double> predVec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(predicted.Slice(i)));

            // error = actual - predicted
            Vector256<double> errorVec = Avx.Subtract(actVec, predVec);

            // sqError = error * error
            Vector256<double> sqErrorVec = Avx.Multiply(errorVec, errorVec);

            sqErrorVec.StoreUnsafe(ref MemoryMarshal.GetReference(output.Slice(i)));
        }

        // Handle remainder with scalar
        for (; i < len; i++)
        {
            double diff = actual[i] - predicted[i];
            output[i] = diff * diff;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeSquaredErrorsScalar(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted)
    {
        int len = actual.Length;
        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) currentValidActual = act; else act = currentValidActual;
            if (double.IsFinite(pred)) currentValidPredicted = pred; else pred = currentValidPredicted;

            double diff = act - pred;
            output[i] = diff * diff;
        }
    }

    /// <summary>
    /// Numerically stable log(cosh(x)) computation.
    /// For large |x|, uses approximation: |x| - log(2)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double LogCosh(double x)
    {
        // For |x| > 20, cosh(x) ≈ exp(|x|)/2, so log(cosh(x)) ≈ |x| - log(2)
        double absX = Math.Abs(x);
        if (absX > 20.0)
        {
            return absX - 0.6931471805599453; // log(2)
        }
        return Math.Log(Math.Cosh(x));
    }

    #endregion
}