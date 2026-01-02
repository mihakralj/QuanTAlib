using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// ME: Mean Error (also known as Mean Bias Error)
/// </summary>
/// <remarks>
/// ME measures the average error between actual and predicted values,
/// preserving the sign to indicate systematic bias in predictions.
///
/// Formula:
/// ME = (1/n) * Σ(actual - predicted)
///
/// Key properties:
/// - Can be positive or negative
/// - Positive ME indicates under-prediction (actual > predicted)
/// - Negative ME indicates over-prediction (actual &lt; predicted)
/// - ME = 0 indicates no systematic bias (but not necessarily accurate predictions)
/// - Errors can cancel out, hiding large individual errors
/// </remarks>
[SkipLocalsInit]
public sealed class Me : AbstractBase
{
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Sum, double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    public Me(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _buffer = new RingBuffer(period);
        Name = $"Me({period})";
        WarmupPeriod = period;
    }

    public override bool IsHot => _buffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, bool isNew = true)
    {
        double actualVal = actual.Value;
        double predictedVal = predicted.Value;

        if (!double.IsFinite(actualVal))
            actualVal = double.IsFinite(_state.LastValidActual) ? _state.LastValidActual : 0.0;
        else
            _state.LastValidActual = actualVal;

        if (!double.IsFinite(predictedVal))
            predictedVal = double.IsFinite(_state.LastValidPredicted) ? _state.LastValidPredicted : 0.0;
        else
            _state.LastValidPredicted = predictedVal;

        double error = actualVal - predictedVal; // Preserves sign!

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double actual, double predicted, bool isNew = true)
    {
        return Update(new TValue(DateTime.UtcNow, actual), new TValue(DateTime.UtcNow, predicted), isNew);
    }

    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("ME requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("ME requires two inputs. Use Calculate(actualSeries, predictedSeries, period).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("ME requires two inputs.");
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException("All spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = actual.Length;
        if (len == 0) return;

        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        // Pre-compute signed errors using SIMD if available and data is clean
        Span<double> errors = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        ComputeSignedErrors(actual, predicted, errors);

        // Apply rolling window average with O(1) per element
        double sum = 0;
        int bufferIndex = 0;

        int warmupEnd = Math.Min(period, len);
        for (int i = 0; i < warmupEnd; i++)
        {
            sum += errors[i];
            buffer[i] = errors[i];
            output[i] = sum / (i + 1);
        }

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
    private static void ComputeSignedErrors(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> errors)
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
                ComputeSignedErrorsSimd(actual, predicted, errors);
                return;
            }
        }

        // Scalar fallback with NaN handling
        ComputeSignedErrorsScalar(actual, predicted, errors, lastValidActual, lastValidPredicted);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeSignedErrorsSimd(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> errors)
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

            errorVec.StoreUnsafe(ref MemoryMarshal.GetReference(errors.Slice(i)));
        }

        // Handle remainder with scalar
        for (; i < len; i++)
        {
            errors[i] = actual[i] - predicted[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeSignedErrorsScalar(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> errors,
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

            errors[i] = act - pred;
        }
    }
}
