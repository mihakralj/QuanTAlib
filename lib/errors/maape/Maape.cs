using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MAAPE: Mean Arctangent Absolute Percentage Error
/// </summary>
/// <remarks>
/// MAAPE uses the arctangent function to bound the error between 0 and π/2,
/// making it more robust to outliers and handling zero actual values gracefully.
/// It provides a bounded alternative to MAPE with better statistical properties.
///
/// Formula:
/// MAAPE = (1/n) * Σ arctan(|actual - predicted| / |actual|)
///
/// Key properties:
/// - Bounded output: always between 0 and π/2 (≈1.5708)
/// - Handles zero actual values gracefully (approaches π/2)
/// - Less sensitive to outliers than MAPE
/// - Symmetric: treats over- and under-prediction similarly
/// - Scale-independent
/// </remarks>
[SkipLocalsInit]
public sealed class Maape : AbstractBase
{
    private readonly RingBuffer _atanBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double AtanSum, double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    public Maape(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _atanBuffer = new RingBuffer(period);
        Name = $"Maape({period})";
        WarmupPeriod = period;
    }

    public override bool IsHot => _atanBuffer.IsFull;

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

        // arctan(|error| / |actual|) - if actual is 0, ratio approaches infinity, arctan approaches π/2
        double absActual = Math.Abs(actualVal);
        double absError = Math.Abs(actualVal - predictedVal);
        double atanValue = absActual > 1e-10 ? Math.Atan(absError / absActual) : Math.PI / 2.0;

        if (isNew)
        {
            _p_state = _state;

            double removedAtan = _atanBuffer.Count == _atanBuffer.Capacity ? _atanBuffer.Oldest : 0.0;
            _state.AtanSum = _state.AtanSum - removedAtan + atanValue;
            _atanBuffer.Add(atanValue);

            _state.TickCount++;
            if (_atanBuffer.IsFull && _state.TickCount >= ResyncInterval)
            {
                _state.TickCount = 0;
                _state.AtanSum = _atanBuffer.RecalculateSum();
            }
        }
        else
        {
            _state = _p_state;

            double removedAtan = _atanBuffer.Count == _atanBuffer.Capacity ? _atanBuffer.Oldest : 0.0;
            _state.AtanSum = _state.AtanSum - removedAtan + atanValue;
            _atanBuffer.UpdateNewest(atanValue);
            _state.AtanSum = _atanBuffer.RecalculateSum();
        }

        // MAAPE = (1/n) * Σ arctan(|error| / |actual|)
        double result = _atanBuffer.Count > 0 ? _state.AtanSum / _atanBuffer.Count : 0.0;

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
        throw new NotSupportedException("MAAPE requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("MAAPE requires two inputs. Use Calculate(actualSeries, predictedSeries, period).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("MAAPE requires two inputs.");
    }

    public override void Reset()
    {
        _atanBuffer.Clear();
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
        Span<double> atanBuffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double atanSum = 0;
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

            double absActual = Math.Abs(act);
            double absError = Math.Abs(act - pred);
            double atanValue = absActual > 1e-10 ? Math.Atan(absError / absActual) : Math.PI / 2.0;

            atanSum += atanValue;
            atanBuffer[i] = atanValue;

            output[i] = atanSum / (i + 1);
        }

        int tickCount = 0;
        for (; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double absActual = Math.Abs(act);
            double absError = Math.Abs(act - pred);
            double atanValue = absActual > 1e-10 ? Math.Atan(absError / absActual) : Math.PI / 2.0;

            atanSum = atanSum - atanBuffer[bufferIndex] + atanValue;
            atanBuffer[bufferIndex] = atanValue;

            bufferIndex++;
            if (bufferIndex >= period) bufferIndex = 0;

            output[i] = atanSum / period;

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcSum = 0;
                for (int k = 0; k < period; k++)
                    recalcSum += atanBuffer[k];
                atanSum = recalcSum;
            }
        }
    }
}
