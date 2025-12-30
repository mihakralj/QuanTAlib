using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MAPE: Mean Absolute Percentage Error
/// </summary>
/// <remarks>
/// MAPE measures the average absolute percentage error between actual and predicted values.
/// It expresses accuracy as a percentage, making it scale-independent.
///
/// Formula:
/// MAPE = (100/n) * Σ|((actual - predicted) / actual)|
///
/// Key properties:
/// - Scale-independent (expressed as percentage)
/// - Cannot be calculated when actual = 0
/// - Asymmetric: penalizes under-predictions more than over-predictions
/// - Undefined for zero actual values
/// </remarks>
[SkipLocalsInit]
public sealed class Mape : AbstractBase
{
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Sum, double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    public Mape(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _buffer = new RingBuffer(period);
        Name = $"Mape({period})";
        WarmupPeriod = period;
    }

    public override bool IsHot => _buffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, bool isNew = true)
    {
        double actualVal = actual.Value;
        double predictedVal = predicted.Value;

        if (!double.IsFinite(actualVal))
            actualVal = double.IsFinite(_state.LastValidActual) ? _state.LastValidActual : 1.0; // Avoid division by zero
        else
            _state.LastValidActual = actualVal;

        if (!double.IsFinite(predictedVal))
            predictedVal = double.IsFinite(_state.LastValidPredicted) ? _state.LastValidPredicted : 0.0;
        else
            _state.LastValidPredicted = predictedVal;

        // Avoid division by zero - use small epsilon if actual is zero
        double divisor = Math.Abs(actualVal) < 1e-10 ? 1e-10 : actualVal;
        double percentageError = 100.0 * Math.Abs((actualVal - predictedVal) / divisor);

        if (isNew)
        {
            _p_state = _state;

            double removedValue = _buffer.Count == _buffer.Capacity ? _buffer.Oldest : 0.0;
            _state.Sum = _state.Sum - removedValue + percentageError;
            _buffer.Add(percentageError);

            _state.TickCount++;
            if (_buffer.IsFull && _state.TickCount >= ResyncInterval)
            {
                _state.TickCount = 0;
                _state.Sum = _buffer.RecalculateSum();
            }
        }
        else
        {
            _state = _p_state;

            double removedValue = _buffer.Count == _buffer.Capacity ? _buffer.Oldest : 0.0;
            _state.Sum = _state.Sum - removedValue + percentageError;
            _buffer.UpdateNewest(percentageError);
            _state.Sum = _buffer.RecalculateSum();
        }

        double result = _buffer.Count > 0 ? _state.Sum / _buffer.Count : percentageError;
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
        throw new NotSupportedException("MAPE requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("MAPE requires two inputs. Use Calculate(actualSeries, predictedSeries, period).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("MAPE requires two inputs.");
    }

    public override void Reset()
    {
        _buffer.Clear();
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
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double sum = 0;
        double lastValidActual = 1.0; // Default to 1 to avoid division by zero
        double lastValidPredicted = 0;

        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(actual[k]) && Math.Abs(actual[k]) >= 1e-10) { lastValidActual = actual[k]; break; }
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

            if (double.IsFinite(act) && Math.Abs(act) >= 1e-10) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double divisor = Math.Abs(act) < 1e-10 ? 1e-10 : act;
            double percentageError = 100.0 * Math.Abs((act - pred) / divisor);

            sum += percentageError;
            buffer[i] = percentageError;
            output[i] = sum / (i + 1);
        }

        int tickCount = 0;
        for (; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act) && Math.Abs(act) >= 1e-10) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double divisor = Math.Abs(act) < 1e-10 ? 1e-10 : act;
            double percentageError = 100.0 * Math.Abs((act - pred) / divisor);

            sum = sum - buffer[bufferIndex] + percentageError;
            buffer[bufferIndex] = percentageError;

            bufferIndex++;
            if (bufferIndex >= period) bufferIndex = 0;

            output[i] = sum / period;

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcSum = 0;
                for (int k = 0; k < period; k++) recalcSum += buffer[k];
                sum = recalcSum;
            }
        }
    }
}
