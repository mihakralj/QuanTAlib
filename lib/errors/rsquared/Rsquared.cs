using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// R²: R-squared (Coefficient of Determination)
/// </summary>
/// <remarks>
/// R² measures the proportion of variance in the actual values that is
/// predictable from the predicted values. It indicates how well the predictions
/// approximate the actual data points.
///
/// Formula:
/// R² = 1 - (RSS / TSS) = 1 - RSE
/// where RSS = Σ(actual - predicted)², TSS = Σ(actual - mean(actual))²
///
/// Key properties:
/// - R² = 1 means perfect predictions
/// - R² = 0 means predictions equal mean predictor
/// - R² &lt; 0 means predictions worse than mean predictor
/// - Range: (-∞, 1]
/// </remarks>
[SkipLocalsInit]
public sealed class Rsquared : AbstractBase
{
    private readonly RingBuffer _actualBuffer;
    private readonly RingBuffer _sqResidualBuffer;
    private readonly RingBuffer _sqTotalBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double ActualSum,
        double SqResidualSum,
        double SqTotalSum,
        double LastValidActual,
        double LastValidPredicted,
        int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    public Rsquared(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _actualBuffer = new RingBuffer(period);
        _sqResidualBuffer = new RingBuffer(period);
        _sqTotalBuffer = new RingBuffer(period);
        Name = $"R²({period})";
        WarmupPeriod = period;
    }

    public override bool IsHot => _actualBuffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, bool isNew = true)
    {
        double actualVal = actual.Value;
        double predictedVal = predicted.Value;

        if (!double.IsFinite(actualVal))
        {
            actualVal = double.IsFinite(_state.LastValidActual) ? _state.LastValidActual : 0.0;
        }
        else
        {
            _state.LastValidActual = actualVal;
        }

        if (!double.IsFinite(predictedVal))
        {
            predictedVal = double.IsFinite(_state.LastValidPredicted) ? _state.LastValidPredicted : 0.0;
        }
        else
        {
            _state.LastValidPredicted = predictedVal;
        }

        if (isNew)
        {
            _p_state = _state;

            // Update actual buffer for mean calculation
            double removedActual = _actualBuffer.Count == _actualBuffer.Capacity ? _actualBuffer.Oldest : 0.0;
            _state.ActualSum = _state.ActualSum - removedActual + actualVal;
            _actualBuffer.Add(actualVal);

            // Calculate mean and errors
            double mean = _state.ActualSum / _actualBuffer.Count;
            double residual = actualVal - predictedVal;
            double totalDev = actualVal - mean;
            double sqResidual = residual * residual;
            double sqTotal = totalDev * totalDev;

            // Update squared residual buffer (RSS)
            double removedResidual = _sqResidualBuffer.Count == _sqResidualBuffer.Capacity ? _sqResidualBuffer.Oldest : 0.0;
            _state.SqResidualSum = _state.SqResidualSum - removedResidual + sqResidual;
            _sqResidualBuffer.Add(sqResidual);

            // Update squared total buffer (TSS)
            double removedTotal = _sqTotalBuffer.Count == _sqTotalBuffer.Capacity ? _sqTotalBuffer.Oldest : 0.0;
            _state.SqTotalSum = _state.SqTotalSum - removedTotal + sqTotal;
            _sqTotalBuffer.Add(sqTotal);

            _state.TickCount++;
            if (_actualBuffer.IsFull && _state.TickCount >= ResyncInterval)
            {
                _state.TickCount = 0;
                _state.ActualSum = _actualBuffer.RecalculateSum();
                _state.SqResidualSum = _sqResidualBuffer.RecalculateSum();
                _state.SqTotalSum = _sqTotalBuffer.RecalculateSum();
            }
        }
        else
        {
            _state = _p_state;

            // Bar correction: update buffers and recalculate sums
            _actualBuffer.UpdateNewest(actualVal);

            // Calculate mean and errors
            double mean = _actualBuffer.RecalculateSum() / _actualBuffer.Count;
            _state.ActualSum = _actualBuffer.RecalculateSum();

            double residual = actualVal - predictedVal;
            double totalDev = actualVal - mean;
            double sqResidual = residual * residual;
            double sqTotal = totalDev * totalDev;

            _sqResidualBuffer.UpdateNewest(sqResidual);
            _sqTotalBuffer.UpdateNewest(sqTotal);

            _state.SqResidualSum = _sqResidualBuffer.RecalculateSum();
            _state.SqTotalSum = _sqTotalBuffer.RecalculateSum();
        }

        // R² = 1 - RSS/TSS
        double result = _state.SqTotalSum > 1e-10 ? 1.0 - (_state.SqResidualSum / _state.SqTotalSum) : 1.0;

        Last = new TValue(actual.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double actual, double predicted, bool isNew = true)
    {
        return Update(new TValue(DateTime.MinValue, actual), new TValue(DateTime.MinValue, predicted), isNew);
    }

    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("R² requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("R² requires two inputs. Use Batch(actualSeries, predictedSeries, period).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("R² requires two inputs.");
    }

    public override void Reset()
    {
        _actualBuffer.Clear();
        _sqResidualBuffer.Clear();
        _sqTotalBuffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    public static TSeries Batch(TSeries actual, TSeries predicted, int period)
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

        Batch(actual.Values, predicted.Values, vSpan, period);
        actual.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
        {
            throw new ArgumentException("All spans must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = actual.Length;
        if (len == 0)
        {
            return;
        }

        const int StackAllocThreshold = 256;
        Span<double> actualBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];
        Span<double> sqResidualBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];
        Span<double> sqTotalBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double actualSum = 0;
        double sqResidualSum = 0;
        double sqTotalSum = 0;
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

            actualSum += act;
            actualBuffer[i] = act;

            double mean = actualSum / (i + 1);
            double residual = act - pred;
            double totalDev = act - mean;
            double sqResidual = residual * residual;
            double sqTotal = totalDev * totalDev;

            sqResidualSum += sqResidual;
            sqTotalSum += sqTotal;
            sqResidualBuffer[i] = sqResidual;
            sqTotalBuffer[i] = sqTotal;

            output[i] = sqTotalSum > 1e-10 ? 1.0 - (sqResidualSum / sqTotalSum) : 1.0;
        }

        int tickCount = 0;
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

            actualSum = actualSum - actualBuffer[bufferIndex] + act;
            actualBuffer[bufferIndex] = act;

            double mean = actualSum / period;
            double residual = act - pred;
            double totalDev = act - mean;
            double sqResidual = residual * residual;
            double sqTotal = totalDev * totalDev;

            sqResidualSum = sqResidualSum - sqResidualBuffer[bufferIndex] + sqResidual;
            sqTotalSum = sqTotalSum - sqTotalBuffer[bufferIndex] + sqTotal;
            sqResidualBuffer[bufferIndex] = sqResidual;
            sqTotalBuffer[bufferIndex] = sqTotal;

            bufferIndex++;
            if (bufferIndex >= period)
            {
                bufferIndex = 0;
            }

            output[i] = sqTotalSum > 1e-10 ? 1.0 - (sqResidualSum / sqTotalSum) : 1.0;

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcActual = 0, recalcResidual = 0, recalcTotal = 0;
                for (int k = 0; k < period; k++)
                {
                    recalcActual += actualBuffer[k];
                    recalcResidual += sqResidualBuffer[k];
                    recalcTotal += sqTotalBuffer[k];
                }
                actualSum = recalcActual;
                sqResidualSum = recalcResidual;
                sqTotalSum = recalcTotal;
            }
        }
    }

    public static (TSeries Results, Rsquared Indicator) Calculate(TSeries actual, TSeries predicted, int period)
    {
        var indicator = new Rsquared(period);
        TSeries results = Batch(actual, predicted, period);
        return (results, indicator);
    }
}