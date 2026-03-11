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
    private const int StackAllocThreshold = 256;

    private readonly RingBuffer _buffer;
    private readonly double[] _sortBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    public Mdae(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

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

        // Snapshot BEFORE any mutations for correct rollback
        if (isNew)
        {
            _p_state = _state;
        }
        else
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

        double absError = Math.Abs(actualVal - predictedVal);

        if (isNew)
        {
            _buffer.Add(absError);
            _state.TickCount++;
        }
        else
        {
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
        return Update(new TValue(DateTime.MinValue, actual), new TValue(DateTime.MinValue, predicted), isNew);
    }

    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("MdAE requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("MdAE requires two inputs. Use Batch(actualSeries, predictedSeries, period).");
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
        if (count == 0)
        {
            return 0.0;
        }

        // Copy buffer contents to sort buffer using GetSequencedSpans to handle wraparound
        _buffer.GetSequencedSpans(out var first, out var second);
        first.CopyTo(_sortBuffer.AsSpan(0, first.Length));
        if (second.Length > 0)
        {
            second.CopyTo(_sortBuffer.AsSpan(first.Length, second.Length));
        }

        // Sort the portion we copied
        Array.Sort(_sortBuffer, 0, count);

        // Calculate median
        if ((count & 1) != 0)
        {
            return _sortBuffer[count / 2];
        }

        // For even count, average the two middle elements
        int mid = count / 2;
        return (_sortBuffer[mid - 1] + _sortBuffer[mid]) * 0.5;
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

        // Use stackalloc for small periods, heap for larger
        scoped Span<double> buffer;
        scoped Span<double> sortBuffer;

        if (period <= StackAllocThreshold)
        {
            buffer = stackalloc double[period];
            sortBuffer = stackalloc double[period];
        }
        else
        {
            buffer = new double[period];
            sortBuffer = new double[period];
        }

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
        int bufferCount = 0;

        for (int i = 0; i < len; i++)
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

            // Add to circular buffer
            buffer[bufferIndex] = absError;
            bufferIndex++;
            if (bufferIndex >= period)
            {
                bufferIndex = 0;
            }

            if (bufferCount < period)
            {
                bufferCount++;
            }

            // Copy and use QuickSelect for median
            buffer.Slice(0, bufferCount).CopyTo(sortBuffer);

            // Calculate median using QuickSelect
            if ((bufferCount & 1) != 0)
            {
                output[i] = QuickSelectSpan(sortBuffer.Slice(0, bufferCount), bufferCount / 2);
                continue;
            }

            int mid = bufferCount / 2;
            double upper = QuickSelectSpan(sortBuffer.Slice(0, bufferCount), mid);

            // Copy again for second selection
            buffer.Slice(0, bufferCount).CopyTo(sortBuffer);
            double lower = QuickSelectSpan(sortBuffer.Slice(0, bufferCount), mid - 1);

            output[i] = (lower + upper) * 0.5;
        }
    }

    public static (TSeries Results, Mdae Indicator) Calculate(TSeries actual, TSeries predicted, int period)
    {
        var indicator = new Mdae(period);
        TSeries results = Batch(actual, predicted, period);
        return (results, indicator);
    }

    /// <summary>
    /// QuickSelect for Span - finds the k-th smallest element in O(n) average time.
    /// Uses insertion sort for small arrays and Lomuto partition for larger arrays.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double QuickSelectSpan(Span<double> span, int k)
    {
        int left = 0;
        int right = span.Length - 1;

        while (left < right)
        {
            // For small subarrays (<=16 elements), use insertion sort - simple and cache-friendly
            if (right - left < 16)
            {
                for (int i = left + 1; i <= right; i++)
                {
                    double key = span[i];
                    int j = i - 1;
                    while (j >= left && span[j] > key)
                    {
                        span[j + 1] = span[j];
                        j--;
                    }
                    span[j + 1] = key;
                }
                return span[k];
            }

            // Median-of-three pivot selection for better pivot choice
            int mid = left + ((right - left) / 2);
            if (span[mid] < span[left])
            {
                (span[left], span[mid]) = (span[mid], span[left]);
            }

            if (span[right] < span[left])
            {
                (span[left], span[right]) = (span[right], span[left]);
            }

            if (span[right] < span[mid])
            {
                (span[mid], span[right]) = (span[right], span[mid]);
            }

            // Use median as pivot, move to right-1 position
            double pivot = span[mid];
            (span[mid], span[right - 1]) = (span[right - 1], span[mid]);

            // Lomuto partition scheme (safer, no overflow risk)
            int storeIndex = left;
            for (int i = left; i < right - 1; i++)
            {
                if (span[i] < pivot)
                {
                    (span[storeIndex], span[i]) = (span[i], span[storeIndex]);
                    storeIndex++;
                }
            }
            (span[storeIndex], span[right - 1]) = (span[right - 1], span[storeIndex]);

            if (k == storeIndex)
            {
                return span[storeIndex];
            }

            if (k < storeIndex)
            {
                right = storeIndex - 1;
            }
            else
            {
                left = storeIndex + 1;
            }
        }

        return span[left];
    }
}