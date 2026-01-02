using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// MSE: Mean Squared Error
/// </summary>
/// <remarks>
/// MSE measures the average of the squares of the errors between actual and
/// predicted values. It penalizes larger errors more heavily than MAE.
///
/// Formula:
/// MSE = (1/n) * Σ(actual - predicted)²
///
/// Uses a RingBuffer for O(1) streaming updates with running sum.
///
/// Key properties:
/// - Always non-negative (MSE ≥ 0)
/// - Units are squared (e.g., if data is in dollars, MSE is in dollars²)
/// - Heavily penalizes outliers due to squaring
/// - MSE = 0 indicates perfect prediction
/// </remarks>
[SkipLocalsInit]
public sealed class Mse : AbstractBase
{
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Sum, double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    /// <summary>
    /// Creates MSE with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Mse(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _buffer = new RingBuffer(period);
        Name = $"Mse({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// True if the MSE has enough data to produce valid results.
    /// </summary>
    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Updates the MSE with new actual and predicted values.
    /// </summary>
    /// <param name="actual">Actual value (source1)</param>
    /// <param name="predicted">Predicted value (source2)</param>
    /// <param name="isNew">Whether this is a new bar.</param>
    /// <returns>The calculated MSE value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, bool isNew = true)
    {
        double actualVal = actual.Value;
        double predictedVal = predicted.Value;

        // Handle NaN/Infinity with last-valid-value substitution
        if (!double.IsFinite(actualVal))
            actualVal = double.IsFinite(_state.LastValidActual) ? _state.LastValidActual : 0.0;
        else
            _state.LastValidActual = actualVal;

        if (!double.IsFinite(predictedVal))
            predictedVal = double.IsFinite(_state.LastValidPredicted) ? _state.LastValidPredicted : 0.0;
        else
            _state.LastValidPredicted = predictedVal;

        double diff = actualVal - predictedVal;
        double error = diff * diff;

        if (isNew)
        {
            _p_state = _state;

            double removedValue = _buffer.Count == _buffer.Capacity ? _buffer.Oldest : 0.0;
            _state.Sum = _state.Sum - removedValue + error;
            _buffer.Add(error);

            _state.TickCount++;
            if (_buffer.IsFull && _state.TickCount >= ResyncInterval)
            {
                _state.TickCount = 0;
                _state.Sum = _buffer.RecalculateSum();
            }
        }
        else
        {
            _state = _p_state;

            double removedValue = _buffer.Count == _buffer.Capacity ? _buffer.Oldest : 0.0;
            _state.Sum = _state.Sum - removedValue + error;
            _buffer.UpdateNewest(error);
            _state.Sum = _buffer.RecalculateSum();
        }

        double result = _buffer.Count > 0 ? _state.Sum / _buffer.Count : error;
        Last = new TValue(actual.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the MSE with raw double values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double actual, double predicted, bool isNew = true)
    {
        return Update(new TValue(DateTime.UtcNow, actual), new TValue(DateTime.UtcNow, predicted), isNew);
    }

    /// <summary>
    /// Single-input Update is not supported. Use Update(actual, predicted).
    /// </summary>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("MSE requires two inputs. Use Update(actual, predicted).");
    }

    /// <summary>
    /// Single-series Update is not supported. Use Calculate(actual, predicted, period).
    /// </summary>
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("MSE requires two inputs. Use Calculate(actualSeries, predictedSeries, period).");
    }

    /// <summary>
    /// Single-series Prime is not supported.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("MSE requires two inputs.");
    }

    /// <summary>
    /// Resets the MSE state.
    /// </summary>
    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    /// <summary>
    /// Calculates MSE for the entire series pair.
    /// </summary>
    public static TSeries Calculate(TSeries actual, TSeries predicted, int period)
    {
        if (actual.Count != predicted.Count)
            throw new ArgumentException("Actual and predicted series must have the same length", nameof(predicted));

        int len = actual.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(actual.Values, predicted.Values, vSpan, period);
        actual.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates MSE in-place using pre-allocated spans.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException("All spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = actual.Length;
        if (len == 0) return;

        CalculateScalarCore(actual, predicted, output, period);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        int len = actual.Length;

        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        // Pre-compute squared errors using SIMD if available and data is clean
        Span<double> sqErrors = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        ComputeSquaredErrors(actual, predicted, sqErrors);

        // Apply rolling window average with O(1) per element
        double sum = 0;
        int bufferIndex = 0;

        int warmupEnd = Math.Min(period, len);
        for (int i = 0; i < warmupEnd; i++)
        {
            sum += sqErrors[i];
            buffer[i] = sqErrors[i];
            output[i] = sum / (i + 1);
        }

        int tickCount = 0;
        for (int i = warmupEnd; i < len; i++)
        {
            double sqError = sqErrors[i];
            sum = sum - buffer[bufferIndex] + sqError;
            buffer[bufferIndex] = sqError;

            bufferIndex++;
            if (bufferIndex >= period) bufferIndex = 0;

            output[i] = sum / period;

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcSum = 0;
                for (int k = 0; k < period; k++) recalcSum += buffer[k];
                sum = recalcSum;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeSquaredErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> sqErrors)
    {
        int len = actual.Length;
        double lastValidActual = 0;
        double lastValidPredicted = 0;

        // Find first valid values
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(actual[k])) { lastValidActual = actual[k]; break; }
        }
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(predicted[k])) { lastValidPredicted = predicted[k]; break; }
        }

        // Try SIMD path for clean data (no NaN/Inf)
        if (Avx2.IsSupported && len >= Vector256<double>.Count)
        {
            // Check if data is clean (no NaN/Inf) - sample check
            bool dataClean = true;
            int checkStep = Math.Max(1, len / 32);
            for (int i = 0; i < len && dataClean; i += checkStep)
            {
                dataClean = double.IsFinite(actual[i]) && double.IsFinite(predicted[i]);
            }

            if (dataClean)
            {
                ComputeSquaredErrorsSimd(actual, predicted, sqErrors);
                return;
            }
        }

        // Scalar fallback with NaN handling
        ComputeSquaredErrorsScalar(actual, predicted, sqErrors, lastValidActual, lastValidPredicted);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeSquaredErrorsSimd(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> sqErrors)
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

            sqErrorVec.StoreUnsafe(ref MemoryMarshal.GetReference(sqErrors.Slice(i)));
        }

        // Handle remainder with scalar
        for (; i < len; i++)
        {
            double diff = actual[i] - predicted[i];
            sqErrors[i] = diff * diff;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeSquaredErrorsScalar(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> sqErrors,
        double lastValidActual,
        double lastValidPredicted)
    {
        int len = actual.Length;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double diff = act - pred;
            sqErrors[i] = diff * diff;
        }
    }
}
