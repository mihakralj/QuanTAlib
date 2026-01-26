using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// A monotonic deque for O(1) amortized sliding window min/max queries.
/// Maintains elements in strictly monotonic order (non-increasing for max, non-decreasing for min).
/// Used by channel indicators (Donchian, MinMax) for efficient rolling extrema.
/// </summary>
/// <remarks>
/// Algorithm: For each new element, expire indices outside the window, then pop elements
/// from the back that would violate monotonicity, then push the new index.
/// Time complexity: O(1) amortized per operation (each element pushed/popped at most once).
/// Space complexity: O(period) for the deque array.
/// </remarks>
[SkipLocalsInit]
public sealed class MonotonicDeque
{
    private readonly int[] _deque;
    private readonly int _period;
    private int _head;
    private int _count;

    /// <summary>
    /// Gets the current front index (the index of the current extremum).
    /// </summary>
    public int FrontIndex => _count > 0 ? _deque[_head] : -1;

    /// <summary>
    /// Gets the current element count in the deque.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Creates a new monotonic deque with the specified period.
    /// </summary>
    /// <param name="period">The sliding window size.</param>
    public MonotonicDeque(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 0");
        }

        _period = period;
        _deque = new int[period];
        _head = 0;
        _count = 0;
    }

    /// <summary>
    /// Pushes a value for maximum tracking (maintains non-increasing order).
    /// Smaller or equal values are removed from the back before pushing.
    /// </summary>
    /// <param name="logicalIndex">The logical index of the value (used for expiration).</param>
    /// <param name="value">The value to push.</param>
    /// <param name="buffer">The circular buffer containing values (indexed by logicalIndex % period).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PushMax(long logicalIndex, double value, double[] buffer)
    {
        // Expire old indices outside the window
        long expire = logicalIndex - _period;
        while (_count > 0 && _deque[_head] <= expire)
        {
            _head = (_head + 1) % _period;
            _count--;
        }

        // Pop elements from back that are <= value (maintain non-increasing order)
        while (_count > 0)
        {
            int backIdx = (_head + _count - 1) % _period;
            int bufIdx = _deque[backIdx] % _period;
            if (buffer[bufIdx] <= value)
            {
                _count--;
            }
            else
            {
                break;
            }
        }

        // Push new index
        int tail = (_head + _count) % _period;
        _deque[tail] = (int)logicalIndex;
        _count++;
    }

    /// <summary>
    /// Pushes a value for minimum tracking (maintains non-decreasing order).
    /// Larger or equal values are removed from the back before pushing.
    /// </summary>
    /// <param name="logicalIndex">The logical index of the value (used for expiration).</param>
    /// <param name="value">The value to push.</param>
    /// <param name="buffer">The circular buffer containing values (indexed by logicalIndex % period).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PushMin(long logicalIndex, double value, double[] buffer)
    {
        // Expire old indices outside the window
        long expire = logicalIndex - _period;
        while (_count > 0 && _deque[_head] <= expire)
        {
            _head = (_head + 1) % _period;
            _count--;
        }

        // Pop elements from back that are >= value (maintain non-decreasing order)
        while (_count > 0)
        {
            int backIdx = (_head + _count - 1) % _period;
            int bufIdx = _deque[backIdx] % _period;
            if (buffer[bufIdx] >= value)
            {
                _count--;
            }
            else
            {
                break;
            }
        }

        // Push new index
        int tail = (_head + _count) % _period;
        _deque[tail] = (int)logicalIndex;
        _count++;
    }

    /// <summary>
    /// Gets the current extremum value from the buffer.
    /// </summary>
    /// <param name="buffer">The circular buffer containing values.</param>
    /// <returns>The value at the front of the deque, or NaN if empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetExtremum(double[] buffer)
    {
        return _count > 0 ? buffer[_deque[_head] % _period] : double.NaN;
    }

    /// <summary>
    /// Resets the deque to empty state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _head = 0;
        _count = 0;
    }

    /// <summary>
    /// Rebuilds the max deque from scratch using the buffer contents.
    /// Used after bar corrections (isNew=false) to maintain consistency.
    /// </summary>
    /// <param name="buffer">The circular buffer containing values.</param>
    /// <param name="currentIndex">The current logical index.</param>
    /// <param name="count">The number of valid elements in the buffer.</param>
    public void RebuildMax(double[] buffer, long currentIndex, int count)
    {
        Reset();
        if (count == 0)
        {
            return;
        }

        long startLogical = currentIndex - count + 1;
        for (int i = 0; i < count; i++)
        {
            long logicalIndex = startLogical + i;
            int bufIdx = (int)(logicalIndex % _period);
            PushMax(logicalIndex, buffer[bufIdx], buffer);
        }
    }

    /// <summary>
    /// Rebuilds the min deque from scratch using the buffer contents.
    /// Used after bar corrections (isNew=false) to maintain consistency.
    /// </summary>
    /// <param name="buffer">The circular buffer containing values.</param>
    /// <param name="currentIndex">The current logical index.</param>
    /// <param name="count">The number of valid elements in the buffer.</param>
    public void RebuildMin(double[] buffer, long currentIndex, int count)
    {
        Reset();
        if (count == 0)
        {
            return;
        }

        long startLogical = currentIndex - count + 1;
        for (int i = 0; i < count; i++)
        {
            long logicalIndex = startLogical + i;
            int bufIdx = (int)(logicalIndex % _period);
            PushMin(logicalIndex, buffer[bufIdx], buffer);
        }
    }
}
