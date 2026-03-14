using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RAE: Relative Absolute Error
/// </summary>
/// <remarks>
/// RAE measures the total absolute error relative to the total absolute error of
/// a simple predictor (the mean). It provides a normalized measure that indicates
/// how well the model performs compared to predicting the mean for all values.
///
/// Formula:
/// RAE = Σ|actual - predicted| / Σ|actual - mean(actual)|
///
/// Key properties:
/// - RAE &lt; 1 means better than mean predictor
/// - RAE = 1 means same as mean predictor
/// - RAE &gt; 1 means worse than mean predictor
/// - Scale-independent ratio
///
/// Uses Kahan compensated summation to prevent floating-point drift without periodic resync.
/// </remarks>
[SkipLocalsInit]
public sealed class Rae : AbstractBase
{
    private readonly RingBuffer _actualBuffer;
    private readonly RingBuffer _absErrorBuffer;
    private readonly RingBuffer _absBaselineBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double ActualSum,
        double AbsErrorSum,
        double AbsBaselineSum,
        double ActualComp,
        double AbsErrorComp,
        double AbsBaselineComp,
        double LastValidActual,
        double LastValidPredicted);
    private State _state;
    private State _p_state;

    public Rae(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _actualBuffer = new RingBuffer(period);
        _absErrorBuffer = new RingBuffer(period);
        _absBaselineBuffer = new RingBuffer(period);
        Name = $"Rae({period})";
        WarmupPeriod = period;
    }

    public override bool IsHot => _actualBuffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, bool isNew = true)
    {
        double actualVal = actual.Value;
        double predictedVal = predicted.Value;

        // Snapshot BEFORE any mutations for correct rollback
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        // Sanitize non-finite values AFTER snapshot/restore
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
            // Update actual buffer for mean calculation — Kahan compensated
            double removedActual = _actualBuffer.Count == _actualBuffer.Capacity ? _actualBuffer.Oldest : 0.0;
            {
                double delta = actualVal - removedActual;
                double y = delta - _state.ActualComp;
                double t = _state.ActualSum + y;
                _state.ActualComp = (t - _state.ActualSum) - y;
                _state.ActualSum = t;
            }
            _actualBuffer.Add(actualVal);

            // Calculate mean and baseline error
            double mean = _state.ActualSum / _actualBuffer.Count;
            double absError = Math.Abs(actualVal - predictedVal);
            double absBaseline = Math.Abs(actualVal - mean);

            // Update error buffer — Kahan compensated
            double removedError = _absErrorBuffer.Count == _absErrorBuffer.Capacity ? _absErrorBuffer.Oldest : 0.0;
            {
                double delta = absError - removedError;
                double y = delta - _state.AbsErrorComp;
                double t = _state.AbsErrorSum + y;
                _state.AbsErrorComp = (t - _state.AbsErrorSum) - y;
                _state.AbsErrorSum = t;
            }
            _absErrorBuffer.Add(absError);

            // Update baseline buffer — Kahan compensated
            double removedBaseline = _absBaselineBuffer.Count == _absBaselineBuffer.Capacity ? _absBaselineBuffer.Oldest : 0.0;
            {
                double delta = absBaseline - removedBaseline;
                double y = delta - _state.AbsBaselineComp;
                double t = _state.AbsBaselineSum + y;
                _state.AbsBaselineComp = (t - _state.AbsBaselineSum) - y;
                _state.AbsBaselineSum = t;
            }
            _absBaselineBuffer.Add(absBaseline);
        }
        else
        {
            // Update buffers and recalculate sums (buffer state is inconsistent with _p_state)
            _actualBuffer.UpdateNewest(actualVal);
            _state.ActualSum = _actualBuffer.RecalculateSum();

            // Calculate mean and errors
            double mean = _state.ActualSum / _actualBuffer.Count;
            double absError = Math.Abs(actualVal - predictedVal);
            double absBaseline = Math.Abs(actualVal - mean);

            _absErrorBuffer.UpdateNewest(absError);
            _absBaselineBuffer.UpdateNewest(absBaseline);
            _state.AbsErrorSum = _absErrorBuffer.RecalculateSum();
            _state.AbsBaselineSum = _absBaselineBuffer.RecalculateSum();
        }

        double result = _state.AbsBaselineSum > 1e-10 ? _state.AbsErrorSum / _state.AbsBaselineSum : 1.0;

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
        throw new NotSupportedException("RAE requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("RAE requires two inputs. Use Batch(actualSeries, predictedSeries, period).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("RAE requires two inputs.");
    }

    public override void Reset()
    {
        _actualBuffer.Clear();
        _absErrorBuffer.Clear();
        _absBaselineBuffer.Clear();
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
        Span<double> absErrorBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];
        Span<double> absBaselineBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double actualSum = 0;
        double absErrorSum = 0;
        double absBaselineSum = 0;
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
            double absError = Math.Abs(act - pred);
            double absBaseline = Math.Abs(act - mean);

            absErrorSum += absError;
            absBaselineSum += absBaseline;
            absErrorBuffer[i] = absError;
            absBaselineBuffer[i] = absBaseline;

            output[i] = absBaselineSum > 1e-10 ? absErrorSum / absBaselineSum : 1.0;
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

            actualSum = actualSum - actualBuffer[bufferIndex] + act;
            actualBuffer[bufferIndex] = act;

            double mean = actualSum / period;
            double absError = Math.Abs(act - pred);
            double absBaseline = Math.Abs(act - mean);

            absErrorSum = absErrorSum - absErrorBuffer[bufferIndex] + absError;
            absBaselineSum = absBaselineSum - absBaselineBuffer[bufferIndex] + absBaseline;
            absErrorBuffer[bufferIndex] = absError;
            absBaselineBuffer[bufferIndex] = absBaseline;

            bufferIndex++;
            if (bufferIndex >= period)
            {
                bufferIndex = 0;
            }

            output[i] = absBaselineSum > 1e-10 ? absErrorSum / absBaselineSum : 1.0;
        }
    }

    public static (TSeries Results, Rae Indicator) Calculate(TSeries actual, TSeries predicted, int period)
    {
        var indicator = new Rae(period);
        TSeries results = Batch(actual, predicted, period);
        return (results, indicator);
    }
}
