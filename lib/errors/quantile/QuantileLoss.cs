using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// QuantileLoss: Quantile Loss (Pinball Loss)
/// </summary>
/// <remarks>
/// Quantile Loss (also known as Pinball Loss) is used for quantile regression.
/// It asymmetrically penalizes over- and under-predictions based on the quantile
/// parameter. This is useful for generating prediction intervals.
///
/// Formula:
/// QuantileLoss = (1/n) * Σ max(q*(actual - predicted), (q-1)*(actual - predicted))
///
/// Which simplifies to:
/// - If actual >= predicted: q * (actual - predicted)
/// - If actual < predicted: (1-q) * (predicted - actual)
///
/// Key properties:
/// - Asymmetric penalty based on quantile parameter q
/// - q = 0.5 gives MAE (median regression)
/// - q > 0.5 penalizes under-prediction more heavily
/// - q < 0.5 penalizes over-prediction more heavily
/// - Used for prediction intervals (e.g., q=0.1 and q=0.9 for 80% interval)
/// </remarks>
[SkipLocalsInit]
public sealed class QuantileLoss : AbstractBase
{
    private readonly RingBuffer _lossBuffer;
    private readonly double _quantile;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LossSum, double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    public QuantileLoss(int period, double quantile = 0.5)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (quantile <= 0.0 || quantile >= 1.0)
            throw new ArgumentException("Quantile must be between 0 and 1 (exclusive)", nameof(quantile));

        _lossBuffer = new RingBuffer(period);
        _quantile = quantile;
        Name = $"QuantileLoss({period},{quantile:F2})";
        WarmupPeriod = period;
    }

    public double Quantile => _quantile;
    public override bool IsHot => _lossBuffer.IsFull;

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

        // Pinball loss: max(q*(y-p), (q-1)*(y-p))
        double diff = actualVal - predictedVal;
        double loss = diff >= 0 ? _quantile * diff : (_quantile - 1.0) * diff;

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

        // QuantileLoss = (1/n) * Σ loss
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
        throw new NotSupportedException("QuantileLoss requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("QuantileLoss requires two inputs. Use Calculate(actualSeries, predictedSeries, period, quantile).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("QuantileLoss requires two inputs.");
    }

    public override void Reset()
    {
        _lossBuffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    public static TSeries Calculate(TSeries actual, TSeries predicted, int period, double quantile = 0.5)
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

        Batch(actual.Values, predicted.Values, vSpan, period, quantile);
        actual.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period, double quantile = 0.5)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException("All spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (quantile <= 0.0 || quantile >= 1.0)
            throw new ArgumentException("Quantile must be between 0 and 1 (exclusive)", nameof(quantile));

        int len = actual.Length;
        if (len == 0) return;

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

            double diff = act - pred;
            double loss = diff >= 0 ? quantile * diff : (quantile - 1.0) * diff;

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

            double diff = act - pred;
            double loss = diff >= 0 ? quantile * diff : (quantile - 1.0) * diff;

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
