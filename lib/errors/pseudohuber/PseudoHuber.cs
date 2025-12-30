using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PseudoHuber: Pseudo-Huber Loss (Charbonnier Loss)
/// </summary>
/// <remarks>
/// The Pseudo-Huber loss is a smooth approximation to the Huber loss function.
/// Unlike Huber loss which has a piecewise definition, Pseudo-Huber is smooth
/// and differentiable everywhere, making it ideal for gradient-based optimization.
///
/// Formula:
/// PseudoHuber = δ² * (√(1 + (error/δ)²) - 1)
///
/// Key properties:
/// - Smooth and continuously differentiable everywhere
/// - Approximates L2 (squared error) for small errors
/// - Approximates L1 (absolute error) for large errors
/// - δ (delta) controls the transition point
/// - More computationally efficient than Huber's conditional logic
/// - Also known as Charbonnier loss in image processing
/// </remarks>
[SkipLocalsInit]
public sealed class PseudoHuber : AbstractBase
{
    private readonly RingBuffer _lossBuffer;
    private readonly double _delta;
    private readonly double _deltaSquared;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LossSum, double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;
    private const double DefaultDelta = 1.0;

    public PseudoHuber(int period, double delta = DefaultDelta)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (delta <= 0)
            throw new ArgumentException("Delta must be positive", nameof(delta));

        _lossBuffer = new RingBuffer(period);
        _delta = delta;
        _deltaSquared = delta * delta;
        Name = $"PseudoHuber({period},{delta:F3})";
        WarmupPeriod = period;
    }

    public double Delta => _delta;
    public override bool IsHot => _lossBuffer.IsFull;

    /// <summary>
    /// Computes Pseudo-Huber loss: δ² * (√(1 + (x/δ)²) - 1)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double PseudoHuberLoss(double x)
    {
        double ratio = x / _delta;
        double ratioSq = ratio * ratio;
        return _deltaSquared * (Math.Sqrt(1.0 + ratioSq) - 1.0);
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
        double loss = PseudoHuberLoss(error);

        if (isNew)
        {
            _p_state = _state;

            double removedLoss = _lossBuffer.Count == _lossBuffer.Capacity ? _lossBuffer.Oldest : 0.0;
            _state.LossSum = _state.LossSum - removedLoss + loss;
            _lossBuffer.Add(loss);

            _state.TickCount++;
            if (_lossBuffer.IsFull && _state.TickCount >= ResyncInterval)
            {
                _state.TickCount = 0;
                _state.LossSum = _lossBuffer.RecalculateSum();
            }
        }
        else
        {
            _state = _p_state;

            double removedLoss = _lossBuffer.Count == _lossBuffer.Capacity ? _lossBuffer.Oldest : 0.0;
            _state.LossSum = _state.LossSum - removedLoss + loss;
            _lossBuffer.UpdateNewest(loss);
            _state.LossSum = _lossBuffer.RecalculateSum();
        }

        // Mean Pseudo-Huber Loss
        double result = _lossBuffer.Count > 0 ? _state.LossSum / _lossBuffer.Count : 0.0;

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
        throw new NotSupportedException("PseudoHuber requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("PseudoHuber requires two inputs. Use Calculate(actualSeries, predictedSeries, period, delta).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("PseudoHuber requires two inputs.");
    }

    public override void Reset()
    {
        _lossBuffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    public static TSeries Calculate(TSeries actual, TSeries predicted, int period, double delta = DefaultDelta)
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
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period, double delta = DefaultDelta)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException("All spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (delta <= 0)
            throw new ArgumentException("Delta must be positive", nameof(delta));

        int len = actual.Length;
        if (len == 0) return;

        double deltaSquared = delta * delta;

        const int StackAllocThreshold = 256;
        Span<double> lossBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double lossSum = 0;
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
            double ratio = error / delta;
            double ratioSq = ratio * ratio;
            double loss = deltaSquared * (Math.Sqrt(1.0 + ratioSq) - 1.0);

            lossSum += loss;
            lossBuffer[i] = loss;

            output[i] = lossSum / (i + 1);
        }

        int tickCount = 0;
        for (; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double error = act - pred;
            double ratio = error / delta;
            double ratioSq = ratio * ratio;
            double loss = deltaSquared * (Math.Sqrt(1.0 + ratioSq) - 1.0);

            lossSum = lossSum - lossBuffer[bufferIndex] + loss;
            lossBuffer[bufferIndex] = loss;

            bufferIndex++;
            if (bufferIndex >= period) bufferIndex = 0;

            output[i] = lossSum / period;

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcSum = 0;
                for (int k = 0; k < period; k++)
                    recalcSum += lossBuffer[k];
                lossSum = recalcSum;
            }
        }
    }
}
