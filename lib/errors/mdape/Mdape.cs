using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MdAPE: Median Absolute Percentage Error
/// </summary>
/// <remarks>
/// MdAPE is the median of absolute percentage errors. Unlike MAPE which uses
/// the mean, MdAPE is robust to outliers in percentage terms.
///
/// Formula:
/// MdAPE = Median(|actual - predicted| / |actual|) * 100
///
/// Key properties:
/// - Robust to outliers (50% breakdown point)
/// - Scale-independent (expressed as percentage)
/// - Less sensitive to extreme percentage errors than MAPE
/// - Undefined when actual = 0 (uses epsilon protection)
/// </remarks>
[SkipLocalsInit]
public sealed class Mdape : AbstractBase
{
    private readonly RingBuffer _buffer;
    private readonly double[] _sortBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValidActual, double LastValidPredicted, int TickCount);
    private State _state;
    private State _p_state;

    public Mdape(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _buffer = new RingBuffer(period);
        _sortBuffer = new double[period];
        Name = $"Mdape({period})";
        WarmupPeriod = period;
    }

    public override bool IsHot => _buffer.IsFull;

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
        throw new NotSupportedException("MdAPE requires two inputs. Use Update(actual, predicted).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("MdAPE requires two inputs. Use Batch(actualSeries, predictedSeries, period).");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(DateTime time, double actualVal, double predictedVal, bool isNew)
    {
        // Validate actual: must be finite AND have sufficient magnitude (matches Batch logic)
        if (!double.IsFinite(actualVal) || Math.Abs(actualVal) < 1e-10)
        {
            actualVal = double.IsFinite(_state.LastValidActual) && Math.Abs(_state.LastValidActual) >= 1e-10
                ? _state.LastValidActual
                : 1.0;
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

        // Calculate absolute percentage error (absActual guaranteed >= 1e-10 by validation above)
        double absActual = Math.Abs(actualVal);
        double absError = Math.Abs(actualVal - predictedVal);
        double percentageError = (absError / absActual) * 100.0;

        if (isNew)
        {
            _p_state = _state;
            _buffer.Add(percentageError);
            _state.TickCount++;
        }
        else
        {
            _state = _p_state;
            _buffer.UpdateNewest(percentageError);
        }

        // Calculate median
        double result = CalculateMedian();

        Last = new TValue(time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("MdAPE requires two inputs.");
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

        // Use dual-heap sliding median for O(log n) updates instead of O(n log n) sort per element
        var slidingMedian = new SlidingMedianHeap(period);

        double lastValidActual = 1.0;
        double lastValidPredicted = 0;

        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(actual[k]) && Math.Abs(actual[k]) >= 1e-10)
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

        for (int i = 0; i < len; i++)
        {
            double act = actual[i];
            double pred = predicted[i];

            if (double.IsFinite(act) && Math.Abs(act) >= 1e-10)
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

            double absActual = Math.Abs(act);
            double absError = Math.Abs(act - pred);
            double percentageError = absActual > 1e-10 ? (absError / absActual) * 100.0 : 0.0;

            // Add to sliding median (handles removal of old values automatically)
            slidingMedian.Add(percentageError);

            // Get median in O(1)
            output[i] = slidingMedian.GetMedian();
        }
    }

    public static (TSeries Results, Mdape Indicator) Calculate(TSeries actual, TSeries predicted, int period)
    {
        var indicator = new Mdape(period);
        TSeries results = Batch(actual, predicted, period);
        return (results, indicator);
    }

    /// <summary>
    /// Dual-heap based sliding window median calculator.
    /// Maintains O(log n) insert/remove and O(1) median query.
    /// </summary>
    private sealed class SlidingMedianHeap
    {
        private readonly int _windowSize;
        private readonly Queue<double> _window;
        private readonly SortedList<double, int> _lower; // max-heap simulation (stores smaller half)
        private readonly SortedList<double, int> _upper; // min-heap simulation (stores larger half)
        private int _lowerCount;
        private int _upperCount;

        public SlidingMedianHeap(int windowSize)
        {
            _windowSize = windowSize;
            _window = new Queue<double>(windowSize + 1);
            _lower = new SortedList<double, int>();
            _upper = new SortedList<double, int>();
            _lowerCount = 0;
            _upperCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(double value)
        {
            // If window is full, remove oldest element
            if (_window.Count >= _windowSize)
            {
                double oldest = _window.Dequeue();
                Remove(oldest);
            }

            _window.Enqueue(value);
            Insert(value);
            Rebalance();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetMedian()
        {
            if (_lowerCount == 0 && _upperCount == 0)
            {
                return 0.0;
            }

            if (_lowerCount > _upperCount)
            {
                return _lower.Keys[_lower.Count - 1]; // max of lower
            }
            else if (_upperCount > _lowerCount)
            {
                return _upper.Keys[0]; // min of upper
            }
            else
            {
                return (_lower.Keys[_lower.Count - 1] + _upper.Keys[0]) * 0.5;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Insert(double value)
        {
            if (_lowerCount == 0 || value <= _lower.Keys[_lower.Count - 1])
            {
                AddToList(_lower, value);
                _lowerCount++;
            }
            else
            {
                AddToList(_upper, value);
                _upperCount++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Remove(double value)
        {
            if (_lowerCount > 0 && value <= _lower.Keys[_lower.Count - 1])
            {
                RemoveFromList(_lower, value);
                _lowerCount--;
            }
            else
            {
                RemoveFromList(_upper, value);
                _upperCount--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Rebalance()
        {
            // Ensure lower has at most 1 more element than upper
            while (_lowerCount > _upperCount + 1)
            {
                double val = _lower.Keys[_lower.Count - 1];
                RemoveFromList(_lower, val);
                _lowerCount--;
                AddToList(_upper, val);
                _upperCount++;
            }

            while (_upperCount > _lowerCount)
            {
                double val = _upper.Keys[0];
                RemoveFromList(_upper, val);
                _upperCount--;
                AddToList(_lower, val);
                _lowerCount++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddToList(SortedList<double, int> list, double value)
        {
            if (list.TryGetValue(value, out int count))
            {
                list[value] = count + 1;
            }
            else
            {
                list[value] = 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RemoveFromList(SortedList<double, int> list, double value)
        {
            if (list.TryGetValue(value, out int count))
            {
                if (count == 1)
                {
                    list.Remove(value);
                }
                else
                {
                    list[value] = count - 1;
                }
            }
        }
    }
}