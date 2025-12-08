using System;
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
/// SMA calculates the arithmetic mean of the last n values.
/// Uses a RingBuffer for storage and manual running sum for O(1) complexity per update.
///
/// Calculation:
/// SMA = (P_n + P_(n-1) + ... + P_1) / n
///
/// O(1) update:
/// S_new = S_old - oldest + newest
/// SMA = S_new / n
///
/// IsHot:
/// Becomes true when the buffer is full (period samples processed).
/// </remarks>
[SkipLocalsInit]
public sealed class Sma : ITValuePublisher
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    private double _sum;
    private double _p_sum;
    private double _p_lastInput;
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

    public Sma(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += (item) => Update(item);
    }

    /// <summary>
    /// Current SMA value.
    /// </summary>
    public TValue Last { get; private set; }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateState(double val)
    {
        double removedValue = _buffer.Count == _buffer.Capacity ? _buffer.Oldest : 0.0;

        _sum = _sum - removedValue + val;

        _buffer.Add(val);

        _tickCount++;
        if (_buffer.IsFull && _tickCount >= ResyncInterval)
        {
            _tickCount = 0;
            _sum = _buffer.Sum();
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            double val = GetValidValue(input.Value);
            UpdateState(val);

            _p_sum = _sum;
            _p_lastInput = val;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _lastValidValue = _p_lastValidValue;
            double val = GetValidValue(input.Value);

            _sum = _p_sum - _p_lastInput + val;
            _buffer.UpdateNewest(val);
        }

        double result = _sum / _buffer.Count;
        Last = new TValue(input.Time, result);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries();

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Calculate(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Restore state
        int windowSize = Math.Min(len, _period);
        int startIndex = len - windowSize;

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

        _buffer.Clear();
        _sum = 0;
        _tickCount = 0;

        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(source.Values[i]);
            UpdateState(val);
        }

        _p_sum = _sum;
        _p_lastInput = source.Values[len - 1];
        _p_lastValidValue = _lastValidValue;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;

        const int StackAllocThreshold = 256;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : new double[period];

        double sum = 0;
        double lastValid = 0;
        int bufferIndex = 0;
        int i = 0;

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

        int tickCount = 0;
        for (; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            sum = sum - buffer[bufferIndex] + val;
            buffer[bufferIndex] = val;

            bufferIndex++;
            if (bufferIndex >= period)
                bufferIndex = 0;

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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateSimdCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int VectorWidth = 4;

        ref double srcRef = ref MemoryMarshal.GetReference(source);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        double invPeriod = 1.0 / period;

        int warmupEnd = Math.Min(period, len);
        double sum = 0;
        for (int i = 0; i < warmupEnd; i++)
        {
            sum += Unsafe.Add(ref srcRef, i);
            Unsafe.Add(ref outRef, i) = sum / (i + 1);
        }

        if (len <= period)
            return;

        var vInvPeriod = Vector256.Create(invPeriod);
        var vZero = Vector256<double>.Zero;
        int simdEnd = period + ((len - period) / VectorWidth) * VectorWidth;
        int tickCount = 0;

        for (int i = period; i < simdEnd; i += VectorWidth)
        {
            var vNew = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
            var vOld = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - period));

            var vDelta = Avx.Subtract(vNew, vOld);

            var vShift1 = Avx2.Permute4x64(vDelta.AsUInt64(), 0b_10_01_00_00).AsDouble();
            vShift1 = Avx.Blend(vZero, vShift1, 0b_1110);
            var vP1 = Avx.Add(vDelta, vShift1);

            var vShift2 = Avx2.Permute4x64(vP1.AsUInt64(), 0b_01_00_00_00).AsDouble();
            vShift2 = Avx.Blend(vZero, vShift2, 0b_1100);
            var vP2 = Avx.Add(vP1, vShift2);

            var vSumPrev = Vector256.Create(sum);
            var vSums = Avx.Add(vSumPrev, vP2);

            var vResult = Avx.Multiply(vSums, vInvPeriod);
            Vector256.StoreUnsafe(vResult, ref Unsafe.Add(ref outRef, i));

            sum = vSums.GetElement(3);

            tickCount += VectorWidth;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                int lastIdx = i + VectorWidth - 1;
                double recalcSum = 0;
                for (int k = 0; k < period; k++)
                {
                    recalcSum += Unsafe.Add(ref srcRef, lastIdx - k);
                }
                sum = recalcSum;
            }
        }

        for (int i = simdEnd; i < len; i++)
        {
            sum = sum - Unsafe.Add(ref srcRef, i - period) + Unsafe.Add(ref srcRef, i);
            Unsafe.Add(ref outRef, i) = sum * invPeriod;
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
        var resetSum = 0;
        _sum = resetSum;
        Last = default;
        _tickCount = 0;
    }
}
