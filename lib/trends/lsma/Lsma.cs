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
///
/// Disposal:
/// When constructed with an ITValuePublisher source, Lsma subscribes to the source's Pub event.
/// Call Dispose() to unsubscribe and prevent memory leaks, especially in long-running applications
/// or when creating many short-lived indicator instances.
/// </remarks>
[SkipLocalsInit]
public sealed class Lsma : AbstractBase, IDisposable
{
    private readonly int _period;
    private readonly int _offset;
    private readonly RingBuffer _buffer;

    private readonly double _sum_x;
    private readonly double _denominator;
    private readonly TValuePublishedHandler _handler;
    private ITValuePublisher? _source;
    private int _disposed;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double SumY, double SumXY, double LastVal, double LastValidValue);
    private State _state;
    private State _p_state;

    private int _tickCount;

    private const int ResyncInterval = 1000;

    public override bool IsHot => _buffer.IsFull;

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
        WarmupPeriod = period;
        _handler = Handle;

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
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _source.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue = input;
            return input;
        }
        return _state.LastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateState(double val)
    {
        if (_buffer.IsFull)
        {
            double oldest = _buffer.Oldest;
            double prev_sum_y = _state.SumY;

            // O(1) update for sum_xy
            // sum_xy_new = sum_xy_old + sum_y_prev - n * oldest
            _state.SumXY = Math.FusedMultiplyAdd(-_period, oldest, _state.SumXY + prev_sum_y);

            // O(1) update for sum_y
            _state.SumY = _state.SumY - oldest + val;

            _buffer.Add(val);
        }
        else
        {
            if (_buffer.Count > 0)
            {
                _state.SumXY += _state.SumY;
            }
            _state.SumY += val;
            _buffer.Add(val);
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
        _state.SumY = _buffer.Sum;
        _state.SumXY = 0;
        var span = _buffer.GetSpan();
        for (int i = 0; i < span.Length; i++)
        {
            int x = span.Length - 1 - i;
            _state.SumXY = Math.FusedMultiplyAdd(x, span[i], _state.SumXY);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            double val = GetValidValue(input.Value);
            UpdateState(val);

            _p_state = _state;
            _state.LastVal = val;
        }
        else
        {
            _state.LastValidValue = _p_state.LastValidValue;
            double val = GetValidValue(input.Value);

            // For isNew=false, we update the current bar.
            // sum_xy remains constant because it depends on the previous window state which hasn't changed.
            // sum_y updates to reflect the change in the newest value.

            _state.SumY = _p_state.SumY - _p_state.LastVal + val;
            _state.SumXY = _p_state.SumXY; // Restore sum_xy to the state after the shift

            _buffer.UpdateNewest(val);
            _state.LastVal = val;
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
                double m = Math.FusedMultiplyAdd(n, _state.SumXY, -sx * _state.SumY) / denom;
                double b = Math.FusedMultiplyAdd(-m, sx, _state.SumY) / n;

                // LSMA = b - m * offset
                result = Math.FusedMultiplyAdd(-m, _offset, b);
            }
        }

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries([], []);

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        double initialLastValid = _state.LastValidValue;
        Calculate(source.Values, vSpan, _period, _offset, initialLastValid);
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
                    _state.LastValidValue = source.Values[i];
                    break;
                }
            }
        }
        else
        {
            _state.LastValidValue = initialLastValid;
        }

        double lastProcessedValue = _state.LastValidValue;
        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(source.Values[i]);
            UpdateState(val);
            lastProcessedValue = val;
        }

        _state.LastVal = lastProcessedValue;
        _p_state = _state;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    public static TSeries Batch(TSeries source, int period, int offset = 0)
    {
        var lsma = new Lsma(period, offset);
        return lsma.Update(source);
    }

    /// <summary>
    /// Calculates LSMA in-place, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period, int offset = 0, double initialLastValid = 0)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));
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
        double lastValid = initialLastValid;
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
                count++;

                // O(1) update: adding new value at x=0, existing values shift x+1
                // New value at x=0 contributes 0, existing sum shifts by sum_y
                if (count > 1)
                {
                    sum_xy += sum_y; // Shift existing values before adding new
                }
                sum_y += val;

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
                        double m = Math.FusedMultiplyAdd(n, sum_xy, -sx * sum_y) / denom;
                        double b = Math.FusedMultiplyAdd(-m, sx, sum_y) / n;
                        output[i] = Math.FusedMultiplyAdd(-m, offset, b);
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
                sum_xy = Math.FusedMultiplyAdd(-period, oldest, sum_xy + prev_sum_y);

                sum_y = sum_y - oldest + val;
                buffer[bufferIndex] = val;

                bufferIndex++;
                if (bufferIndex >= period)
                    bufferIndex = 0;

                double m = Math.FusedMultiplyAdd(period, sum_xy, -full_sum_x * sum_y) / full_denom;
                double b = Math.FusedMultiplyAdd(-m, full_sum_x, sum_y) / period;
                output[i] = Math.FusedMultiplyAdd(-m, offset, b);
            }
        }
    }

    /// <summary>
    /// Resets the LSMA state.
    /// </summary>
    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
        _tickCount = 0;
    }

    /// <summary>
    /// Disposes the Lsma instance, unsubscribing from the source publisher if subscribed.
    /// This method is idempotent and thread-safe.
    /// </summary>
    public void Dispose()
    {
        // Use Interlocked.CompareExchange for thread-safe, idempotent disposal
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0 && _source != null)
        {
            _source.Pub -= _handler;
            _source = null;
        }
        GC.SuppressFinalize(this);
    }
}
