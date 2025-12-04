using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// WMA: Weighted Moving Average
/// </summary>
/// <remarks>
/// WMA applies linear weighting to data points, giving more weight to recent values.
/// Uses dual running sums for O(1) complexity per update.
///
/// Key characteristics:
/// - Linear weighting: newest value has weight n, oldest has weight 1
/// - More responsive than SMA due to emphasis on recent data
/// - Less lag than SMA, but more than EMA
/// - O(1) time complexity for both update and bar correction
/// - O(1) space complexity for state save/restore (scalars only)
///
/// Calculation method:
/// WMA = (n*P_n + (n-1)*P_(n-1) + ... + 2*P_2 + 1*P_1) / (n*(n+1)/2)
///
/// O(1) update formula:
/// S_new = S - oldest + newest
/// W_new = W - S_old + n*newest
/// WMA = W_new / divisor
///
/// Bar correction (isNew=false):
/// - Restores to state after last isNew=true
/// - Then replaces the last value with new correction value
/// - All O(1) using scalar state
///
/// Sources:
/// - https://www.investopedia.com/terms/w/weightedaverage.asp
/// - https://school.stockcharts.com/doku.php?id=technical_indicators:weighted_moving_average
/// </remarks>
[SkipLocalsInit]
public sealed class Wma
{
    private readonly int _period;
    private readonly double _divisor;
    private readonly RingBuffer _buffer;

    // Dual running sums for O(1) WMA calculation
    private double _sum;      // Simple sum of values in window
    private double _wsum;     // Weighted sum of values in window
    private double _p_sum;    // Sum AFTER last isNew=true (for correction restore)
    private double _p_wsum;   // Weighted sum AFTER last isNew=true
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
    /// Creates WMA with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Wma(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _divisor = period * (period + 1) * 0.5;
        _buffer = new RingBuffer(period);
        Name = $"Wma({period})";
    }

    /// <summary>
    /// Current WMA value.
    /// </summary>
    public TValue Value { get; private set; }

    /// <summary>
    /// True if the WMA has enough data to produce valid results.
    /// WMA is "hot" when the buffer is full (has received at least 'period' values).
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
        if (_buffer.IsFull)
        {
            // Buffer is full: O(1) update using dual running sums
            double oldSum = _sum;  // Capture before update
            double oldest = _buffer.Oldest;
            _sum = _sum - oldest + val;
            _wsum = _wsum - oldSum + (_period * val);
        }
        else
        {
            // Warmup phase: incrementally build sums
            int count = _buffer.Count + 1;
            _sum += val;
            _wsum += count * val;
        }

        // Update buffer
        _buffer.Add(val);

        // Periodic resync: recalculate sums from scratch to eliminate floating-point drift
        _tickCount++;
        if (_buffer.IsFull && _tickCount >= ResyncInterval)
        {
            _tickCount = 0;
            double recalcSum = 0;
            double recalcWsum = 0;
            int weight = 1;
            foreach (double item in _buffer)
            {
                recalcSum += item;
                recalcWsum += weight * item;
                weight++;
            }
            _sum = recalcSum;
            _wsum = recalcWsum;
        }
    }

    /// <summary>
    /// Updates WMA with the given value.
    /// O(1) for both isNew=true and isNew=false.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True for new bar, false for update to current bar (default: true)</param>
    /// <returns>Current WMA value</returns>
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
            _p_wsum = _wsum;
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

            // Restore sums to state after last isNew=true
            _sum = _p_sum;
            _wsum = _p_wsum;

            // Correction: replace _p_lastInput with val
            // S_corrected = S - lastInput + val
            // W_corrected = W + weight*(val - lastInput), where weight = period (if full) or count (if warmup)
            int weight = _buffer.IsFull ? _period : _buffer.Count;
            _sum = _sum - _p_lastInput + val;
            _wsum += weight * (val - _p_lastInput);

            // Update buffer's newest value
            _buffer.UpdateNewest(val);
        }

        // Calculate WMA using current divisor (handles warmup)
        double currentDivisor = _buffer.IsFull ? _divisor : _buffer.Count * (_buffer.Count + 1) * 0.5;
        double result = _wsum / currentDivisor;
        Value = new TValue(input.Time, result);
        return Value;
    }

    /// <summary>
    /// Updates WMA with the entire series.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>WMA series</returns>
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
        // We need to restore _buffer, _sum, _wsum, and _lastValidValue
        
        // Find the last valid value before the reconstruction window
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

        // Rebuild buffer and sums from last 'period' values using shared logic
        _buffer.Clear();
        _sum = 0;
        _wsum = 0;
        _tickCount = 0;

        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(sourceValues[i]);
            UpdateState(val);
        }

        // Save state for potential future corrections
        _p_sum = _sum;
        _p_wsum = _wsum;
        _p_lastInput = sourceValues[len - 1];
        _p_lastValidValue = _lastValidValue;

        Value = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates WMA for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="period">WMA period</param>
    /// <returns>WMA series</returns>
    public static TSeries Calculate(TSeries source, int period)
    {
        var wma = new Wma(period);
        return wma.Update(source);
    }

    /// <summary>
    /// Calculates WMA in-place, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// Uses O(1) dual running sum algorithm.
    /// Automatically uses SIMD acceleration for large, clean datasets.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output span (must be same length as source)</param>
    /// <param name="period">WMA period (must be > 0)</param>
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
        double divisor = period * (period + 1) * 0.5;
        double sum = 0;
        double wsum = 0;
        double lastValid = 0;

        // Ring buffer simulation
        Span<double> buffer = period <= 512 ? stackalloc double[period] : new double[period];
        int bufferIdx = 0;
        int i = 0;

        // Phase 1: Warmup (0 to period-1)
        int warmupEnd = Math.Min(period, len);
        for (; i < warmupEnd; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            sum += val;
            wsum += (i + 1) * val;
            buffer[i] = val;

            double currentDivisor = (i + 1) * (i + 2) * 0.5;
            output[i] = wsum / currentDivisor;
        }

        // Phase 2: Hot loop (period to len)
        int tickCount = 0;
        for (; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            // O(1) update using dual running sums
            double oldSum = sum;
            double oldest = buffer[bufferIdx];
            sum = sum - oldest + val;
            wsum = wsum - oldSum + (period * val);

            buffer[bufferIdx] = val;
            bufferIdx++;
            if (bufferIdx >= period)
                bufferIdx = 0;

            output[i] = wsum / divisor;

            // Periodic resync every 1000 ticks
            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                // Recalculate sums from buffer to prevent drift
                double recalcSum = 0;
                double recalcWsum = 0;
                // Buffer contains values in order: [oldest ... newest] relative to current bufferIdx
                // Actually buffer is circular.
                // Oldest is at bufferIdx (which we just wrote to, so it's actually newest now? No, we incremented bufferIdx)
                // bufferIdx points to the *next* overwrite location, which holds the *oldest* value.
                // So buffer[bufferIdx] is oldest (weight 1).
                // buffer[bufferIdx+1] is 2nd oldest (weight 2).
                // ...
                // buffer[bufferIdx-1] is newest (weight period).
                
                for (int k = 0; k < period; k++)
                {
                    int idx = (bufferIdx + k) % period; // Use modulo here for simplicity in resync (rare)
                    // Wait, modulo is slow.
                    if (idx >= period) idx -= period; // Manual modulo
                    
                    double v = buffer[idx];
                    recalcSum += v;
                    recalcWsum += (k + 1) * v;
                }
                sum = recalcSum;
                wsum = recalcWsum;
            }
        }
    }

    /// <summary>
    /// SIMD-optimized implementation for WMA calculation.
    /// Uses double prefix-sum approach to vectorize the coupled recurrence.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void CalculateSimdCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int VectorWidth = 4;

        fixed (double* srcPtr = source)
        fixed (double* outPtr = output)
        {
            double divisor = period * (period + 1) * 0.5;
            double invDivisor = 1.0 / divisor;

            // Phase 1: Warmup - scalar
            int warmupEnd = Math.Min(period, len);
            double sum = 0;
            double wsum = 0;
            for (int i = 0; i < warmupEnd; i++)
            {
                double val = srcPtr[i];
                sum += val;
                wsum += (i + 1) * val;
                double currentDivisor = (i + 1) * (i + 2) * 0.5;
                outPtr[i] = wsum / currentDivisor;
            }

            if (len <= period)
                return;

            // Phase 2: SIMD hot loop
            var vInvDivisor = Vector256.Create(invDivisor);
            var vPeriod = Vector256.Create((double)period);
            var vZero = Vector256<double>.Zero;
            int simdEnd = period + ((len - period) / VectorWidth) * VectorWidth;
            
            // Initialize vector state
            var vSumState = Vector256.Create(sum);
            var vWsumState = Vector256.Create(wsum);

            int idx = period;
            while (idx < simdEnd)
            {
                int nextSync = Math.Min(simdEnd, idx + ResyncInterval);
                
                // Inner hot loop without branches
                // Unrolled 2x (process 8 doubles per iteration)
                // Optimized Parallel Execution:
                // - Parallel prefix sums for DeltaS
                // - Fast S_shifted calculation using (S - DeltaS)
                // - Parallel prefix sums for U
                int unrolledSync = nextSync - (2 * VectorWidth);
                for (; idx <= unrolledSync; idx += 2 * VectorWidth)
                {
                    // Load data for both iterations
                    var vNew1 = Avx.LoadVector256(srcPtr + idx);
                    var vOld1 = Avx.LoadVector256(srcPtr + idx - period);
                    var vNew2 = Avx.LoadVector256(srcPtr + idx + VectorWidth);
                    var vOld2 = Avx.LoadVector256(srcPtr + idx + VectorWidth - period);

                    // 1. Update Sum (S) - Parallel Prefix Sums
                    var vDeltaS1 = Avx.Subtract(vNew1, vOld1);
                    var vDeltaS2 = Avx.Subtract(vNew2, vOld2);

                    // Prefix Sum DeltaS1
                    var vShiftS1_1 = Avx2.Permute4x64(vDeltaS1.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vShiftS1_1 = Avx.Blend(vZero, vShiftS1_1, 0b_1110);
                    var vPS_DeltaS1 = Avx.Add(vDeltaS1, vShiftS1_1);
                    var vShiftS2_1 = Avx2.Permute4x64(vPS_DeltaS1.AsUInt64(), 0b_01_00_00_00).AsDouble();
                    vShiftS2_1 = Avx.Blend(vZero, vShiftS2_1, 0b_1100);
                    vPS_DeltaS1 = Avx.Add(vPS_DeltaS1, vShiftS2_1);

                    // Prefix Sum DeltaS2
                    var vShiftS1_2 = Avx2.Permute4x64(vDeltaS2.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vShiftS1_2 = Avx.Blend(vZero, vShiftS1_2, 0b_1110);
                    var vPS_DeltaS2 = Avx.Add(vDeltaS2, vShiftS1_2);
                    var vShiftS2_2 = Avx2.Permute4x64(vPS_DeltaS2.AsUInt64(), 0b_01_00_00_00).AsDouble();
                    vShiftS2_2 = Avx.Blend(vZero, vShiftS2_2, 0b_1100);
                    vPS_DeltaS2 = Avx.Add(vPS_DeltaS2, vShiftS2_2);

                    // Combine Sums
                    var vSums1 = Avx.Add(vSumState, vPS_DeltaS1);
                    var vLastS1 = Avx2.Permute4x64(vSums1.AsUInt64(), 0b_11_11_11_11).AsDouble();
                    var vSums2 = Avx.Add(vLastS1, vPS_DeltaS2);

                    // 2. Update Weighted Sum (W)
                    // Optimization: S_shifted = S - DeltaS
                    // This avoids expensive Permute/Blend operations
                    var vSumsShifted1 = Avx.Subtract(vSums1, vDeltaS1);
                    var vSumsShifted2 = Avx.Subtract(vSums2, vDeltaS2);

                    Vector256<double> vU1, vU2;
                    if (Fma.IsSupported)
                    {
                        vU1 = Fma.MultiplySubtract(vPeriod, vNew1, vSumsShifted1);
                        vU2 = Fma.MultiplySubtract(vPeriod, vNew2, vSumsShifted2);
                    }
                    else
                    {
                        vU1 = Avx.Subtract(Avx.Multiply(vPeriod, vNew1), vSumsShifted1);
                        vU2 = Avx.Subtract(Avx.Multiply(vPeriod, vNew2), vSumsShifted2);
                    }

                    // Prefix Sum W1
                    var vShiftW1_1 = Avx2.Permute4x64(vU1.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vShiftW1_1 = Avx.Blend(vZero, vShiftW1_1, 0b_1110);
                    var vPW1_1 = Avx.Add(vU1, vShiftW1_1);
                    var vShiftW2_1 = Avx2.Permute4x64(vPW1_1.AsUInt64(), 0b_01_00_00_00).AsDouble();
                    vShiftW2_1 = Avx.Blend(vZero, vShiftW2_1, 0b_1100);
                    var vPW2_1 = Avx.Add(vPW1_1, vShiftW2_1);

                    // Prefix Sum W2
                    var vShiftW1_2 = Avx2.Permute4x64(vU2.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vShiftW1_2 = Avx.Blend(vZero, vShiftW1_2, 0b_1110);
                    var vPW1_2 = Avx.Add(vU2, vShiftW1_2);
                    var vShiftW2_2 = Avx2.Permute4x64(vPW1_2.AsUInt64(), 0b_01_00_00_00).AsDouble();
                    vShiftW2_2 = Avx.Blend(vZero, vShiftW2_2, 0b_1100);
                    var vPW2_2 = Avx.Add(vPW1_2, vShiftW2_2);

                    // Combine Weighted Sums
                    var vWsums1 = Avx.Add(vWsumState, vPW2_1);
                    var vLastW1 = Avx2.Permute4x64(vWsums1.AsUInt64(), 0b_11_11_11_11).AsDouble();
                    var vWsums2 = Avx.Add(vLastW1, vPW2_2);

                    // Store results
                    Avx.Store(outPtr + idx, Avx.Multiply(vWsums1, vInvDivisor));
                    Avx.Store(outPtr + idx + VectorWidth, Avx.Multiply(vWsums2, vInvDivisor));

                    // Update state for next iteration
                    vSumState = Avx2.Permute4x64(vSums2.AsUInt64(), 0b_11_11_11_11).AsDouble();
                    vWsumState = Avx2.Permute4x64(vWsums2.AsUInt64(), 0b_11_11_11_11).AsDouble();
                }

                // Handle remaining vectors (if any)
                for (; idx < nextSync; idx += VectorWidth)
                {
                    // Load 4 entering values and 4 leaving values
                    var vNew = Avx.LoadVector256(srcPtr + idx);
                    var vOld = Avx.LoadVector256(srcPtr + idx - period);

                    // 1. Update Sum (S)
                    // Delta S = New - Old
                    var vDeltaS = Avx.Subtract(vNew, vOld);

                    // Prefix sum of Delta S
                    var vShiftS1 = Avx2.Permute4x64(vDeltaS.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vShiftS1 = Avx.Blend(vZero, vShiftS1, 0b_1110);
                    var vPS1 = Avx.Add(vDeltaS, vShiftS1);

                    var vShiftS2 = Avx2.Permute4x64(vPS1.AsUInt64(), 0b_01_00_00_00).AsDouble();
                    vShiftS2 = Avx.Blend(vZero, vShiftS2, 0b_1100);
                    var vPS2 = Avx.Add(vPS1, vShiftS2);

                    // Add previous sum state
                    var vSums = Avx.Add(vSumState, vPS2);

                    // 2. Update Weighted Sum (W)
                    // Shift vSums right and insert sum (S_t) at pos 0
                    var vSumsShifted = Avx2.Permute4x64(vSums.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vSumsShifted = Avx.Blend(vSumState, vSumsShifted, 0b_1110);

                    // U = (n * New) - S_shifted
                    var vTerm1 = Avx.Multiply(vPeriod, vNew);
                    var vU = Avx.Subtract(vTerm1, vSumsShifted);

                    // Prefix sum of U
                    var vShiftW1 = Avx2.Permute4x64(vU.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vShiftW1 = Avx.Blend(vZero, vShiftW1, 0b_1110);
                    var vPW1 = Avx.Add(vU, vShiftW1);

                    var vShiftW2 = Avx2.Permute4x64(vPW1.AsUInt64(), 0b_01_00_00_00).AsDouble();
                    vShiftW2 = Avx.Blend(vZero, vShiftW2, 0b_1100);
                    var vPW2 = Avx.Add(vPW1, vShiftW2);

                    // Add previous wsum state
                    var vWsums = Avx.Add(vWsumState, vPW2);

                    // Store result
                    var vResult = Avx.Multiply(vWsums, vInvDivisor);
                    Avx.Store(outPtr + idx, vResult);

                    // Update state for next iteration
                    vSumState = Avx2.Permute4x64(vSums.AsUInt64(), 0b_11_11_11_11).AsDouble();
                    vWsumState = Avx2.Permute4x64(vWsums.AsUInt64(), 0b_11_11_11_11).AsDouble();
                }

                // Periodic resync
                if (idx < len)
                {
                    // Extract scalar state for resync logic
                    sum = vSumState.GetElement(0);
                    wsum = vWsumState.GetElement(0);

                    // Recalculate sums from scratch
                    int lastIdx = idx - 1;
                    double recalcSum = 0;
                    double recalcWsum = 0;
                    for (int k = 0; k < period; k++)
                    {
                        double val = srcPtr[lastIdx - k];
                        recalcSum += val;
                        recalcWsum += (period - k) * val;
                    }
                    sum = recalcSum;
                    wsum = recalcWsum;
                    
                    // Update vector state after resync
                    vSumState = Vector256.Create(sum);
                    vWsumState = Vector256.Create(wsum);
                }
            }
            
            // Extract final scalar state for tail
            sum = vSumState.GetElement(0);
            wsum = vWsumState.GetElement(0);

            // Phase 3: Scalar tail
            for (; idx < len; idx++)
            {
                double val = srcPtr[idx];
                double oldSum = sum;
                double oldest = srcPtr[idx - period];
                sum = sum - oldest + val;
                wsum = wsum - oldSum + (period * val);
                outPtr[idx] = wsum * invDivisor;
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
    /// Resets the WMA state.
    /// </summary>
    public void Reset()
    {
        _buffer.Clear();
        _sum = 0;
        _wsum = 0;
        _p_sum = 0;
        _p_wsum = 0;
        _p_lastInput = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Value = default;
    }
}
