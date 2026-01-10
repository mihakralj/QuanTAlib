using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MdAE: Median Absolute Error
/// </summary>
/// <remarks>
/// MdAE is the median of absolute errors between actual and predicted values.
/// Unlike MAE which uses the mean, MdAE is robust to outliers.
///
/// Formula:
/// MdAE = Median(|actual - predicted|)
///
/// Key properties:
/// - Robust to outliers (50% breakdown point)
/// - Same units as the original data
/// - Less sensitive to extreme errors than MAE
/// - MdAE = 0 indicates at least half the predictions are perfect
/// </remarks>
[SkipLocalsInit]
public sealed class Mdae : AbstractBase
{
    private readonly RingBuffer _buffer;
    private readonly double[] _sortBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    public Mdae(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _buffer = new RingBuffer(period);
        _sortBuffer = new double[period];
        Name = $"Mdae({period})";
        WarmupPeriod = period;
    }

    public override bool IsHot => _buffer.IsFull;

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

        double absError = Math.Abs(actualVal - predictedVal);

        if (isNew)
        {
            _p_state = _state;
            _buffer.Add(absError);
            _state.TickCount++;
        }
        else
        {
            _state = _p_state;
            _buffer.UpdateNewest(absError);
        }

        // Calculate median
        double result = CalculateMedian();

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
        throw new NotSupportedException("MdAE requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("MdAE requires two inputs. Use Calculate(actualSeries, predictedSeries, period).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("MdAE requires two inputs.");
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateMedian()
    {
        int count = _buffer.Count;
        if (count == 0) return 0.0;

        // Copy to sort buffer
        for (int i = 0; i < count; i++)
        {
            _sortBuffer[i] = _buffer[i];
        }

        // Sort the portion we're using
        Array.Sort(_sortBuffer, 0, count);

        // Return median
        if (count % 2 == 1)
        {
            return _sortBuffer[count / 2];
        }
        return (_sortBuffer[count / 2 - 1] + _sortBuffer[count / 2]) * 0.5;
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

        // Use heap allocation for batch - we need sorting per element
        double[] buffer = new double[period];
        double[] sortBuffer = new double[period];

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
        int bufferCount = 0;

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act)) lastValidActual = act; else act = lastValidActual;
            if (double.IsFinite(pred)) lastValidPredicted = pred; else pred = lastValidPredicted;

            double absError = Math.Abs(act - pred);

            // Add to circular buffer
            buffer[bufferIndex] = absError;
            bufferIndex++;
            if (bufferIndex >= period) bufferIndex = 0;
            if (bufferCount < period) bufferCount++;

            // Copy and sort for median
            for (int j = 0; j < bufferCount; j++)
            {
                sortBuffer[j] = buffer[j];
            }
            Array.Sort(sortBuffer, 0, bufferCount);

            // Calculate median
            if (bufferCount % 2 == 1)
            {
                output[i] = sortBuffer[bufferCount / 2];
                continue;
            }
            output[i] = (sortBuffer[bufferCount / 2 - 1] + sortBuffer[bufferCount / 2]) * 0.5;
        }
    }
}
