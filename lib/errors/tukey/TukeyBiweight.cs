using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TukeyBiweight: Tukey's Biweight (Bisquare) Loss
/// </summary>
/// <remarks>
/// Tukey's Biweight is a robust loss function that completely rejects outliers
/// beyond a threshold c. Unlike Huber loss which downweights outliers, Tukey's
/// biweight assigns zero weight to extreme outliers, making it highly resistant
/// to contaminated data.
///
/// Formula:
/// ρ(x) = (c²/6) * (1 - (1 - (x/c)²)³)  for |x| ≤ c
/// ρ(x) = c²/6                           for |x| > c
///
/// Key properties:
/// - Completely rejects outliers beyond threshold c
/// - Redescending: influence function goes to zero for large errors
/// - Common c values: 4.685 (95% efficiency), 6.0 (more permissive)
/// - More robust than Huber for heavily contaminated data
/// - Smooth and differentiable everywhere
/// </remarks>
[SkipLocalsInit]
public sealed class TukeyBiweight : AbstractBase
{
    private readonly RingBuffer _lossBuffer;
    private readonly double _c;
    private readonly double _cSquaredOver6;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LossSum, double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;
    private const double DefaultC = 4.685; // 95% efficiency for normal distribution

    public TukeyBiweight(int period, double c = DefaultC)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (c <= 0)
            throw new ArgumentException("Threshold c must be positive", nameof(c));

        _lossBuffer = new RingBuffer(period);
        _c = c;
        _cSquaredOver6 = (c * c) / 6.0;
        Name = $"TukeyBiweight({period},{c:F3})";
        WarmupPeriod = period;
    }

    public double C => _c;
    public override bool IsHot => _lossBuffer.IsFull;

    /// <summary>
    /// Computes Tukey's biweight loss function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double BiweightLoss(double x)
    {
        double absX = Math.Abs(x);
        if (absX > _c)
            return _cSquaredOver6;

        double ratio = x / _c;
        double ratioSq = ratio * ratio;
        double oneMinusRatioSq = 1.0 - ratioSq;
        double cubed = oneMinusRatioSq * oneMinusRatioSq * oneMinusRatioSq;
        return _cSquaredOver6 * (1.0 - cubed);
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
        double loss = BiweightLoss(error);

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

        // Mean Tukey Biweight Loss
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
        throw new NotSupportedException("TukeyBiweight requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("TukeyBiweight requires two inputs. Use Calculate(actualSeries, predictedSeries, period, c).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("TukeyBiweight requires two inputs.");
    }

    public override void Reset()
    {
        _lossBuffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    public static TSeries Calculate(TSeries actual, TSeries predicted, int period, double c = DefaultC)
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

        Batch(actual.Values, predicted.Values, vSpan, period, c);
        actual.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period, double c = DefaultC)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException("All spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (c <= 0)
            throw new ArgumentException("Threshold c must be positive", nameof(c));

        int len = actual.Length;
        if (len == 0) return;

        double cSquaredOver6 = (c * c) / 6.0;

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
            double loss;
            double absError = Math.Abs(error);
            if (absError > c)
            {
                loss = cSquaredOver6;
            }
            else
            {
                double ratio = error / c;
                double ratioSq = ratio * ratio;
                double oneMinusRatioSq = 1.0 - ratioSq;
                double cubed = oneMinusRatioSq * oneMinusRatioSq * oneMinusRatioSq;
                loss = cSquaredOver6 * (1.0 - cubed);
            }

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
            double loss;
            double absError = Math.Abs(error);
            if (absError > c)
            {
                loss = cSquaredOver6;
            }
            else
            {
                double ratio = error / c;
                double ratioSq = ratio * ratio;
                double oneMinusRatioSq = 1.0 - ratioSq;
                double cubed = oneMinusRatioSq * oneMinusRatioSq * oneMinusRatioSq;
                loss = cSquaredOver6 * (1.0 - cubed);
            }

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
