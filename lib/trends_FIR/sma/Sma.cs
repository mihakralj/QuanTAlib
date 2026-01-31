using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// SMA: Simple Moving Average
/// </summary>
/// <remarks>
/// Arithmetic mean of the last n values using running sum for O(1) updates.
/// SIMD-accelerated batch processing (AVX-512/AVX2/NEON).
///
/// Calculation: <c>SMA = Σ(values) / n</c>.
/// </remarks>
/// <seealso href="Sma.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Sma : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;
    private bool _disposed;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Sum, double LastValidValue, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    /// <summary>
    /// Creates SMA with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Sma(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Sma({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    public Sma(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        source.Pub += _handler;
    }

    public Sma(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        _source = source;
        source.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode B: Streaming (Stateful)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// True if the SMA has enough data to produce valid results.
    /// SMA is "hot" when the buffer is full (has received at least 'period' values).
    /// </summary>
    public override bool IsHot => _buffer.IsFull;

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode C: Priming (The Bridge)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// Efficiently processes only the last 'Period' values required to sync the buffer.
    /// </summary>
    /// <param name="source">Historical data (only the last 'period' is actually needed)</param>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        // Reset state
        _buffer.Clear();
        _state = default;
        _p_state = default;

        // We only need the last 'period' values to fully restore state
        // If history is shorter than period, we take it all.
        int warmupLength = Math.Min(source.Length, WarmupPeriod);
        int startIndex = source.Length - warmupLength;

        // 1. Seed the LastValidValue (crucial for NaN handling)
        // We must look backwards from start of our warmup window to find a valid predecessor
        _state.LastValidValue = double.NaN;
        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (double.IsFinite(source[i]))
            {
                _state.LastValidValue = source[i];
                break;
            }
        }

        // If we didn't find a valid value in history, try finding one inside the warmup window
        if (double.IsNaN(_state.LastValidValue))
        {
            for (int i = startIndex; i < source.Length; i++)
            {
                if (double.IsFinite(source[i]))
                {
                    _state.LastValidValue = source[i];
                    break;
                }
            }
        }

        // 2. Feed the RingBuffer and State
        for (int i = startIndex; i < source.Length; i++)
        {
            double val = GetValidValue(source[i]);
            UpdateState(val);
        }

        // 3. Finalize State
        // Calculate the initial "Last" value so the indicator is ready to be read immediately
        double result = _buffer.Count > 0 ? _state.Sum / _buffer.Count : double.NaN;

        // Note: We can't infer accurate Time from a simple Span<double>,
        // so we leave 'Last' with default time or user updates it on next Tick.
        Last = new TValue(DateTime.MinValue, result);

        // Backup state for the next update cycle
        _p_state = _state;
    }

    /// <summary>
    /// Gets a valid input value, using last-value substitution for non-finite inputs.
    /// </summary>
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
        double removedValue = _buffer.Count == _buffer.Capacity ? _buffer.Oldest : 0.0;

        _state.Sum = Math.FusedMultiplyAdd(-1.0, removedValue, _state.Sum + val);

        _buffer.Add(val);

        _state.TickCount++;
        if (_buffer.IsFull && _state.TickCount >= ResyncInterval)
        {
            _state.TickCount = 0;
            _state.Sum = _buffer.RecalculateSum();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            // Capture previous state BEFORE any mutation
            _p_state = _state;

            double val = GetValidValue(input.Value);
            UpdateState(val);
        }
        else
        {
            // Restore scalar state to pre-mutation values (except Sum which we'll recalculate)
            var restoredState = _p_state;

            double val = GetValidValue(input.Value);

            // Update the buffer's newest value - this also updates buffer's internal sum
            _buffer.UpdateNewest(val);

            // Use buffer's authoritative sum (UpdateNewest already did the differential update internally)
            _state = restoredState with { Sum = _buffer.Sum };
            // Note: Resync is only done on isNew=true path via UpdateState()
        }

        double result = _buffer.Count > 0 ? _state.Sum / _buffer.Count : double.NaN;
        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode A: Batch (Stateless)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Calculates SMA for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="period">SMA period</param>
    /// <returns>SMA series</returns>
    public static TSeries Batch(TSeries source, int period)
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
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Try SIMD path for large, clean datasets
        // Requirements: SIMD support, large enough dataset, no NaN values
        const int SimdThreshold = 256;
        if (len >= SimdThreshold && !source.ContainsNonFinite())
        {
            if (Avx512F.IsSupported)
            {
                CalculateAvx512Core(source, output, period);
                return;
            }

            if (Avx2.IsSupported)
            {
                CalculateAvx2Core(source, output, period);
                return;
            }

            if (AdvSimd.Arm64.IsSupported)
            {
                CalculateNeonCore(source, output, period);
                return;
            }
        }

        // Scalar path with NaN handling
        CalculateScalarCore(source, output, period);
    }

    /// <summary>
    /// Runs a high-performance SIMD batch calculation on history and returns
    /// a "Hot" Sma instance ready to process the next tick immediately.
    /// </summary>
    /// <param name="source">Historical time series</param>
    /// <param name="period">SMA Period</param>
    /// <returns>A tuple containing the full calculation results and the hot indicator instance</returns>
    public static (TSeries Results, Sma Indicator) Calculate(TSeries source, int period)
    {
        var sma = new Sma(period);
        TSeries results = sma.Update(source);
        return (results, sma);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;

        const int StackAllocThreshold = 256;
        double[]? rented = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> buffer = rented != null
            ? rented.AsSpan(0, period)
            : stackalloc double[period];

        try
        {
            double sum = 0;
            double lastValid = double.NaN;

            // Find first valid value to seed lastValid
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(source[k]))
                {
                    lastValid = source[k];
                    break;
                }
            }

            int bufferIndex = 0;
            int i = 0;

            int warmupEnd = Math.Min(period, len);
            for (; i < warmupEnd; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                {
                    lastValid = val;
                }
                else
                {
                    val = lastValid;
                }

                sum += val;
                buffer[i] = val;
                output[i] = sum / (i + 1);
            }

            int tickCount = 0;
            for (; i < len; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                {
                    lastValid = val;
                }
                else
                {
                    val = lastValid;
                }

                sum = Math.FusedMultiplyAdd(-1.0, buffer[bufferIndex], sum + val);
                buffer[bufferIndex] = val;

                bufferIndex++;
                if (bufferIndex >= period)
                {
                    bufferIndex = 0;
                }

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
        finally
        {
            if (rented != null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateAvx512Core(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int VectorWidth = 8;

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
        {
            return;
        }

        var vInvPeriod = Vector512.Create(invPeriod);
        int simdEnd = period + (len - period) / VectorWidth * VectorWidth;
        int tickCount = 0;

        for (int i = period; i < simdEnd; i += VectorWidth)
        {
            var vNew = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
            var vOld = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - period));

            var vDelta = Avx512F.Subtract(vNew, vOld);

            // Prefix sum of Delta
            var vShift1 = Vector512.Create(0.0, vDelta.GetElement(0), vDelta.GetElement(1), vDelta.GetElement(2), vDelta.GetElement(3), vDelta.GetElement(4), vDelta.GetElement(5), vDelta.GetElement(6));
            var vP1 = Avx512F.Add(vDelta, vShift1);

            var vShift2 = Vector512.Create(0.0, 0.0, vP1.GetElement(0), vP1.GetElement(1), vP1.GetElement(2), vP1.GetElement(3), vP1.GetElement(4), vP1.GetElement(5));
            var vP2 = Avx512F.Add(vP1, vShift2);

            var vShift4 = Vector512.Create(0.0, 0.0, 0.0, 0.0, vP2.GetElement(0), vP2.GetElement(1), vP2.GetElement(2), vP2.GetElement(3));
            var vP4 = Avx512F.Add(vP2, vShift4);

            var vSumPrev = Vector512.Create(sum);
            var vSums = Avx512F.Add(vSumPrev, vP4);

            var vResult = Avx512F.Multiply(vSums, vInvPeriod);
            vResult.StoreUnsafe(ref Unsafe.Add(ref outRef, i));

            sum = vSums.GetElement(7);

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
            double newVal = Unsafe.Add(ref srcRef, i);
            double oldVal = Unsafe.Add(ref srcRef, i - period);
            sum = Math.FusedMultiplyAdd(-1.0, oldVal, sum + newVal);
            Unsafe.Add(ref outRef, i) = sum * invPeriod;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateAvx2Core(ReadOnlySpan<double> source, Span<double> output, int period)
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
        {
            return;
        }

        var vInvPeriod = Vector256.Create(invPeriod);
        var vZero = Vector256<double>.Zero;
        int simdEnd = period + (len - period) / VectorWidth * VectorWidth;
        int tickCount = 0;

        for (int i = period; i < simdEnd; i += VectorWidth)
        {
            var vNew = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
            var vOld = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - period));

            var vDelta = Avx.Subtract(vNew, vOld);

            var vShift1 = Avx2.Permute4x64(vDelta.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
            vShift1 = Avx.Blend(vZero, vShift1, 0b_1110);
            var vP1 = Avx.Add(vDelta, vShift1);

            var vShift2 = Avx2.Permute4x64(vP1.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
            vShift2 = Avx.Blend(vZero, vShift2, 0b_1100);
            var vP2 = Avx.Add(vP1, vShift2);

            var vSumPrev = Vector256.Create(sum);
            var vSums = Avx.Add(vSumPrev, vP2);

            var vResult = Fma.MultiplyAdd(vSums, vInvPeriod, vZero);
            vResult.StoreUnsafe(ref Unsafe.Add(ref outRef, i));

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
            double newVal = Unsafe.Add(ref srcRef, i);
            double oldVal = Unsafe.Add(ref srcRef, i - period);
            sum = Math.FusedMultiplyAdd(-1.0, oldVal, sum + newVal);
            Unsafe.Add(ref outRef, i) = sum * invPeriod;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateNeonCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int VectorWidth = 2;

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
        {
            return;
        }

        var vInvPeriod = Vector128.Create(invPeriod);
        int simdEnd = period + (len - period) / VectorWidth * VectorWidth;
        int tickCount = 0;

        for (int i = period; i < simdEnd; i += VectorWidth)
        {
            var vNew = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
            var vOld = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - period));

            var vDelta = AdvSimd.Arm64.Subtract(vNew, vOld);

            // Prefix sum of Delta: [d0, d0+d1]
            double d0 = vDelta.GetElement(0);
            double d1 = vDelta.GetElement(1);
            double ps0 = sum + d0;
            double ps1 = ps0 + d1;

            var vSums = Vector128.Create(ps0, ps1);

            var vResult = AdvSimd.Arm64.Multiply(vSums, vInvPeriod);
            vResult.StoreUnsafe(ref Unsafe.Add(ref outRef, i));

            sum = ps1;

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
            double newVal = Unsafe.Add(ref srcRef, i);
            double oldVal = Unsafe.Add(ref srcRef, i - period);
            sum = Math.FusedMultiplyAdd(-1.0, oldVal, sum + newVal);
            Unsafe.Add(ref outRef, i) = sum * invPeriod;
        }
    }

    /// <summary>
    /// Resets the SMA state.
    /// </summary>
    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }

    /// <summary>
    /// Disposes the indicator and unsubscribes from the source.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= _handler;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
