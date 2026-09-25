using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace QuanTAlib;

/// <summary>
/// SIMD-accelerated error computation helpers for error indicators.
/// Provides shared methods for computing absolute errors, squared errors,
/// and percentage errors with automatic SIMD/scalar fallback.
/// </summary>
public static class ErrorHelpers
{
    /// <summary>
    /// Default stack allocation threshold for temporary buffers.
    /// 256 doubles = 2KB, safe margin for nested calls on 1MB thread stack.
    /// Beyond this threshold, ArrayPool is used instead of stackalloc.
    /// </summary>
    public const int StackAllocThreshold = 256;

    /// <summary>
    /// Default resync interval for running sums to correct floating-point drift.
    /// </summary>
    public const int DefaultResyncInterval = 1000;

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
        {
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);

        // Try SIMD path - NaN detection is integrated into the SIMD loop
#if NET5_0_OR_GREATER
        if (Avx2.IsSupported && len >= Vector256<double>.Count)
        {
            int processedCount = ComputeSignedErrorsSimdWithNaNDetection(actual, predicted, output, ref lastValidActual, ref lastValidPredicted);
            if (processedCount == len)
            {
                return; // All processed via SIMD
            }
            // Continue with scalar for remaining elements (NaN was detected)
            ComputeSignedErrorsScalar(actual.Slice(processedCount), predicted.Slice(processedCount), output.Slice(processedCount), lastValidActual, lastValidPredicted);
            return;
        }
#endif

#if !NET5_0_OR_GREATER
        if (len >= Vector<double>.Count)
        {
            ComputeSignedErrorsVectorCore(actual, predicted, output, lastValidActual, lastValidPredicted);
            return;
        }
#endif

        // Scalar fallback with NaN handling
        ComputeSignedErrorsScalar(actual, predicted, output, lastValidActual, lastValidPredicted);
    }

    /// <summary>
    /// Computes absolute errors: |actual - predicted|
    /// Uses AVX2 SIMD when available with integrated NaN detection, with scalar fallback for NaN handling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeAbsoluteErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
        {
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);

        // Try SIMD path - NaN detection is integrated into the SIMD loop (avoids double-pass)
#if NET5_0_OR_GREATER
        if (Avx2.IsSupported && len >= Vector256<double>.Count)
        {
            int processedCount = ComputeAbsoluteErrorsSimdWithNaNDetection(actual, predicted, output, ref lastValidActual, ref lastValidPredicted);
            if (processedCount == len)
            {
                return; // All processed via SIMD
            }
            // Continue with scalar for remaining elements (NaN was detected)
            ComputeAbsoluteErrorsScalar(actual.Slice(processedCount), predicted.Slice(processedCount), output.Slice(processedCount), lastValidActual, lastValidPredicted);
            return;
        }
#endif

#if !NET5_0_OR_GREATER
        if (len >= Vector<double>.Count)
        {
            ComputeAbsoluteErrorsVectorCore(actual, predicted, output, lastValidActual, lastValidPredicted);
            return;
        }
#endif

        // Scalar fallback with NaN handling
        ComputeAbsoluteErrorsScalar(actual, predicted, output, lastValidActual, lastValidPredicted);
    }

    /// <summary>
    /// Computes squared errors: (actual - predicted)²
    /// Uses AVX2 SIMD when available with integrated NaN detection, with scalar fallback for NaN handling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeSquaredErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
        {
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);

        // Try SIMD path - NaN detection is integrated into the SIMD loop (avoids double-pass)
#if NET5_0_OR_GREATER
        if (Avx2.IsSupported && len >= Vector256<double>.Count)
        {
            int processedCount = ComputeSquaredErrorsSimdWithNaNDetection(actual, predicted, output, ref lastValidActual, ref lastValidPredicted);
            if (processedCount == len)
            {
                return; // All processed via SIMD
            }
            // Continue with scalar for remaining elements (NaN was detected)
            ComputeSquaredErrorsScalar(actual.Slice(processedCount), predicted.Slice(processedCount), output.Slice(processedCount), lastValidActual, lastValidPredicted);
            return;
        }
#endif

#if !NET5_0_OR_GREATER
        if (len >= Vector<double>.Count)
        {
            ComputeSquaredErrorsVectorCore(actual, predicted, output, lastValidActual, lastValidPredicted);
            return;
        }
#endif

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
        {
            throw new ArgumentException("All spans must have the same length", nameof(output));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);
        double lastValidWeight = FindFirstValidValue(weights);

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;
        double currentValidWeight = lastValidWeight;

#if !NET5_0_OR_GREATER
        if (len >= Vector<double>.Count)
        {
            ComputeWeightedErrorsVectorCore(actual, predicted, weights, output, lastValidActual, lastValidPredicted, lastValidWeight);
            return;
        }
#endif

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];
            double wgt = weights[i];

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

            if (double.IsFinite(wgt))
            {
                currentValidWeight = wgt;
            }
            else
            {
                wgt = currentValidWeight;
            }

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
        {
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

#if !NET5_0_OR_GREATER
        if (len >= Vector<double>.Count)
        {
            ComputePercentageErrorsVectorCore(actual, predicted, output, lastValidActual, lastValidPredicted, epsilon);
            return;
        }
#endif

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

#pragma warning disable S1121 // Assignments should not be made from within sub-expressions
            act = double.IsFinite(act) ? (currentValidActual = act) : currentValidActual;
            pred = double.IsFinite(pred) ? (currentValidPredicted = pred) : currentValidPredicted;
#pragma warning restore S1121

            double absActual = Math.Abs(act);
            output[i] = absActual < epsilon
                ? Math.Abs(act - pred)
                : Math.Abs(act - pred) / absActual * 100.0;
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
        {
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

#if !NET5_0_OR_GREATER
        if (len >= Vector<double>.Count)
        {
            ComputeSymmetricPercentageErrorsVectorCore(actual, predicted, output, lastValidActual, lastValidPredicted, epsilon);
            return;
        }
#endif

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

            double denominator = (Math.Abs(act) + Math.Abs(pred)) / 2.0;
            output[i] = denominator < epsilon
                ? 0.0  // Both values near zero
                : Math.Abs(act - pred) / denominator * 100.0;
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
        {
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

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
        {
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);
        double deltaSquared = delta * delta;

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

#if !NET5_0_OR_GREATER
        if (len >= Vector<double>.Count)
        {
            ComputePseudoHuberErrorsVectorCore(actual, predicted, output, lastValidActual, lastValidPredicted, delta);
            return;
        }
#endif

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

            double diff = act - pred;
            double ratio = diff / delta;
            // δ² * (√(1 + (error/δ)²) - 1)
            output[i] = deltaSquared * (Math.Sqrt(1.0 + (ratio * ratio)) - 1.0);
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
        {
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);
        double cSquaredOver6 = (c * c) / 6.0;

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

#if !NET5_0_OR_GREATER
        if (len >= Vector<double>.Count)
        {
            ComputeTukeyBiweightErrorsVectorCore(actual, predicted, output, lastValidActual, lastValidPredicted, c);
            return;
        }
#endif

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

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
        {
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(output));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);
        double halfDelta = 0.5 * delta;

        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

#if !NET5_0_OR_GREATER
        if (len >= Vector<double>.Count)
        {
            ComputeHuberErrorsVectorCore(actual, predicted, output, lastValidActual, lastValidPredicted, delta);
            return;
        }
#endif

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

            double diff = act - pred;
            double absDiff = Math.Abs(diff);

            output[i] = absDiff <= delta
                ? 0.5 * diff * diff
                : delta * (absDiff - halfDelta);
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
        {
            throw new ArgumentException("Spans must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = errors.Length;
        if (len == 0)
        {
            return;
        }

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
                if (bufferIndex >= period)
                {
                    bufferIndex = 0;
                }

                output[i] = sum / period;

                tickCount++;
                if (tickCount >= resyncInterval)
                {
                    tickCount = 0;
                    double recalcSum = 0;
                    for (int k = 0; k < period; k++)
                    {
                        recalcSum += buffer[k];
                    }

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
        {
            throw new ArgumentException("Spans must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = squaredErrors.Length;
        if (len == 0)
        {
            return;
        }

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
                if (bufferIndex >= period)
                {
                    bufferIndex = 0;
                }

                output[i] = Math.Sqrt(sum / period);

                tickCount++;
                if (tickCount >= resyncInterval)
                {
                    tickCount = 0;
                    double recalcSum = 0;
                    for (int k = 0; k < period; k++)
                    {
                        recalcSum += buffer[k];
                    }

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
        {
            throw new ArgumentException("Spans must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = weightedSquaredErrors.Length;
        if (len == 0)
        {
            return;
        }

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
                if (bufferIndex >= period)
                {
                    bufferIndex = 0;
                }

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

    /// <summary>
    /// Sanitizes input spans by replacing NaN/Infinity values with the last valid value.
    /// Writes sanitized values to output spans for use in batch calculations.
    /// </summary>
    /// <param name="actual">Input actual values</param>
    /// <param name="predicted">Input predicted values</param>
    /// <param name="actualOut">Output sanitized actual values</param>
    /// <param name="predictedOut">Output sanitized predicted values</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SanitizeInputs(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> actualOut,
        Span<double> predictedOut)
    {
        if (actual.Length != predicted.Length || actual.Length != actualOut.Length || actual.Length != predictedOut.Length)
        {
            throw new ArgumentException(SpanLengthMismatchMessage, nameof(predictedOut));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        double lastValidActual = FindFirstValidValue(actual);
        double lastValidPredicted = FindFirstValidValue(predicted);

#if !NET5_0_OR_GREATER
        if (len >= Vector<double>.Count)
        {
            SanitizeInputsVectorCore(actual, predicted, actualOut, predictedOut, lastValidActual, lastValidPredicted);
            return;
        }
#endif

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
            {
                lastValidActual = act;
            }
            else
            {
                act = lastValidActual;
            }

            if (double.IsFinite(pred))
            {
                lastValidPredicted = pred;
            }
            else
            {
                pred = lastValidPredicted;
            }

            actualOut[i] = act;
            predictedOut[i] = pred;
        }
    }

    /// <summary>
    /// Finds the first finite value in a span, or returns 0.0 if none found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double FindFirstValidValue(ReadOnlySpan<double> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (double.IsFinite(span[i]))
            {
                return span[i];
            }
        }
        return 0.0;
    }

    #region Private Helpers

#if NET5_0_OR_GREATER
    /// <summary>
    /// SIMD path with integrated NaN detection. Returns the number of elements processed.
    /// If NaN is detected, returns the index where NaN was found so caller can continue with scalar.
    /// Updates lastValidActual/lastValidPredicted to track last seen finite values for scalar continuation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeSignedErrorsSimdWithNaNDetection(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        ref double lastValidActual,
        ref double lastValidPredicted)
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
                // NaN detected - update lastValid from previously processed elements before returning
                if (i > 0)
                {
                    lastValidActual = actual[i - 1];
                    lastValidPredicted = predicted[i - 1];
                }
                return i;
            }

            // No NaN - compute error
            Vector256<double> errorVec = Avx.Subtract(actVec, predVec);
            errorVec.StoreUnsafe(ref MemoryMarshal.GetReference(output.Slice(i)));
        }

        // Update lastValid from end of SIMD-processed section
        if (i > 0)
        {
            lastValidActual = actual[i - 1];
            lastValidPredicted = predicted[i - 1];
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

            lastValidActual = act;
            lastValidPredicted = pred;
            output[i] = act - pred;
        }

        return len;
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeSignedErrorsScalar(
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

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

            output[i] = act - pred;
        }
    }

#if NET5_0_OR_GREATER
    /// <summary>
    /// SIMD path with integrated NaN detection for absolute errors. Returns the number of elements processed.
    /// If NaN is detected, returns the index where NaN was found so caller can continue with scalar.
    /// Updates lastValidActual/lastValidPredicted to track last seen finite values for scalar continuation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeAbsoluteErrorsSimdWithNaNDetection(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        ref double lastValidActual,
        ref double lastValidPredicted)
    {
        int len = actual.Length;
        int vectorSize = Vector256<double>.Count;
        int vectorEnd = len - (len % vectorSize);

        // Mask for absolute value (clear sign bit)
        Vector256<double> absMask = Vector256.Create(~(1L << 63)).AsDouble();

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
                // NaN detected - update lastValid from previously processed elements before returning
                if (i > 0)
                {
                    lastValidActual = actual[i - 1];
                    lastValidPredicted = predicted[i - 1];
                }
                return i;
            }

            // No NaN - compute absolute error: |actual - predicted|
            Vector256<double> errorVec = Avx.Subtract(actVec, predVec);
            Vector256<double> absErrorVec = Avx.And(errorVec, absMask);
            absErrorVec.StoreUnsafe(ref MemoryMarshal.GetReference(output.Slice(i)));
        }

        // Update lastValid from end of SIMD-processed section
        if (i > 0)
        {
            lastValidActual = actual[i - 1];
            lastValidPredicted = predicted[i - 1];
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

            lastValidActual = act;
            lastValidPredicted = pred;
            output[i] = Math.Abs(act - pred);
        }

        return len;
    }
#endif

#if NET5_0_OR_GREATER
    /// <summary>
    /// SIMD path with integrated NaN detection for squared errors. Returns the number of elements processed.
    /// If NaN is detected, returns the index where NaN was found so caller can continue with scalar.
    /// Updates lastValidActual/lastValidPredicted to track last seen finite values for scalar continuation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeSquaredErrorsSimdWithNaNDetection(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        ref double lastValidActual,
        ref double lastValidPredicted)
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
                // NaN detected - update lastValid from previously processed elements before returning
                if (i > 0)
                {
                    lastValidActual = actual[i - 1];
                    lastValidPredicted = predicted[i - 1];
                }
                return i;
            }

            // No NaN - compute squared error: (actual - predicted)²
            Vector256<double> errorVec = Avx.Subtract(actVec, predVec);
            Vector256<double> sqErrorVec = Avx.Multiply(errorVec, errorVec);
            sqErrorVec.StoreUnsafe(ref MemoryMarshal.GetReference(output.Slice(i)));
        }

        // Update lastValid from end of SIMD-processed section
        if (i > 0)
        {
            lastValidActual = actual[i - 1];
            lastValidPredicted = predicted[i - 1];
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

            lastValidActual = act;
            lastValidPredicted = pred;
            double diff = act - pred;
            output[i] = diff * diff;
        }

        return len;
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeAbsoluteErrorsScalar(
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

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

            output[i] = Math.Abs(act - pred);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeSquaredErrorsScalar(
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

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeWeightedErrorsScalarCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        ReadOnlySpan<double> weights,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted,
        double lastValidWeight)
    {
        int len = actual.Length;
        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;
        double currentValidWeight = lastValidWeight;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];
            double wgt = weights[i];

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

            if (double.IsFinite(wgt))
            {
                currentValidWeight = wgt;
            }
            else
            {
                wgt = currentValidWeight;
            }

            double diff = act - pred;
            output[i] = wgt * diff * diff;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputePercentageErrorsScalarCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted,
        double epsilon)
    {
        int len = actual.Length;
        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

            double absActual = Math.Abs(act);
            output[i] = absActual < epsilon
                ? Math.Abs(act - pred)
                : Math.Abs(act - pred) / absActual * 100.0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeSymmetricPercentageErrorsScalarCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted,
        double epsilon)
    {
        int len = actual.Length;
        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

            double denominator = (Math.Abs(act) + Math.Abs(pred)) / 2.0;
            output[i] = denominator < epsilon
                ? 0.0
                : Math.Abs(act - pred) / denominator * 100.0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputePseudoHuberErrorsScalarCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted,
        double delta)
    {
        int len = actual.Length;
        double deltaSquared = delta * delta;
        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

            double diff = act - pred;
            double ratio = diff / delta;
            output[i] = deltaSquared * (Math.Sqrt(1.0 + (ratio * ratio)) - 1.0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeTukeyBiweightErrorsScalarCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted,
        double c)
    {
        int len = actual.Length;
        double cSquaredOver6 = (c * c) / 6.0;
        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeHuberErrorsScalarCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted,
        double delta)
    {
        int len = actual.Length;
        double halfDelta = 0.5 * delta;
        double currentValidActual = lastValidActual;
        double currentValidPredicted = lastValidPredicted;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
            {
                currentValidActual = act;
            }
            else
            {
                act = currentValidActual;
            }

            if (double.IsFinite(pred))
            {
                currentValidPredicted = pred;
            }
            else
            {
                pred = currentValidPredicted;
            }

            double diff = act - pred;
            double absDiff = Math.Abs(diff);

            output[i] = absDiff <= delta
                ? 0.5 * diff * diff
                : delta * (absDiff - halfDelta);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SanitizeInputsScalarCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> actualOut,
        Span<double> predictedOut,
        double lastValidActual,
        double lastValidPredicted)
    {
        for (int i = 0; i < actual.Length; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act))
            {
                lastValidActual = act;
            }
            else
            {
                act = lastValidActual;
            }

            if (double.IsFinite(pred))
            {
                lastValidPredicted = pred;
            }
            else
            {
                pred = lastValidPredicted;
            }

            actualOut[i] = act;
            predictedOut[i] = pred;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeSignedErrorsVectorCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted)
    {
        if (actual.ContainsNonFinite() || predicted.ContainsNonFinite())
        {
            ComputeSignedErrorsScalar(actual, predicted, output, lastValidActual, lastValidPredicted);
            return;
        }

        int vectorSize = Vector<double>.Count;
        int len = actual.Length;
        int i = 0;
        for (; i + vectorSize <= len; i += vectorSize)
        {
            Vector<double> actVec = VectorCompat.Load<double>(actual.Slice(i, vectorSize));
            Vector<double> predVec = VectorCompat.Load<double>(predicted.Slice(i, vectorSize));
            (actVec - predVec).CopyTo(output.Slice(i, vectorSize));
        }

        for (; i < len; i++)
        {
            output[i] = actual[i] - predicted[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeAbsoluteErrorsVectorCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted)
    {
        if (actual.ContainsNonFinite() || predicted.ContainsNonFinite())
        {
            ComputeAbsoluteErrorsScalar(actual, predicted, output, lastValidActual, lastValidPredicted);
            return;
        }

        int vectorSize = Vector<double>.Count;
        int len = actual.Length;
        int i = 0;
        for (; i + vectorSize <= len; i += vectorSize)
        {
            Vector<double> actVec = VectorCompat.Load<double>(actual.Slice(i, vectorSize));
            Vector<double> predVec = VectorCompat.Load<double>(predicted.Slice(i, vectorSize));
            Vector.Abs(actVec - predVec).CopyTo(output.Slice(i, vectorSize));
        }

        for (; i < len; i++)
        {
            output[i] = Math.Abs(actual[i] - predicted[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeSquaredErrorsVectorCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted)
    {
        if (actual.ContainsNonFinite() || predicted.ContainsNonFinite())
        {
            ComputeSquaredErrorsScalar(actual, predicted, output, lastValidActual, lastValidPredicted);
            return;
        }

        int vectorSize = Vector<double>.Count;
        int len = actual.Length;
        int i = 0;
        for (; i + vectorSize <= len; i += vectorSize)
        {
            Vector<double> actVec = VectorCompat.Load<double>(actual.Slice(i, vectorSize));
            Vector<double> predVec = VectorCompat.Load<double>(predicted.Slice(i, vectorSize));
            Vector<double> diff = actVec - predVec;
            (diff * diff).CopyTo(output.Slice(i, vectorSize));
        }

        for (; i < len; i++)
        {
            double diff = actual[i] - predicted[i];
            output[i] = diff * diff;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeWeightedErrorsVectorCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        ReadOnlySpan<double> weights,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted,
        double lastValidWeight)
    {
        if (actual.ContainsNonFinite() || predicted.ContainsNonFinite() || weights.ContainsNonFinite())
        {
            ComputeWeightedErrorsScalarCore(actual, predicted, weights, output, lastValidActual, lastValidPredicted, lastValidWeight);
            return;
        }

        int vectorSize = Vector<double>.Count;
        int len = actual.Length;
        int i = 0;
        for (; i + vectorSize <= len; i += vectorSize)
        {
            Vector<double> actVec = VectorCompat.Load<double>(actual.Slice(i, vectorSize));
            Vector<double> predVec = VectorCompat.Load<double>(predicted.Slice(i, vectorSize));
            Vector<double> wgtVec = VectorCompat.Load<double>(weights.Slice(i, vectorSize));
            Vector<double> diff = actVec - predVec;
            (wgtVec * diff * diff).CopyTo(output.Slice(i, vectorSize));
        }

        for (; i < len; i++)
        {
            double diff = actual[i] - predicted[i];
            output[i] = weights[i] * diff * diff;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputePercentageErrorsVectorCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted,
        double epsilon)
    {
        if (actual.ContainsNonFinite() || predicted.ContainsNonFinite())
        {
            ComputePercentageErrorsScalarCore(actual, predicted, output, lastValidActual, lastValidPredicted, epsilon);
            return;
        }

        int vectorSize = Vector<double>.Count;
        int len = actual.Length;
        Vector<double> epsilonVec = new(epsilon);
        Vector<double> hundred = new(100.0);
        int i = 0;
        for (; i + vectorSize <= len; i += vectorSize)
        {
            Vector<double> actVec = VectorCompat.Load<double>(actual.Slice(i, vectorSize));
            Vector<double> predVec = VectorCompat.Load<double>(predicted.Slice(i, vectorSize));
            Vector<double> absDiff = Vector.Abs(actVec - predVec);
            Vector<double> absAct = Vector.Abs(actVec);
            Vector<double> result = Vector.ConditionalSelect(
                Vector.LessThan(absAct, epsilonVec),
                absDiff,
                (absDiff / absAct) * hundred);
            result.CopyTo(output.Slice(i, vectorSize));
        }

        for (; i < len; i++)
        {
            double absActual = Math.Abs(actual[i]);
            double absDiff = Math.Abs(actual[i] - predicted[i]);
            output[i] = absActual < epsilon ? absDiff : absDiff / absActual * 100.0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeSymmetricPercentageErrorsVectorCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted,
        double epsilon)
    {
        if (actual.ContainsNonFinite() || predicted.ContainsNonFinite())
        {
            ComputeSymmetricPercentageErrorsScalarCore(actual, predicted, output, lastValidActual, lastValidPredicted, epsilon);
            return;
        }

        int vectorSize = Vector<double>.Count;
        int len = actual.Length;
        Vector<double> epsilonVec = new(epsilon);
        Vector<double> hundred = new(100.0);
        Vector<double> half = new(0.5);
        int i = 0;
        for (; i + vectorSize <= len; i += vectorSize)
        {
            Vector<double> actVec = VectorCompat.Load<double>(actual.Slice(i, vectorSize));
            Vector<double> predVec = VectorCompat.Load<double>(predicted.Slice(i, vectorSize));
            Vector<double> denom = (Vector.Abs(actVec) + Vector.Abs(predVec)) * half;
            Vector<double> absDiff = Vector.Abs(actVec - predVec);
            Vector<double> result = Vector.ConditionalSelect(
                Vector.LessThan(denom, epsilonVec),
                Vector<double>.Zero,
                (absDiff / denom) * hundred);
            result.CopyTo(output.Slice(i, vectorSize));
        }

        for (; i < len; i++)
        {
            double denominator = (Math.Abs(actual[i]) + Math.Abs(predicted[i])) / 2.0;
            double absDiff = Math.Abs(actual[i] - predicted[i]);
            output[i] = denominator < epsilon ? 0.0 : absDiff / denominator * 100.0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputePseudoHuberErrorsVectorCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted,
        double delta)
    {
        if (actual.ContainsNonFinite() || predicted.ContainsNonFinite())
        {
            ComputePseudoHuberErrorsScalarCore(actual, predicted, output, lastValidActual, lastValidPredicted, delta);
            return;
        }

        int vectorSize = Vector<double>.Count;
        int len = actual.Length;
        double deltaSquared = delta * delta;
        Vector<double> deltaVec = new(delta);
        Vector<double> deltaSqVec = new(deltaSquared);
        Vector<double> one = Vector<double>.One;
        int i = 0;
        for (; i + vectorSize <= len; i += vectorSize)
        {
            Vector<double> actVec = VectorCompat.Load<double>(actual.Slice(i, vectorSize));
            Vector<double> predVec = VectorCompat.Load<double>(predicted.Slice(i, vectorSize));
            Vector<double> diff = actVec - predVec;
            Vector<double> ratio = diff / deltaVec;
            Vector<double> inner = Vector.SquareRoot(one + (ratio * ratio));
            ((inner - one) * deltaSqVec).CopyTo(output.Slice(i, vectorSize));
        }

        for (; i < len; i++)
        {
            double diff = actual[i] - predicted[i];
            double ratio = diff / delta;
            output[i] = deltaSquared * (Math.Sqrt(1.0 + (ratio * ratio)) - 1.0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeTukeyBiweightErrorsVectorCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted,
        double c)
    {
        if (actual.ContainsNonFinite() || predicted.ContainsNonFinite())
        {
            ComputeTukeyBiweightErrorsScalarCore(actual, predicted, output, lastValidActual, lastValidPredicted, c);
            return;
        }

        int vectorSize = Vector<double>.Count;
        int len = actual.Length;
        double cSquaredOver6 = (c * c) / 6.0;
        Vector<double> cVec = new(c);
        Vector<double> cSqOver6Vec = new(cSquaredOver6);
        Vector<double> one = Vector<double>.One;
        int i = 0;
        for (; i + vectorSize <= len; i += vectorSize)
        {
            Vector<double> actVec = VectorCompat.Load<double>(actual.Slice(i, vectorSize));
            Vector<double> predVec = VectorCompat.Load<double>(predicted.Slice(i, vectorSize));
            Vector<double> diff = actVec - predVec;
            Vector<double> absDiff = Vector.Abs(diff);
            Vector<double> ratio = diff / cVec;
            Vector<double> ratioSq = ratio * ratio;
            Vector<double> oneMinusRatioSq = one - ratioSq;
            Vector<double> cubed = oneMinusRatioSq * oneMinusRatioSq * oneMinusRatioSq;
            Vector<double> result = Vector.ConditionalSelect(
                Vector.GreaterThan(absDiff, cVec),
                cSqOver6Vec,
                cSqOver6Vec * (one - cubed));
            result.CopyTo(output.Slice(i, vectorSize));
        }

        for (; i < len; i++)
        {
            double diff = actual[i] - predicted[i];
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ComputeHuberErrorsVectorCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> output,
        double lastValidActual,
        double lastValidPredicted,
        double delta)
    {
        if (actual.ContainsNonFinite() || predicted.ContainsNonFinite())
        {
            ComputeHuberErrorsScalarCore(actual, predicted, output, lastValidActual, lastValidPredicted, delta);
            return;
        }

        int vectorSize = Vector<double>.Count;
        int len = actual.Length;
        double halfDelta = 0.5 * delta;
        Vector<double> deltaVec = new(delta);
        Vector<double> halfDeltaVec = new(halfDelta);
        Vector<double> half = new(0.5);
        int i = 0;
        for (; i + vectorSize <= len; i += vectorSize)
        {
            Vector<double> actVec = VectorCompat.Load<double>(actual.Slice(i, vectorSize));
            Vector<double> predVec = VectorCompat.Load<double>(predicted.Slice(i, vectorSize));
            Vector<double> diff = actVec - predVec;
            Vector<double> absDiff = Vector.Abs(diff);
            Vector<double> result = Vector.ConditionalSelect(
                Vector.LessThanOrEqual(absDiff, deltaVec),
                half * diff * diff,
                deltaVec * (absDiff - halfDeltaVec));
            result.CopyTo(output.Slice(i, vectorSize));
        }

        for (; i < len; i++)
        {
            double diff = actual[i] - predicted[i];
            double absDiff = Math.Abs(diff);

            output[i] = absDiff <= delta
                ? 0.5 * diff * diff
                : delta * (absDiff - halfDelta);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SanitizeInputsVectorCore(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> actualOut,
        Span<double> predictedOut,
        double lastValidActual,
        double lastValidPredicted)
    {
        if (actual.ContainsNonFinite() || predicted.ContainsNonFinite())
        {
            SanitizeInputsScalarCore(actual, predicted, actualOut, predictedOut, lastValidActual, lastValidPredicted);
            return;
        }

        int vectorSize = Vector<double>.Count;
        int len = actual.Length;
        int i = 0;
        for (; i + vectorSize <= len; i += vectorSize)
        {
            Vector<double> actVec = VectorCompat.Load<double>(actual.Slice(i, vectorSize));
            actVec.CopyTo(actualOut.Slice(i, vectorSize));
            Vector<double> predVec = VectorCompat.Load<double>(predicted.Slice(i, vectorSize));
            predVec.CopyTo(predictedOut.Slice(i, vectorSize));
        }

        for (; i < len; i++)
        {
            actualOut[i] = actual[i];
            predictedOut[i] = predicted[i];
        }
    }

    #endregion
}
