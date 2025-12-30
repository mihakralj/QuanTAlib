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
/// </remarks>
[SkipLocalsInit]
public sealed class TheilU : AbstractBase
{
    private readonly RingBuffer _sqErrorBuffer;
    private readonly RingBuffer _sqActualBuffer;
    private readonly RingBuffer _sqPredBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double SqErrorSum, double SqActualSum, double SqPredSum, double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    public TheilU(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

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

        double error = predictedVal - actualVal;
        double sqError = error * error;
        double sqActual = actualVal * actualVal;
        double sqPred = predictedVal * predictedVal;

        if (isNew)
        {
            _p_state = _state;

            double removedSqError = _sqErrorBuffer.Count == _sqErrorBuffer.Capacity ? _sqErrorBuffer.Oldest : 0.0;
            _state.SqErrorSum = _state.SqErrorSum - removedSqError + sqError;
            _sqErrorBuffer.Add(sqError);

            double removedSqActual = _sqActualBuffer.Count == _sqActualBuffer.Capacity ? _sqActualBuffer.Oldest : 0.0;
            _state.SqActualSum = _state.SqActualSum - removedSqActual + sqActual;
            _sqActualBuffer.Add(sqActual);

            double removedSqPred = _sqPredBuffer.Count == _sqPredBuffer.Capacity ? _sqPredBuffer.Oldest : 0.0;
            _state.SqPredSum = _state.SqPredSum - removedSqPred + sqPred;
            _sqPredBuffer.Add(sqPred);

            _state.TickCount++;
            if (_sqErrorBuffer.IsFull && _state.TickCount >= ResyncInterval)
            {
                _state.TickCount = 0;
                _state.SqErrorSum = _sqErrorBuffer.RecalculateSum();
                _state.SqActualSum = _sqActualBuffer.RecalculateSum();
                _state.SqPredSum = _sqPredBuffer.RecalculateSum();
            }
        }
        else
        {
            _state = _p_state;

            double removedSqError = _sqErrorBuffer.Count == _sqErrorBuffer.Capacity ? _sqErrorBuffer.Oldest : 0.0;
            _state.SqErrorSum = _state.SqErrorSum - removedSqError + sqError;
            _sqErrorBuffer.UpdateNewest(sqError);
            _state.SqErrorSum = _sqErrorBuffer.RecalculateSum();

            double removedSqActual = _sqActualBuffer.Count == _sqActualBuffer.Capacity ? _sqActualBuffer.Oldest : 0.0;
            _state.SqActualSum = _state.SqActualSum - removedSqActual + sqActual;
            _sqActualBuffer.UpdateNewest(sqActual);
            _state.SqActualSum = _sqActualBuffer.RecalculateSum();

            double removedSqPred = _sqPredBuffer.Count == _sqPredBuffer.Capacity ? _sqPredBuffer.Oldest : 0.0;
            _state.SqPredSum = _state.SqPredSum - removedSqPred + sqPred;
            _sqPredBuffer.UpdateNewest(sqPred);
            _state.SqPredSum = _sqPredBuffer.RecalculateSum();
        }

        // TheilU = √(Σ(pred-act)²) / √(Σact² + Σpred²)
        double denominator = Math.Sqrt(_state.SqActualSum + _state.SqPredSum);
        double result = denominator > 1e-10 ? Math.Sqrt(_state.SqErrorSum) / denominator : 0.0;

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
        throw new NotSupportedException("TheilU requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("TheilU requires two inputs. Use Calculate(actualSeries, predictedSeries, period).");
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

        int tickCount = 0;
        for (; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double error = pred - act;
            double sqError = error * error;
            double sqActual = act * act;
            double sqPred = pred * pred;

            sqErrorSum = sqErrorSum - sqErrorBuffer[bufferIndex] + sqError;
            sqActualSum = sqActualSum - sqActualBuffer[bufferIndex] + sqActual;
            sqPredSum = sqPredSum - sqPredBuffer[bufferIndex] + sqPred;
            sqErrorBuffer[bufferIndex] = sqError;
            sqActualBuffer[bufferIndex] = sqActual;
            sqPredBuffer[bufferIndex] = sqPred;

            bufferIndex++;
            if (bufferIndex >= period) bufferIndex = 0;

            double denom = Math.Sqrt(sqActualSum + sqPredSum);
            output[i] = denom > 1e-10 ? Math.Sqrt(sqErrorSum) / denom : 0.0;

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcSqError = 0, recalcSqActual = 0, recalcSqPred = 0;
                for (int k = 0; k < period; k++)
                {
                    recalcSqError += sqErrorBuffer[k];
                    recalcSqActual += sqActualBuffer[k];
                    recalcSqPred += sqPredBuffer[k];
                }
                sqErrorSum = recalcSqError;
                sqActualSum = recalcSqActual;
                sqPredSum = recalcSqPred;
            }
        }
    }
}
