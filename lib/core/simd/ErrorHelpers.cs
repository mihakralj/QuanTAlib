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

        // Try SIMD path for clean data (no NaN/Inf)
        if (Avx2.IsSupported && len >= Vector256<double>.Count && IsDataClean(actual, predicted))
        {
            ComputeSignedErrorsSimd(actual, predicted, output);
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
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

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
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

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
        // Full validation: check every element to ensure no NaN/Inf slips through to SIMD path
        int len = actual.Length;

        for (int i = 0; i < len; i++)
        {
            if (!double.IsFinite(actual[i]) || !double.IsFinite(predicted[i]))
                return false;
        }
        return true;
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