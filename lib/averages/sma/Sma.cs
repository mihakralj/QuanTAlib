using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// SMA: Simple Moving Average
/// </summary>
/// <remarks>
/// SMA calculates the arithmetic mean of the last N values.
/// Uses a RingBuffer for storage and manual running sum for O(1) operations.
///
/// Key characteristics:
/// - Equal weighting of all values in the period
/// - No lag bias - responds equally to all values in window
/// - Smooth output with good noise reduction
/// - O(1) time complexity for both update and bar correction
/// - O(1) space complexity for state save/restore (scalars only)
///
/// Calculation method:
/// SMA = Sum(values in period) / period
///
/// Bar correction (isNew=false):
/// - Restores to state after last isNew=true
/// - Then replaces the last value with new correction value
/// - All O(1) using scalar state
///
/// Sources:
/// - https://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:moving_averages
/// - https://www.investopedia.com/terms/s/sma.asp
/// </remarks>
[SkipLocalsInit]
public sealed class Sma
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    // Running sum maintained separately for O(1) bar correction
    private double _sum;
    private double _p_sum;          // Sum AFTER last isNew=true (for correction restore)
    private double _p_lastInput;    // Input that was added on last isNew=true
    private double _lastValidValue;
    private double _p_lastValidValue;
    private int _tickCount;         // Counter for periodic sum resync

    // Resync interval: recalculate sum from buffer every N ticks to prevent drift
    private const int ResyncInterval = 1000;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creates SMA with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Sma(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Sma({period})";
    }

    /// <summary>
    /// Current SMA value.
    /// </summary>
    public TValue Value { get; private set; }

    /// <summary>
    /// True if the SMA has enough data to produce valid results.
    /// SMA is "hot" when the buffer is full (has received at least 'period' values).
    /// </summary>
    public bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Gets a valid input value, using last-value substitution for non-finite inputs.
    /// </summary>
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

    /// <summary>
    /// Updates internal state with a new value.
    /// Shared logic for both streaming and batch-reconstruction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateState(double val)
    {
        // Calculate what to remove from sum (oldest value if buffer full)
        double removedValue = _buffer.Count == _buffer.Capacity ? _buffer.Oldest : 0.0;

        // Update sum: remove oldest, add newest
        _sum = _sum - removedValue + val;

        // Update buffer
        _buffer.Add(val);

        // Periodic resync: recalculate sum from scratch to eliminate floating-point drift
        _tickCount++;
        if (_buffer.IsFull && _tickCount >= ResyncInterval)
        {
            _tickCount = 0;
            _sum = _buffer.Sum();
        }
    }

    /// <summary>
    /// Updates SMA with the given value.
    /// O(1) for both isNew=true and isNew=false.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True for new bar, false for update to current bar (default: true)</param>
    /// <returns>Current SMA value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            // Get valid value (this may update _lastValidValue)
            double val = GetValidValue(input.Value);

            UpdateState(val);

            // Save state AFTER this update for potential future corrections
            _p_sum = _sum;
            _p_lastInput = val;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            // Bar correction: restore to state AFTER last isNew=true, then swap last value
            // Restore _lastValidValue BEFORE calling GetValidValue
            _lastValidValue = _p_lastValidValue;

            // Get valid value (this may update _lastValidValue)
            double val = GetValidValue(input.Value);

            // _p_sum is the sum AFTER the last isNew=true completed
            // _p_lastInput is the value that was added on last isNew=true
            // We want: new_sum = _p_sum - _p_lastInput + val
            _sum = _p_sum - _p_lastInput + val;

            // Update buffer's newest value
            _buffer.UpdateNewest(val);
        }

        double result = _sum / _buffer.Count;
        Value = new TValue(input.Time, result);
        return Value;
    }

    /// <summary>
    /// Updates SMA with the entire series.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>SMA series</returns>
    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries(new List<long>(), new List<double>());

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        var sourceValues = source.Values;
        var sourceTimes = source.Times;

        // 1. Fast Batch Calculation (SIMD optimized)
        Calculate(sourceValues, vSpan, _period);

        // 2. Copy Times
        sourceTimes.CopyTo(tSpan);

        // 3. Reconstruct State for subsequent updates
        // We need to restore _buffer, _sum, and _lastValidValue to what they would be
        // if we had processed the series sequentially.

        // Find the last valid value before the reconstruction window
        // The reconstruction window is the last 'period' elements (or less if len < period)
        int windowSize = Math.Min(len, _period);
        int startIndex = len - windowSize;

        // Restore _lastValidValue from before the window
        if (startIndex > 0)
        {
            // Scan backwards to find last valid value
            for (int i = startIndex - 1; i >= 0; i--)
            {
                if (double.IsFinite(sourceValues[i]))
                {
                    _lastValidValue = sourceValues[i];
                    break;
                }
            }
        }
        else
        {
            _lastValidValue = 0; // Reset if starting from 0
        }

        // Rebuild buffer and sum from last 'period' values using shared logic
        _buffer.Clear();
        _sum = 0;
        _tickCount = 0;

        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(sourceValues[i]);
            UpdateState(val);
        }

        // Save state for potential future corrections
        _p_sum = _sum;
        _p_lastInput = sourceValues[len - 1];
        _p_lastValidValue = _lastValidValue;

        Value = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates SMA for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="period">SMA period</param>
    /// <returns>SMA series</returns>
    public static TSeries Calculate(TSeries source, int period)
    {
        var sma = new Sma(period);
        return sma.Update(source);
    }

    /// <summary>
    /// Calculates SMA in-place, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// Uses stackalloc circular buffer for NaN-safe sliding window calculation.
    /// Automatically uses SIMD acceleration for large, clean datasets.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output span (must be same length as source)</param>
    /// <param name="period">SMA period (must be > 0)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = source.Length;
        if (len == 0) return;

        // Try SIMD path for large, clean datasets
        // Requirements: AVX2 support, large enough dataset, no NaN values
        const int SimdThreshold = 256;
        if (Avx2.IsSupported && len >= SimdThreshold && !HasNonFiniteValues(source))
        {
            CalculateSimdCore(source, output, period);
            return;
        }

        // Scalar path with NaN handling
        CalculateScalarCore(source, output, period);
    }

    /// <summary>
    /// Scalar implementation with NaN handling via last-value substitution.
    /// Uses circular buffer for sliding window calculation.
    /// Optimized with split loops and periodic resync.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;

        // Use stackalloc for small periods, otherwise fall back to heap allocation
        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double sum = 0;
        double lastValid = 0;
        int bufferIndex = 0;
        int i = 0;

        // Phase 1: Warmup (0 to period-1)
        // No need to remove oldest value, just accumulate
        int warmupEnd = Math.Min(period, len);
        for (; i < warmupEnd; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            sum += val;
            buffer[i] = val;
            output[i] = sum / (i + 1);
        }

        // Phase 2: Hot loop (period to len)
        // Buffer is full, remove oldest, add newest
        // Optimized buffer indexing (no modulo)
        int tickCount = 0;
        for (; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            // Remove oldest, add newest
            sum = sum - buffer[bufferIndex] + val;
            buffer[bufferIndex] = val;

            // Increment buffer index with wrap-around check (faster than modulo)
            bufferIndex++;
            if (bufferIndex >= period)
                bufferIndex = 0;

            output[i] = sum / period;

            // Periodic resync every 1000 ticks
            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                // Recalculate sum from buffer to prevent drift
                double recalcSum = 0;
                for (int k = 0; k < period; k++)
                {
                    recalcSum += buffer[k];
                }
                sum = recalcSum;
            }
        }
    }

    /// <summary>
    /// SIMD-optimized implementation for SMA calculation.
    /// Processes 4 consecutive values per iteration using AVX2 (Vector256&lt;double&gt;).
    /// Assumes input contains no NaN/Infinity values.
    /// </summary>
    /// <remarks>
    /// Key insight: For consecutive positions i, i+1, i+2, i+3:
    /// - sum[i+1] = sum[i] - src[i-period+1] + src[i+1]
    /// - We can vectorize the load of 4 "leaving" values and 4 "entering" values
    /// - Then use prefix-sum style to compute the 4 sums from one base sum
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void CalculateSimdCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int VectorWidth = 4; // Vector256<double> holds 4 doubles

        fixed (double* srcPtr = source)
        fixed (double* outPtr = output)
        {
            double invPeriod = 1.0 / period;

            // Phase 1: Warmup - scalar processing until buffer is full
            int warmupEnd = Math.Min(period, len);
            double sum = 0;
            for (int i = 0; i < warmupEnd; i++)
            {
                sum += srcPtr[i];
                outPtr[i] = sum / (i + 1);
            }

            if (len <= period)
                return;

            // Phase 2: SIMD hot loop
            // Uses prefix-sum approach to break dependency chain
            var vInvPeriod = Vector256.Create(invPeriod);
            var vZero = Vector256<double>.Zero;
            int simdEnd = period + ((len - period) / VectorWidth) * VectorWidth;
            int tickCount = 0;

            for (int i = period; i < simdEnd; i += VectorWidth)
            {
                // Load 4 entering values and 4 leaving values
                var vNew = Avx.LoadVector256(srcPtr + i);
                var vOld = Avx.LoadVector256(srcPtr + i - period);

                // Delta = New - Old
                var vDelta = Avx.Subtract(vNew, vOld);

                // Prefix sum of Deltas
                // Step 1: Shift right by 1 element (insert 0)
                // [D0, D1, D2, D3] -> [0, D0, D1, D2]
                var vShift1 = Avx2.Permute4x64(vDelta.AsUInt64(), 0b_10_01_00_00).AsDouble();
                vShift1 = Avx.Blend(vZero, vShift1, 0b_1110);
                var vP1 = Avx.Add(vDelta, vShift1); // [D0, D0+D1, D1+D2, D2+D3]

                // Step 2: Shift right by 2 elements (insert 0)
                // [D0, D0+D1, D1+D2, D2+D3] -> [0, 0, D0, D0+D1]
                var vShift2 = Avx2.Permute4x64(vP1.AsUInt64(), 0b_01_00_00_00).AsDouble();
                vShift2 = Avx.Blend(vZero, vShift2, 0b_1100);
                var vP2 = Avx.Add(vP1, vShift2); // [D0, D0+D1, D0+D1+D2, D0+D1+D2+D3]

                // Add previous sum to all
                var vSumPrev = Vector256.Create(sum);
                var vSums = Avx.Add(vSumPrev, vP2);

                // Store result
                var vResult = Avx.Multiply(vSums, vInvPeriod);
                Avx.Store(outPtr + i, vResult);

                // Update sum for next iteration (last element of vSums)
                sum = vSums.GetElement(3);

                // Periodic resync every 1000 ticks
                tickCount += VectorWidth;
                if (tickCount >= ResyncInterval)
                {
                    tickCount = 0;
                    // Recalculate sum from scratch using the window ending at i + VectorWidth - 1
                    // Window: [i + VectorWidth - period ... i + VectorWidth - 1]
                    int lastIdx = i + VectorWidth - 1;
                    double recalcSum = 0;
                    for (int k = 0; k < period; k++)
                    {
                        recalcSum += srcPtr[lastIdx - k];
                    }
                    sum = recalcSum;
                }
            }

            // Phase 3: Scalar tail
            for (int i = simdEnd; i < len; i++)
            {
                sum = sum - srcPtr[i - period] + srcPtr[i];
                outPtr[i] = sum * invPeriod;
            }
        }
    }

    /// <summary>
    /// Checks if span contains any non-finite values (NaN or Infinity).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasNonFiniteValues(ReadOnlySpan<double> span)
    {
        for (int idx = 0; idx < span.Length; idx++)
        {
            if (!double.IsFinite(span[idx]))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Resets the SMA state.
    /// </summary>
    public void Reset()
    {
        _buffer.Clear();
        _sum = 0;
        _p_sum = 0;
        _p_lastInput = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        _tickCount = 0;
        Value = default;
    }
}
