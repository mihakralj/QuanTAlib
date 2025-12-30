using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MAE: Mean Absolute Error
/// </summary>
/// <remarks>
/// MAE measures the average magnitude of errors between paired observations,
/// without considering their direction. It is the mean of the absolute differences
/// between actual and predicted values.
///
/// Formula:
/// MAE = (1/n) * Σ|actual - predicted|
///
/// Uses a RingBuffer for O(1) streaming updates with running sum.
///
/// Key properties:
/// - Always non-negative (MAE ≥ 0)
/// - Same units as the original data
/// - Less sensitive to outliers than MSE/RMSE
/// - MAE = 0 indicates perfect prediction
/// </remarks>
[SkipLocalsInit]
public sealed class Mae : AbstractBase
{
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Sum, double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    /// <summary>
    /// Creates MAE with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Mae(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _buffer = new RingBuffer(period);
        Name = $"Mae({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// True if the MAE has enough data to produce valid results.
    /// </summary>
    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Updates the MAE with new actual and predicted values.
    /// </summary>
    /// <param name="actual">Actual value (source1)</param>
    /// <param name="predicted">Predicted value (source2)</param>
    /// <param name="isNew">Whether this is a new bar.</param>
    /// <returns>The calculated MAE value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue actual, TValue predicted, bool isNew = true)
    {
        double actualVal = actual.Value;
        double predictedVal = predicted.Value;

        // Handle NaN/Infinity with last-valid-value substitution
        if (!double.IsFinite(actualVal))
            actualVal = double.IsFinite(_state.LastValidActual) ? _state.LastValidActual : 0.0;
        else
            _state.LastValidActual = actualVal;

        if (!double.IsFinite(predictedVal))
            predictedVal = double.IsFinite(_state.LastValidPredicted) ? _state.LastValidPredicted : 0.0;
        else
            _state.LastValidPredicted = predictedVal;

        double error = Math.Abs(actualVal - predictedVal);

        if (isNew)
        {
            _p_state = _state;

            double removedValue = _buffer.Count == _buffer.Capacity ? _buffer.Oldest : 0.0;
            _state.Sum = _state.Sum - removedValue + error;
            _buffer.Add(error);

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
            _state.Sum = _state.Sum - removedValue + error;
            _buffer.UpdateNewest(error);
            _state.Sum = _buffer.RecalculateSum();
        }

        double result = _buffer.Count > 0 ? _state.Sum / _buffer.Count : error;
        Last = new TValue(actual.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the MAE with raw double values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double actual, double predicted, bool isNew = true)
    {
        return Update(new TValue(DateTime.UtcNow, actual), new TValue(DateTime.UtcNow, predicted), isNew);
    }


    /// <summary>
    /// Single-input Update is not supported. Use Update(actual, predicted).
    /// </summary>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("MAE requires two inputs. Use Update(actual, predicted).");
    }

    /// <summary>
    /// Single-series Update is not supported. Use Calculate(actual, predicted, period).
    /// </summary>
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("MAE requires two inputs. Use Calculate(actualSeries, predictedSeries, period).");
    }

    /// <summary>
    /// Single-series Prime is not supported.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("MAE requires two inputs.");
    }

    /// <summary>
    /// Resets the MAE state.
    /// </summary>
    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    /// <summary>
    /// Calculates MAE for the entire series pair.
    /// </summary>
    /// <param name="actual">Actual values series</param>
    /// <param name="predicted">Predicted values series</param>
    /// <param name="period">MAE period</param>
    /// <returns>MAE series</returns>
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

    /// <summary>
    /// Calculates MAE in-place using pre-allocated spans.
    /// </summary>
    /// <param name="actual">Actual values</param>
    /// <param name="predicted">Predicted values</param>
    /// <param name="output">Output span (must be same length as inputs)</param>
    /// <param name="period">MAE period (must be > 0)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        if (actual.Length != predicted.Length || actual.Length != output.Length)
            throw new ArgumentException("All spans must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = actual.Length;
        if (len == 0) return;

        CalculateScalarCore(actual, predicted, output, period);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> actual, ReadOnlySpan<double> predicted, Span<double> output, int period)
    {
        int len = actual.Length;

        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double sum = 0;
        double lastValidActual = 0;
        double lastValidPredicted = 0;

        // Find first valid values
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

            if (double.IsFinite(act)) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double error = Math.Abs(act - pred);
            sum += error;
            buffer[i] = error;
            output[i] = sum / (i + 1);
        }

        int tickCount = 0;
        for (; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double error = Math.Abs(act - pred);
            sum = sum - buffer[bufferIndex] + error;
            buffer[bufferIndex] = error;

            bufferIndex++;
            if (bufferIndex >= period) bufferIndex = 0;

            output[i] = sum / period;

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcSum = 0;
                for (int k = 0; k < period; k++)
                {
                    recalcSum += buffer[k];
                }
                sum = recalcSum;
            }
        }
    }
}
