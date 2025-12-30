using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MSLE: Mean Squared Logarithmic Error
/// </summary>
/// <remarks>
/// MSLE measures the ratio between actual and predicted values using logarithms,
/// penalizing under-predictions more than over-predictions of the same magnitude.
/// Useful when targets span several orders of magnitude.
///
/// Formula:
/// MSLE = (1/n) * Σ(log(1 + actual) - log(1 + predicted))²
///
/// Key properties:
/// - Robust to outliers (logarithmic compression)
/// - Penalizes under-predictions more heavily
/// - Requires non-negative values (uses 1 + x to handle zeros)
/// - Scale-independent for multiplicative relationships
/// </remarks>
[SkipLocalsInit]
public sealed class Msle : AbstractBase
{
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Sum, double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    public Msle(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _buffer = new RingBuffer(period);
        Name = $"Msle({period})";
        WarmupPeriod = period;
    }

    public override bool IsHot => _buffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, bool isNew = true)
    {
        double actualVal = actual.Value;
        double predictedVal = predicted.Value;

        if (!double.IsFinite(actualVal) || actualVal < 0)
            actualVal = double.IsFinite(_state.LastValidActual) && _state.LastValidActual >= 0
                ? _state.LastValidActual : 0.0;
        else
            _state.LastValidActual = actualVal;

        if (!double.IsFinite(predictedVal) || predictedVal < 0)
            predictedVal = double.IsFinite(_state.LastValidPredicted) && _state.LastValidPredicted >= 0
                ? _state.LastValidPredicted : 0.0;
        else
            _state.LastValidPredicted = predictedVal;

        // MSLE formula: (log(1 + actual) - log(1 + predicted))²
        double logActual = Math.Log(1.0 + actualVal);
        double logPredicted = Math.Log(1.0 + predictedVal);
        double logError = logActual - logPredicted;
        double squaredLogError = logError * logError;

        if (isNew)
        {
            _p_state = _state;

            double removedValue = _buffer.Count == _buffer.Capacity ? _buffer.Oldest : 0.0;
            _state.Sum = _state.Sum - removedValue + squaredLogError;
            _buffer.Add(squaredLogError);

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
            _state.Sum = _state.Sum - removedValue + squaredLogError;
            _buffer.UpdateNewest(squaredLogError);
            _state.Sum = _buffer.RecalculateSum();
        }

        double result = _buffer.Count > 0 ? _state.Sum / _buffer.Count : squaredLogError;
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
        throw new NotSupportedException("MSLE requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("MSLE requires two inputs. Use Calculate(actualSeries, predictedSeries, period).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("MSLE requires two inputs.");
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
        double lastValidActual = 0;
        double lastValidPredicted = 0;

        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(actual[k]) && actual[k] >= 0) { lastValidActual = actual[k]; break; }
        }
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(predicted[k]) && predicted[k] >= 0) { lastValidPredicted = predicted[k]; break; }
        }

        int bufferIndex = 0;
        int i = 0;

        int warmupEnd = Math.Min(period, len);
        for (; i < warmupEnd; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act) && act >= 0) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred) && pred >= 0) lastValidPredicted = pred; else pred = lastValidPredicted;

            double logActual = Math.Log(1.0 + act);
            double logPredicted = Math.Log(1.0 + pred);
            double logError = logActual - logPredicted;
            double squaredLogError = logError * logError;

            sum += squaredLogError;
            buffer[i] = squaredLogError;
            output[i] = sum / (i + 1);
        }

        int tickCount = 0;
        for (; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act) && act >= 0) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred) && pred >= 0) lastValidPredicted = pred; else pred = lastValidPredicted;

            double logActual = Math.Log(1.0 + act);
            double logPredicted = Math.Log(1.0 + pred);
            double logError = logActual - logPredicted;
            double squaredLogError = logError * logError;

            sum = sum - buffer[bufferIndex] + squaredLogError;
            buffer[bufferIndex] = squaredLogError;

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
