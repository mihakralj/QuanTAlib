using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RSE: Relative Squared Error
/// </summary>
/// <remarks>
/// RSE measures the total squared error relative to the total squared error of
/// a simple predictor (the mean). It provides a normalized measure that indicates
/// how well the model performs compared to predicting the mean for all values.
///
/// Formula:
/// RSE = Σ(actual - predicted)² / Σ(actual - mean(actual))²
///
/// Key properties:
/// - RSE &lt; 1 means better than mean predictor
/// - RSE = 1 means same as mean predictor
/// - RSE &gt; 1 means worse than mean predictor
/// - Related to R² by: R² = 1 - RSE
/// </remarks>
[SkipLocalsInit]
public sealed class Rse : AbstractBase
{
    private readonly RingBuffer _actualBuffer;
    private readonly RingBuffer _sqErrorBuffer;
    private readonly RingBuffer _sqBaselineBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double ActualSum,
        double SqErrorSum,
        double SqBaselineSum,
        double LastValidActual,
        double LastValidPredicted,
        int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    public Rse(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _actualBuffer = new RingBuffer(period);
        _sqErrorBuffer = new RingBuffer(period);
        _sqBaselineBuffer = new RingBuffer(period);
        Name = $"Rse({period})";
        WarmupPeriod = period;
    }

    public override bool IsHot => _actualBuffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, bool isNew = true)
    {
        double actualVal = actual.Value;
        double predictedVal = predicted.Value;

        // Restore state FIRST when isNew=false (before any state mutations)
        if (!isNew)
        {
            _state = _p_state;
        }

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

            // Calculate mean and baseline error
            double mean = _state.ActualSum / _actualBuffer.Count;
            double error = actualVal - predictedVal;
            double baselineError = actualVal - mean;
            double sqError = error * error;
            double sqBaseline = baselineError * baselineError;

            // Update squared error buffer
            double removedError = _sqErrorBuffer.Count == _sqErrorBuffer.Capacity ? _sqErrorBuffer.Oldest : 0.0;
            _state.SqErrorSum = _state.SqErrorSum - removedError + sqError;
            _sqErrorBuffer.Add(sqError);

            // Update squared baseline buffer
            double removedBaseline = _sqBaselineBuffer.Count == _sqBaselineBuffer.Capacity ? _sqBaselineBuffer.Oldest : 0.0;
            _state.SqBaselineSum = _state.SqBaselineSum - removedBaseline + sqBaseline;
            _sqBaselineBuffer.Add(sqBaseline);

            _state.TickCount++;
            if (_actualBuffer.IsFull && _state.TickCount >= ResyncInterval)
            {
                _state.TickCount = 0;
                _state.ActualSum = _actualBuffer.RecalculateSum();
                _state.SqErrorSum = _sqErrorBuffer.RecalculateSum();
                _state.SqBaselineSum = _sqBaselineBuffer.RecalculateSum();
            }
        }
        else
        {
            // Update actual buffer - incremental update is sufficient
            double removedActual = _actualBuffer.Count == _actualBuffer.Capacity ? _actualBuffer.Oldest : 0.0;
            _state.ActualSum = _state.ActualSum - removedActual + actualVal;
            _actualBuffer.UpdateNewest(actualVal);

            // Calculate mean and errors
            double mean = _state.ActualSum / _actualBuffer.Count;
            double error = actualVal - predictedVal;
            double baselineError = actualVal - mean;
            double sqError = error * error;
            double sqBaseline = baselineError * baselineError;

            // Update squared error buffer - incremental update
            double removedError = _sqErrorBuffer.Count == _sqErrorBuffer.Capacity ? _sqErrorBuffer.Oldest : 0.0;
            _state.SqErrorSum = _state.SqErrorSum - removedError + sqError;
            _sqErrorBuffer.UpdateNewest(sqError);

            // Update squared baseline buffer - incremental update
            double removedBaseline = _sqBaselineBuffer.Count == _sqBaselineBuffer.Capacity ? _sqBaselineBuffer.Oldest : 0.0;
            _state.SqBaselineSum = _state.SqBaselineSum - removedBaseline + sqBaseline;
            _sqBaselineBuffer.UpdateNewest(sqBaseline);
        }

        double result = _state.SqBaselineSum > 1e-10 ? _state.SqErrorSum / _state.SqBaselineSum : 1.0;

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
        throw new NotSupportedException("RSE requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("RSE requires two inputs. Use Batch(actualSeries, predictedSeries, period).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("RSE requires two inputs.");
    }

    public override void Reset()
    {
        _actualBuffer.Clear();
        _sqErrorBuffer.Clear();
        _sqBaselineBuffer.Clear();
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
        Span<double> sqErrorBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];
        Span<double> sqBaselineBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double actualSum = 0;
        double sqErrorSum = 0;
        double sqBaselineSum = 0;
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
            double error = act - pred;
            double baselineError = act - mean;
            double sqError = error * error;
            double sqBaseline = baselineError * baselineError;

            sqErrorSum += sqError;
            sqBaselineSum += sqBaseline;
            sqErrorBuffer[i] = sqError;
            sqBaselineBuffer[i] = sqBaseline;

            output[i] = sqBaselineSum > 1e-10 ? sqErrorSum / sqBaselineSum : 1.0;
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
            double error = act - pred;
            double baselineError = act - mean;
            double sqError = error * error;
            double sqBaseline = baselineError * baselineError;

            sqErrorSum = sqErrorSum - sqErrorBuffer[bufferIndex] + sqError;
            sqBaselineSum = sqBaselineSum - sqBaselineBuffer[bufferIndex] + sqBaseline;
            sqErrorBuffer[bufferIndex] = sqError;
            sqBaselineBuffer[bufferIndex] = sqBaseline;

            bufferIndex++;
            if (bufferIndex >= period)
            {
                bufferIndex = 0;
            }

            output[i] = sqBaselineSum > 1e-10 ? sqErrorSum / sqBaselineSum : 1.0;

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcActual = 0, recalcError = 0, recalcBaseline = 0;
                for (int k = 0; k < period; k++)
                {
                    recalcActual += actualBuffer[k];
                    recalcError += sqErrorBuffer[k];
                    recalcBaseline += sqBaselineBuffer[k];
                }
                actualSum = recalcActual;
                sqErrorSum = recalcError;
                sqBaselineSum = recalcBaseline;
            }
        }
    }

    public static (TSeries Results, Rse Indicator) Calculate(TSeries actual, TSeries predicted, int period)
    {
        var indicator = new Rse(period);
        TSeries results = Batch(actual, predicted, period);
        return (results, indicator);
    }
}