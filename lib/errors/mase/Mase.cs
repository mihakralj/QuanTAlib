using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MASE: Mean Absolute Scaled Error
/// </summary>
/// <remarks>
/// MASE scales the mean absolute error by the average absolute difference of the
/// naive forecast (using previous value as prediction). This normalization makes
/// the error interpretable relative to the inherent difficulty of predicting the series.
///
/// Formula:
/// MASE = MAE / Scale
/// where Scale = (1/(n-1)) * Σ|actual[t] - actual[t-1]|
///
/// Key properties:
/// - Scale-independent through normalization
/// - MASE &lt; 1 means better than naive forecast
/// - MASE = 1 means same as naive forecast
/// - MASE &gt; 1 means worse than naive forecast
/// - Robust to zero actual values (unlike MAPE)
///
/// Uses Kahan compensated summation to prevent floating-point drift without periodic resync.
/// </remarks>
[SkipLocalsInit]
public sealed class Mase : AbstractBase
{
    private readonly RingBuffer _errorBuffer;
    private readonly RingBuffer _scaleBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double ErrorSum,
        double ScaleSum,
        double ErrorComp,
        double ScaleComp,
        double LastValidActual,
        double LastValidPredicted,
        double PrevActual,
        int TickCount);
    private State _state;
    private State _p_state;

    public Mase(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _errorBuffer = new RingBuffer(period);
        _scaleBuffer = new RingBuffer(period);
        _state = new State(0, 0, 0, 0, 0, 0, double.NaN, 0);
        _p_state = new State(0, 0, 0, 0, 0, 0, double.NaN, 0);
        Name = $"Mase({period})";
        WarmupPeriod = period + 1; // Need one extra for scale calculation
    }

    public override bool IsHot => _errorBuffer.IsFull;

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

        double absError = Math.Abs(actualVal - predictedVal);
        double naiveDiff = double.IsFinite(_state.PrevActual) ? Math.Abs(actualVal - _state.PrevActual) : 0.0;

        if (isNew)
        {
            _p_state = _state;

            // Update error buffer — Kahan compensated
            double removedError = _errorBuffer.Count == _errorBuffer.Capacity ? _errorBuffer.Oldest : 0.0;
            {
                double delta = absError - removedError;
                double y = delta - _state.ErrorComp;
                double t = _state.ErrorSum + y;
                _state.ErrorComp = (t - _state.ErrorSum) - y;
                _state.ErrorSum = t;
            }
            _errorBuffer.Add(absError);

            // Update scale buffer — Kahan compensated
            double removedScale = _scaleBuffer.Count == _scaleBuffer.Capacity ? _scaleBuffer.Oldest : 0.0;
            {
                double delta = naiveDiff - removedScale;
                double y = delta - _state.ScaleComp;
                double t = _state.ScaleSum + y;
                _state.ScaleComp = (t - _state.ScaleSum) - y;
                _state.ScaleSum = t;
            }
            _scaleBuffer.Add(naiveDiff);

            _state.PrevActual = actualVal;
            _state.TickCount++;
        }
        else
        {
            _state = _p_state;

            // Bar correction: update buffer and recalculate sums
            _errorBuffer.UpdateNewest(absError);
            _scaleBuffer.UpdateNewest(naiveDiff);

            _state.ErrorSum = _errorBuffer.RecalculateSum();
            _state.ScaleSum = _scaleBuffer.RecalculateSum();

            _state.PrevActual = actualVal;
        }

        int count = _errorBuffer.Count;
        int period = _errorBuffer.Capacity;
        double mae = count > 0 ? _state.ErrorSum / count : absError;
        // During warmup (first period items): scale = ScaleSum / (count-1), matching Batch's scaleSum/i
        // After warmup (item period+1 onward): scale = ScaleSum / period, matching Batch's scaleSum/period
        // TickCount is 1-based (incremented after adding), so use >= period+1 for post-warmup
        double scale;
        if (_state.TickCount > period)
        {
            scale = _state.ScaleSum / period;
        }
        else
        {
            scale = count > 1 ? _state.ScaleSum / (count - 1) : 1.0;
        }

        double result = scale > 1e-10 ? mae / scale : mae;

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
        throw new NotSupportedException("MASE requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("MASE requires two inputs. Use Batch(actualSeries, predictedSeries, period).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("MASE requires two inputs.");
    }

    public override void Reset()
    {
        _errorBuffer.Clear();
        _scaleBuffer.Clear();
        _state = new State(0, 0, 0, 0, 0, 0, double.NaN, 0);
        _p_state = new State(0, 0, 0, 0, 0, 0, double.NaN, 0);
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
        Span<double> errorBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];
        Span<double> scaleBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double errorSum = 0;
        double scaleSum = 0;
        double lastValidActual = 0;
        double lastValidPredicted = 0;
        double prevActual = double.NaN;

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

            double absError = Math.Abs(act - pred);
            double naiveDiff = double.IsFinite(prevActual) ? Math.Abs(act - prevActual) : 0.0;

            errorSum += absError;
            scaleSum += naiveDiff;
            errorBuffer[i] = absError;
            scaleBuffer[i] = naiveDiff;

            double mae = errorSum / (i + 1);
            double scale = (i > 0) ? scaleSum / i : 1.0; // scale starts from second value
            output[i] = scale > 1e-10 ? mae / scale : mae;

            prevActual = act;
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

            double absError = Math.Abs(act - pred);
            double naiveDiff = Math.Abs(act - prevActual);

            errorSum = errorSum - errorBuffer[bufferIndex] + absError;
            scaleSum = scaleSum - scaleBuffer[bufferIndex] + naiveDiff;
            errorBuffer[bufferIndex] = absError;
            scaleBuffer[bufferIndex] = naiveDiff;

            bufferIndex++;
            if (bufferIndex >= period)
            {
                bufferIndex = 0;
            }

            double mae = errorSum / period;
            double scale = scaleSum / period;
            output[i] = scale > 1e-10 ? mae / scale : mae;

            prevActual = act;
        }
    }

    public static (TSeries Results, Mase Indicator) Calculate(TSeries actual, TSeries predicted, int period)
    {
        var indicator = new Mase(period);
        TSeries results = Batch(actual, predicted, period);
        return (results, indicator);
    }
}
