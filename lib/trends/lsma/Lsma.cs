using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// LSMA: Least Squares Moving Average
/// </summary>
/// <remarks>
/// LSMA calculates the linear regression line for the last n values and returns the value at the current position (or offset).
/// Uses a RingBuffer for storage and O(1) updates for regression sums.
///
/// Calculation:
/// Uses linear regression y = mx + b where x=0 is the current bar and x increases into the past.
/// m = (n * sum_xy - sum_x * sum_y) / denominator
/// b = (sum_y - m * sum_x) / n
/// LSMA = b - m * offset
///
/// O(1) update:
/// sum_y_new = sum_y_old - oldest + newest
/// sum_xy_new = sum_xy_old + sum_y_prev - n * oldest
///
/// IsHot:
/// Becomes true when the buffer is full (period samples processed).
/// </remarks>
[SkipLocalsInit]
public sealed class Lsma : ITValuePublisher
{
    private readonly int _period;
    private readonly int _offset;
    private readonly RingBuffer _buffer;

    private readonly double _sum_x;
    private readonly double _denominator;

    private double _sum_y;
    private double _sum_xy;
    
    private double _p_sum_y;
    private double _p_sum_xy;
    private double _p_last_val;
    
    private double _lastValidValue;
    private double _p_lastValidValue;
    private int _tickCount;

    private const int ResyncInterval = 1000;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event Action<TValue>? Pub;

    /// <summary>
    /// Creates LSMA with specified period and offset.
    /// </summary>
    /// <param name="period">Lookback period (must be > 0)</param>
    /// <param name="offset">Offset from current bar (default 0). Positive values project into future.</param>
    public Lsma(int period, int offset = 0)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _offset = offset;
        _buffer = new RingBuffer(period);
        Name = $"Lsma({period})";

        // Precalculate constants
        // sum_x = 0 + 1 + ... + (n-1) = n(n-1)/2
        _sum_x = 0.5 * period * (period - 1);
        
        // sum_x2 = 0^2 + ... + (n-1)^2 = (n-1)n(2n-1)/6
        double sum_x2 = (period - 1.0) * period * (2.0 * period - 1.0) / 6.0;
        
        // denominator = n * sum_x2 - sum_x^2
        _denominator = period * sum_x2 - _sum_x * _sum_x;
    }

    public Lsma(ITValuePublisher source, int period, int offset = 0) : this(period, offset)
    {
        source.Pub += (item) => Update(item);
    }

    /// <summary>
    /// Current LSMA value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the LSMA has enough data to produce valid results.
    /// </summary>
    public bool IsHot => _buffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _lastValidValue = input;
            return input;
        }
        return _lastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateState(double val)
    {
        if (_buffer.IsFull)
        {
            double oldest = _buffer.Oldest;
            double prev_sum_y = _sum_y;
            
            // O(1) update for sum_xy
            // sum_xy_new = sum_xy_old + sum_y_prev - n * oldest
            _sum_xy = _sum_xy + prev_sum_y - _period * oldest;
            
            // O(1) update for sum_y
            _sum_y = _sum_y - oldest + val;
            
            _buffer.Add(val);
        }
        else
        {
            _buffer.Add(val);
            _sum_y += val;
            
            // Recalculate sum_xy from scratch during warmup
            _sum_xy = 0;
            var span = _buffer.GetSpan();
            for (int i = 0; i < span.Length; i++)
            {
                // x=0 is newest (index count-1), x=count-1 is oldest (index 0)
                // buffer stores chronological: [oldest, ..., newest]
                // index j in buffer corresponds to x = count - 1 - j
                // sum_xy = sum(x * y)
                int x = span.Length - 1 - i;
                _sum_xy += x * span[i];
            }
        }

        _tickCount++;
        if (_buffer.IsFull && _tickCount >= ResyncInterval)
        {
            _tickCount = 0;
            Resync();
        }
    }

    private void Resync()
    {
        _sum_y = _buffer.Sum;
        _sum_xy = 0;
        var span = _buffer.GetSpan();
        for (int i = 0; i < span.Length; i++)
        {
            int x = span.Length - 1 - i;
            _sum_xy += x * span[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            double val = GetValidValue(input.Value);
            UpdateState(val);

            _p_sum_y = _sum_y;
            _p_sum_xy = _sum_xy;
            _p_last_val = val;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _lastValidValue = _p_lastValidValue;
            double val = GetValidValue(input.Value);

            // For isNew=false, we update the current bar.
            // sum_xy remains constant because it depends on the previous window state which hasn't changed.
            // sum_y updates to reflect the change in the newest value.
            
            _sum_y = _p_sum_y - _p_last_val + val;
            _sum_xy = _p_sum_xy; // Restore sum_xy to the state after the shift
            
            _buffer.UpdateNewest(val);
            _p_last_val = val;
        }

        double result;
        if (_buffer.Count <= 1)
        {
            result = _buffer.Newest;
        }
        else
        {
            // Calculate regression parameters
            // During warmup, we use the current count as n
            double n = _buffer.Count;
            double sx = _sum_x;
            double denom = _denominator;
            
            if (!_buffer.IsFull)
            {
                // Recalculate constants for smaller n
                sx = 0.5 * n * (n - 1);
                double sx2 = (n - 1.0) * n * (2.0 * n - 1.0) / 6.0;
                denom = n * sx2 - sx * sx;
            }

            if (Math.Abs(denom) < 1e-10)
            {
                result = _buffer.Newest;
            }
            else
            {
                double m = (n * _sum_xy - sx * _sum_y) / denom;
                double b = (_sum_y - m * sx) / n;
                
                // LSMA = b - m * offset
                result = b - m * _offset;
            }
        }

        Last = new TValue(input.Time, result);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Calculate(source.Values, vSpan, _period, _offset);
        source.Times.CopyTo(tSpan);

        // Restore state
        // We need to replay the last 'period' bars to set up the buffer and sums correctly
        int windowSize = Math.Min(len, _period);
        int startIndex = len - windowSize;

        Reset();

        // Initialize lastValidValue
        if (startIndex > 0)
        {
            for (int i = startIndex - 1; i >= 0; i--)
            {
                if (double.IsFinite(source.Values[i]))
                {
                    _lastValidValue = source.Values[i];
                    break;
                }
            }
        }
        else
        {
            _lastValidValue = 0;
        }

        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(source.Values[i]);
            UpdateState(val);
        }

        _p_sum_y = _sum_y;
        _p_sum_xy = _sum_xy;
        _p_last_val = source.Values[len - 1];
        _p_lastValidValue = _lastValidValue;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates LSMA for the entire series using a new instance.
    /// </summary>
    public static TSeries Calculate(TSeries source, int period, int offset = 0)
    {
        var lsma = new Lsma(period, offset);
        return lsma.Update(source);
    }

    /// <summary>
    /// Calculates LSMA in-place, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period, int offset = 0)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = source.Length;
        if (len == 0) return;

        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double sum_y = 0;
        double sum_xy = 0;
        double lastValid = 0;
        int bufferIndex = 0; // Points to where the NEXT value will be written (circular)
        int count = 0;

        // Precalculate constants for full period
        double full_sum_x = 0.5 * period * (period - 1);
        double full_sum_x2 = (period - 1.0) * period * (2.0 * period - 1.0) / 6.0;
        double full_denom = period * full_sum_x2 - full_sum_x * full_sum_x;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            if (count < period)
            {
                // Warmup phase
                buffer[count] = val;
                sum_y += val;
                count++;
                
                // Recalculate sum_xy for current count
                sum_xy = 0;
                for (int j = 0; j < count; j++)
                {
                    // buffer[j] is at index j
                    // x = count - 1 - j
                    sum_xy += (count - 1 - j) * buffer[j];
                }

                if (count <= 1)
                {
                    output[i] = val;
                }
                else
                {
                    double n = count;
                    double sx = 0.5 * n * (n - 1);
                    double sx2 = (n - 1.0) * n * (2.0 * n - 1.0) / 6.0;
                    double denom = n * sx2 - sx * sx;

                    if (Math.Abs(denom) < 1e-10)
                    {
                        output[i] = val;
                    }
                    else
                    {
                        double m = (n * sum_xy - sx * sum_y) / denom;
                        double b = (sum_y - m * sx) / n;
                        output[i] = b - m * offset;
                    }
                }
                
                if (count == period)
                {
                    bufferIndex = 0; // Reset for circular buffer usage
                }
            }
            else
            {
                // Full buffer phase - O(1) update
                double oldest = buffer[bufferIndex];
                double prev_sum_y = sum_y;
                
                // sum_xy_new = sum_xy_old + sum_y_prev - n * oldest
                sum_xy = sum_xy + prev_sum_y - period * oldest;
                
                sum_y = sum_y - oldest + val;
                buffer[bufferIndex] = val;
                
                bufferIndex++;
                if (bufferIndex >= period)
                    bufferIndex = 0;

                double m = (period * sum_xy - full_sum_x * sum_y) / full_denom;
                double b = (sum_y - m * full_sum_x) / period;
                output[i] = b - m * offset;
            }
        }
    }

    /// <summary>
    /// Resets the LSMA state.
    /// </summary>
    public void Reset()
    {
        _buffer.Clear();
        _sum_y = 0;
        _sum_xy = 0;
        _p_sum_y = 0;
        _p_sum_xy = 0;
        _p_last_val = 0;
        Last = default;
        _tickCount = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
    }
}
