using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TheilU: Theil's U Statistic (U1)
/// </summary>
/// <remarks>
/// Theil's U is a relative measure of forecasting accuracy that normalizes
/// the RMSE by the sum of squared actual and predicted values. Values range
/// from 0 (perfect forecast) to 1 (naive forecast), with values above 1
/// indicating the forecast is worse than simply predicting no change.
///
/// Formula:
/// U = √(Σ(predicted - actual)²) / √(Σactual² + Σpredicted²)
///
/// Key properties:
/// - Scale-independent (bounded 0-1 for reasonable forecasts)
/// - U = 0: Perfect forecast
/// - U = 1: Forecast as good as naive (no-change) forecast
/// - U > 1: Forecast worse than naive forecast
/// - Useful for comparing forecasting methods
///
/// Uses Kahan compensated summation to prevent floating-point drift without periodic resync.
/// </remarks>
[SkipLocalsInit]
public sealed class TheilU : AbstractBase
{
    private readonly RingBuffer _sqErrorBuffer;
    private readonly RingBuffer _sqActualBuffer;
    private readonly RingBuffer _sqPredBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double SqErrorSum, double SqActualSum, double SqPredSum, double SqErrorComp, double SqActualComp, double SqPredComp, double LastValidActual, double LastValidPredicted);
    private State _state;
    private State _p_state;

    public TheilU(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _sqErrorBuffer = new RingBuffer(period);
        _sqActualBuffer = new RingBuffer(period);
        _sqPredBuffer = new RingBuffer(period);
        Name = $"TheilU({period})";
        WarmupPeriod = period;
    }

    public override bool IsHot => _sqErrorBuffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, bool isNew = true)
    {
        return UpdateCore(actual.AsDateTime, actual.Value, predicted.Value, isNew);
    }

    /// <summary>
    /// Non-allocating Update overload that accepts primitive values.
    /// Avoids TValue allocation in hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double actual, double predicted, bool isNew = true)
    {
        return UpdateCore(DateTime.MinValue, actual, predicted, isNew);
    }

    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("TheilU requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("TheilU requires two inputs. Use Batch(actualSeries, predictedSeries, period).");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(DateTime time, double actualVal, double predictedVal, bool isNew)
    {
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

        double error = predictedVal - actualVal;
        double sqError = error * error;
        double sqActual = actualVal * actualVal;
        double sqPred = predictedVal * predictedVal;

        if (isNew)
        {
            _p_state = _state;

            double removedSqError = _sqErrorBuffer.Count == _sqErrorBuffer.Capacity ? _sqErrorBuffer.Oldest : 0.0;
            {
                double delta = sqError - removedSqError;
                double y = delta - _state.SqErrorComp;
                double t = _state.SqErrorSum + y;
                _state.SqErrorComp = (t - _state.SqErrorSum) - y;
                _state.SqErrorSum = t;
            }
            _sqErrorBuffer.Add(sqError);

            double removedSqActual = _sqActualBuffer.Count == _sqActualBuffer.Capacity ? _sqActualBuffer.Oldest : 0.0;
            {
                double delta = sqActual - removedSqActual;
                double y = delta - _state.SqActualComp;
                double t = _state.SqActualSum + y;
                _state.SqActualComp = (t - _state.SqActualSum) - y;
                _state.SqActualSum = t;
            }
            _sqActualBuffer.Add(sqActual);

            double removedSqPred = _sqPredBuffer.Count == _sqPredBuffer.Capacity ? _sqPredBuffer.Oldest : 0.0;
            {
                double delta = sqPred - removedSqPred;
                double y = delta - _state.SqPredComp;
                double t = _state.SqPredSum + y;
                _state.SqPredComp = (t - _state.SqPredSum) - y;
                _state.SqPredSum = t;
            }
            _sqPredBuffer.Add(sqPred);
        }
        else
        {
            _state = _p_state;

            // Bar correction: update buffer and recalculate sums
            _sqErrorBuffer.UpdateNewest(sqError);
            _sqActualBuffer.UpdateNewest(sqActual);
            _sqPredBuffer.UpdateNewest(sqPred);

            _state.SqErrorSum = _sqErrorBuffer.RecalculateSum();
            _state.SqActualSum = _sqActualBuffer.RecalculateSum();
            _state.SqPredSum = _sqPredBuffer.RecalculateSum();
        }

        // TheilU = √(Σ(pred-act)²) / √(Σact² + Σpred²)
        double denominator = Math.Sqrt(_state.SqActualSum + _state.SqPredSum);
        double result = denominator > 1e-10 ? Math.Sqrt(_state.SqErrorSum) / denominator : 0.0;

        Last = new TValue(time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("TheilU requires two inputs.");
    }

    public override void Reset()
    {
        _sqErrorBuffer.Clear();
        _sqActualBuffer.Clear();
        _sqPredBuffer.Clear();
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
        Span<double> sqErrorBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];
        Span<double> sqActualBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];
        Span<double> sqPredBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double sqErrorSum = 0;
        double sqActualSum = 0;
        double sqPredSum = 0;
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

            double error = pred - act;
            double sqError = error * error;
            double sqActual = act * act;
            double sqPred = pred * pred;

            sqErrorSum += sqError;
            sqActualSum += sqActual;
            sqPredSum += sqPred;
            sqErrorBuffer[i] = sqError;
            sqActualBuffer[i] = sqActual;
            sqPredBuffer[i] = sqPred;

            double denom = Math.Sqrt(sqActualSum + sqPredSum);
            output[i] = denom > 1e-10 ? Math.Sqrt(sqErrorSum) / denom : 0.0;
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

            double error = pred - act;
            double sqError = error * error;
            double sqActual = act * act;
            double sqPred = pred * pred;

            // Use FMA for sliding-window updates
            sqErrorSum = Math.FusedMultiplyAdd(1.0, sqError, Math.FusedMultiplyAdd(-1.0, sqErrorBuffer[bufferIndex], sqErrorSum));
            sqActualSum = Math.FusedMultiplyAdd(1.0, sqActual, Math.FusedMultiplyAdd(-1.0, sqActualBuffer[bufferIndex], sqActualSum));
            sqPredSum = Math.FusedMultiplyAdd(1.0, sqPred, Math.FusedMultiplyAdd(-1.0, sqPredBuffer[bufferIndex], sqPredSum));

            sqErrorBuffer[bufferIndex] = sqError;
            sqActualBuffer[bufferIndex] = sqActual;
            sqPredBuffer[bufferIndex] = sqPred;

            bufferIndex++;
            if (bufferIndex >= period)
            {
                bufferIndex = 0;
            }

            double denom = Math.Sqrt(sqActualSum + sqPredSum);
            output[i] = denom > 1e-10 ? Math.Sqrt(sqErrorSum) / denom : 0.0;
        }
    }

    public static (TSeries Results, TheilU Indicator) Calculate(TSeries actual, TSeries predicted, int period)
    {
        var indicator = new TheilU(period);
        TSeries results = Batch(actual, predicted, period);
        return (results, indicator);
    }
}
