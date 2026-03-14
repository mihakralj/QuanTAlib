using System.Buffers;
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
public sealed class QuantileLoss : BiInputIndicatorBase
{
    /// <summary>
    /// Creates QuantileLoss with specified period and quantile.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    /// <param name="quantile">Quantile value between 0 and 1 exclusive (default 0.5)</param>
    public QuantileLoss(int period, double quantile = 0.5)
        : base(period, $"QuantileLoss({period},{quantile:F2})")
    {
        if (quantile <= 0.0 || quantile >= 1.0)
        {
            throw new ArgumentException("Quantile must be between 0 and 1 (exclusive)", nameof(quantile));
        }

        Quantile = quantile;
    }

    /// <summary>
    /// The quantile parameter (0 &lt; q &lt; 1).
    /// </summary>
    public double Quantile { get; }

    /// <summary>
    /// Computes quantile loss for a single error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double ComputeError(double actual, double predicted)
    {
        double diff = actual - predicted;
        return diff >= 0 ? Quantile * diff : (Quantile - 1.0) * diff;
    }

    public static TSeries Batch(TSeries actual, TSeries predicted, int period, double quantile = 0.5)
    {
        if (actual.Count != predicted.Count)
        {
            throw new ArgumentException("Actual and predicted series must have the same length", nameof(predicted));
        }

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
        {
            throw new ArgumentException("All spans must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (quantile <= 0.0 || quantile >= 1.0)
        {
            throw new ArgumentException("Quantile must be between 0 and 1 (exclusive)", nameof(quantile));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        const int StackAllocThreshold = 256;
        Span<double> lossBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double lossSum = 0;
        double lastValidActual = 0;
        double lastValidPredicted = 0;

        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(actual[k]))
            {
                lastValidActual = actual[k];
                break;
            }
        }
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(predicted[k]))
            {
                lastValidPredicted = predicted[k];
                break;
            }
        }

        int bufferIndex = 0;
        int i = 0;

        int warmupEnd = Math.Min(period, len);
        for (; i < warmupEnd; i++)
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

            double diff = act - pred;
            double loss = diff >= 0 ? quantile * diff : (quantile - 1.0) * diff;

            lossSum += loss;
            lossBuffer[i] = loss;

            output[i] = lossSum / (i + 1);
        }

        for (; i < len; i++)
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

            double diff = act - pred;
            double loss = diff >= 0 ? quantile * diff : (quantile - 1.0) * diff;

            lossSum = lossSum - lossBuffer[bufferIndex] + loss;
            lossBuffer[bufferIndex] = loss;

            bufferIndex++;
            if (bufferIndex >= period)
            {
                bufferIndex = 0;
            }

            output[i] = lossSum / period;
        }
    }

    public static (TSeries Results, QuantileLoss Indicator) Calculate(TSeries actual, TSeries predicted, int period, double quantile = 0.5)
    {
        var indicator = new QuantileLoss(period, quantile);
        TSeries results = Batch(actual, predicted, period, quantile);
        return (results, indicator);
    }
}