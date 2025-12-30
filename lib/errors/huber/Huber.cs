using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// Huber: Huber Loss
/// </summary>
/// <remarks>
/// Huber Loss combines the best properties of MSE and MAE. For small errors
/// (|error| ≤ delta), it behaves like MSE (quadratic). For large errors
/// (|error| > delta), it behaves like MAE (linear).
///
/// Formula:
/// If |error| ≤ delta: L = 0.5 * error²
/// If |error| > delta:  L = delta * |error| - 0.5 * delta²
///
/// Key properties:
/// - Differentiable everywhere (unlike MAE)
/// - Robust to outliers (unlike MSE)
/// - Delta controls the transition point
/// - Default delta = 1.345 (for 95% efficiency with normal distribution)
/// </remarks>
[SkipLocalsInit]
public sealed class Huber : AbstractBase
{
    private readonly double _delta;
    private readonly double _halfDeltaSquared;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Sum, double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    public Huber(int period, double delta = 1.345)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (delta <= 0)
            throw new ArgumentException("Delta must be greater than 0", nameof(delta));

        _delta = delta;
        _halfDeltaSquared = 0.5 * delta * delta;
        _buffer = new RingBuffer(period);
        Name = $"Huber({period},{delta:F3})";
        WarmupPeriod = period;
    }

    public override bool IsHot => _buffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateHuberLoss(double error)
    {
        double absError = Math.Abs(error);
        // Use FMA for the linear portion: delta * absError - halfDeltaSquared
        // = FMA(delta, absError, -halfDeltaSquared)
        return absError <= _delta
            ? 0.5 * error * error
            : Math.FusedMultiplyAdd(_delta, absError, -_halfDeltaSquared);
    }

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

        double error = actualVal - predictedVal;
        double huberLoss = CalculateHuberLoss(error);

        if (isNew)
        {
            _p_state = _state;

            double removedValue = _buffer.Count == _buffer.Capacity ? _buffer.Oldest : 0.0;
            _state.Sum = _state.Sum - removedValue + huberLoss;
            _buffer.Add(huberLoss);

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
            _state.Sum = _state.Sum - removedValue + huberLoss;
            _buffer.UpdateNewest(huberLoss);
            _state.Sum = _buffer.RecalculateSum();
        }

        double result = _buffer.Count > 0 ? _state.Sum / _buffer.Count : huberLoss;
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
        throw new NotSupportedException("Huber requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("Huber requires two inputs. Use Calculate(actualSeries, predictedSeries, period, delta).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("Huber requires two inputs.");
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    public static TSeries Calculate(TSeries actual, TSeries predicted, int period, double delta = 1.345)
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

        Batch(actual.Values, predicted.Values, vSpan, period, delta);
        actual.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period, double delta = 1.345)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException("All spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (delta <= 0)
            throw new ArgumentException("Delta must be greater than 0", nameof(delta));

        int len = actual.Length;
        if (len == 0) return;

        double halfDeltaSquared = 0.5 * delta * delta;
        double negHalfDeltaSquared = -halfDeltaSquared;

        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        // Pre-compute Huber losses using SIMD if available and data is clean
        // Then apply rolling window average
        Span<double> huberLosses = len <= StackAllocThreshold
            ? stackalloc double[len]
            : new double[len];

        ComputeHuberLosses(actual, predicted, huberLosses, delta, halfDeltaSquared, negHalfDeltaSquared);

        // Apply rolling window average with O(1) per element
        double sum = 0;
        int bufferIndex = 0;

        int warmupEnd = Math.Min(period, len);
        for (int i = 0; i < warmupEnd; i++)
        {
            sum += huberLosses[i];
            buffer[i] = huberLosses[i];
            output[i] = sum / (i + 1);
        }

        int tickCount = 0;
        for (int i = warmupEnd; i < len; i++)
        {
            double huberLoss = huberLosses[i];
            sum = sum - buffer[bufferIndex] + huberLoss;
            buffer[bufferIndex] = huberLoss;

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
    private static void ComputeHuberLosses(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> huberLosses,
        double delta,
        double halfDeltaSquared,
        double negHalfDeltaSquared)
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
                ComputeHuberLossesSimd(actual, predicted, huberLosses, delta, halfDeltaSquared, negHalfDeltaSquared);
                return;
            }
        }

        // Scalar fallback with NaN handling
        ComputeHuberLossesScalar(actual, predicted, huberLosses, delta, negHalfDeltaSquared, lastValidActual, lastValidPredicted);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeHuberLossesSimd(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> huberLosses,
        double delta,
        double halfDeltaSquared,
        double negHalfDeltaSquared)
    {
        int len = actual.Length;
        int vectorSize = Vector256<double>.Count;
        int vectorEnd = len - (len % vectorSize);

        Vector256<double> deltaVec = Vector256.Create(delta);
        Vector256<double> halfVec = Vector256.Create(0.5);
        Vector256<double> negHalfDeltaSqVec = Vector256.Create(negHalfDeltaSquared);

        int i = 0;
        for (; i < vectorEnd; i += vectorSize)
        {
            Vector256<double> actVec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(actual.Slice(i)));
            Vector256<double> predVec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(predicted.Slice(i)));

            // error = actual - predicted
            Vector256<double> errorVec = Avx.Subtract(actVec, predVec);

            // absError = |error|
            Vector256<double> absErrorVec = Avx.And(errorVec, Vector256.Create(~(1L << 63)).AsDouble());

            // quadratic = 0.5 * error * error
            Vector256<double> quadraticVec = Avx.Multiply(halfVec, Avx.Multiply(errorVec, errorVec));

            // linear = delta * absError - halfDeltaSquared (using FMA)
            Vector256<double> linearVec = Fma.IsSupported
                ? Fma.MultiplyAdd(deltaVec, absErrorVec, negHalfDeltaSqVec)
                : Avx.Add(Avx.Multiply(deltaVec, absErrorVec), negHalfDeltaSqVec);

            // mask = absError <= delta
            Vector256<double> maskVec = Avx.CompareLessThanOrEqual(absErrorVec, deltaVec);

            // result = mask ? quadratic : linear
            Vector256<double> resultVec = Avx.BlendVariable(linearVec, quadraticVec, maskVec);

            resultVec.StoreUnsafe(ref MemoryMarshal.GetReference(huberLosses.Slice(i)));
        }

        // Handle remainder with scalar
        for (; i < len; i++)
        {
            double error = actual[i] - predicted[i];
            double absError = Math.Abs(error);
            huberLosses[i] = absError <= delta
                ? 0.5 * error * error
                : Math.FusedMultiplyAdd(delta, absError, negHalfDeltaSquared);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeHuberLossesScalar(
        ReadOnlySpan<double> actual,
        ReadOnlySpan<double> predicted,
        Span<double> huberLosses,
        double delta,
        double negHalfDeltaSquared,
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

            double error = act - pred;
            double absError = Math.Abs(error);
            huberLosses[i] = absError <= delta
                ? 0.5 * error * error
                : Math.FusedMultiplyAdd(delta, absError, negHalfDeltaSquared);
        }
    }
}
