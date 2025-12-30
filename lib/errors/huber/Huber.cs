using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        return absError <= _delta
            ? 0.5 * error * error
            : _delta * absError - _halfDeltaSquared;
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

        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double sum = 0;
        double lastValidActual = 0;
        double lastValidPredicted = 0;

        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(actual[k])) { lastValidActual = actual[k]; break; }
        }
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(predicted[k])) { lastValidPredicted = predicted[k]; break; }
        }

        int bufferIndex = 0;
        int i = 0;

        int warmupEnd = Math.Min(period, len);
        for (; i < warmupEnd; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double error = act - pred;
            double absError = Math.Abs(error);
            double huberLoss = absError <= delta
                ? 0.5 * error * error
                : delta * absError - halfDeltaSquared;

            sum += huberLoss;
            buffer[i] = huberLoss;
            output[i] = sum / (i + 1);
        }

        int tickCount = 0;
        for (; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double error = act - pred;
            double absError = Math.Abs(error);
            double huberLoss = absError <= delta
                ? 0.5 * error * error
                : delta * absError - halfDeltaSquared;

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
}
