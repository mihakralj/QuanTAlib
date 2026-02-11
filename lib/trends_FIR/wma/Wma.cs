using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// WMA: Weighted Moving Average
/// </summary>
/// <remarks>
/// Linear weighting giving more weight to recent values. O(1) via dual running sums.
/// SIMD-accelerated batch processing (AVX-512/AVX2/NEON).
///
/// Calculation: <c>WMA = Σ(w_i × P_i) / Σ(w_i)</c> where <c>w_i = i</c>.
/// </remarks>
/// <seealso href="Wma.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Wma : AbstractBase
{
    private readonly int _period;
    private readonly double _divisor;
    private readonly RingBuffer _buffer;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _handler;
    private bool _disposed;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Sum, double WSum, double LastInput, double LastValidValue, int TickCount, bool HasSeenValidData);
    private State _state;
    private State _pState;

    /// <summary>
    /// Default value to use for LastValidValue when no valid data has been seen yet.
    /// Defaults to double.NaN to avoid silently introducing zeros.
    /// </summary>
    public double DefaultLastValidValue { get; set; } = double.NaN;

    private const int ResyncInterval = 10000;

    private static readonly Vector512<long> V512Idx1 = Vector512.Create(0L, 0, 1, 2, 3, 4, 5, 6);
    private static readonly Vector512<long> V512Idx2 = Vector512.Create(0L, 0, 0, 1, 2, 3, 4, 5);
    private static readonly Vector512<long> V512Idx4 = Vector512.Create(0L, 0, 0, 0, 0, 1, 2, 3);
    private static readonly Vector512<double> V512Mask1 = Vector512.Create(0.0, 1, 1, 1, 1, 1, 1, 1);
    private static readonly Vector512<double> V512Mask2 = Vector512.Create(0.0, 0, 1, 1, 1, 1, 1, 1);
    private static readonly Vector512<double> V512Mask4 = Vector512.Create(0.0, 0, 0, 0, 1, 1, 1, 1);

    public Wma(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _divisor = (double)period * (period + 1) * 0.5;
        _buffer = new RingBuffer(period);
        Name = $"Wma({period})";
        WarmupPeriod = period;
    }

    public Wma(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null && _handler != null)
            {
                _source.Pub -= _handler;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    public override bool IsHot => _buffer.IsFull;
    public bool IsNew { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue = input;
            _state.HasSeenValidData = true;
            return input;
        }
        return _state.HasSeenValidData ? _state.LastValidValue : DefaultLastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateState(double val)
    {
        if (_buffer.IsFull)
        {
            double oldSum = _state.Sum;
            double oldest = _buffer.Oldest;
            _state.Sum = Math.FusedMultiplyAdd(-1.0, oldest, _state.Sum + val);
            _state.WSum = Math.FusedMultiplyAdd(-1.0, oldSum, _state.WSum + _period * val);
        }
        else
        {
            int count = _buffer.Count + 1;
            _state.Sum += val;
            _state.WSum = Math.FusedMultiplyAdd(count, val, _state.WSum);
        }

        _buffer.Add(val);

        _state.TickCount++;
        bool isNaN = double.IsNaN(_state.Sum) || double.IsNaN(_state.WSum);
        bool needResync = _buffer.IsFull && _state.TickCount >= ResyncInterval;

        if (needResync || (isNaN && double.IsFinite(val)))
        {
            _state.TickCount = 0;
            double recalcSum = 0;
            double recalcWsum = 0;
            int weight = 1;
            foreach (double item in _buffer)
            {
                recalcSum += item;
                recalcWsum = Math.FusedMultiplyAdd(weight, item, recalcWsum);
                weight++;
            }
            _state.Sum = recalcSum;
            _state.WSum = recalcWsum;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        IsNew = isNew;
        if (isNew)
        {
            double val = GetValidValue(input.Value);
            UpdateState(val);
            _state.LastInput = val;

            _pState = _state;
        }
        else
        {
            // Defensive check: isNew must be true for the first update
            if (_buffer.Count == 0)
            {
                throw new InvalidOperationException(
                    "Cannot call Update with isNew=false when buffer is empty. " +
                    "The first update must have isNew=true to initialize state.");
            }

            _state = _pState;
            double val = GetValidValue(input.Value);

            int weight = _buffer.IsFull ? _period : _buffer.Count;
            _state.Sum = Math.FusedMultiplyAdd(-1.0, _state.LastInput, _state.Sum + val);
            _state.WSum = Math.FusedMultiplyAdd(weight, val - _state.LastInput, _state.WSum);

            _buffer.UpdateNewest(val);
        }

        double currentDivisor = _buffer.IsFull ? _divisor : (double)_buffer.Count * (_buffer.Count + 1) * 0.5;
        Last = new TValue(input.Time, _state.WSum / currentDivisor);
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

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        int len = source.Length;
        int windowSize = Math.Min(len, _period);
        int startIndex = len - windowSize;

        // Seed LastValidValue
        _state.LastValidValue = DefaultLastValidValue;
        _state.HasSeenValidData = false;
        if (startIndex > 0)
        {
            for (int i = startIndex - 1; i >= 0; i--)
            {
                if (double.IsFinite(source[i]))
                {
                    _state.LastValidValue = source[i];
                    _state.HasSeenValidData = true;
                    break;
                }
            }
        }

        // Reset state
        _buffer.Clear();
        _state.Sum = 0;
        _state.WSum = 0;
        _state.TickCount = 0;

        // Process window
        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(source[i]);
            UpdateState(val);
            _state.LastInput = val;
        }

        // Calculate Last
        double currentDivisor = _buffer.IsFull ? _divisor : (double)_buffer.Count * (_buffer.Count + 1) * 0.5;
        Last = new TValue(DateTime.MinValue, _state.WSum / currentDivisor);

        _pState = _state;
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _pState = default;
        Last = default;
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var wma = new Wma(period);
        return wma.Update(source);
    }

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

        const int simdThreshold = 256;
        if (Avx512F.IsSupported && len >= simdThreshold && !source.ContainsNonFinite())
        {
            CalculateAvx512Core(source, output, period);
            return;
        }

        if (Avx2.IsSupported && len >= simdThreshold && !source.ContainsNonFinite())
        {
            CalculateSimdCore(source, output, period);
            return;
        }

        if (AdvSimd.Arm64.IsSupported && len >= simdThreshold && !source.ContainsNonFinite())
        {
            CalculateNeonCore(source, output, period);
            return;
        }

        CalculateScalarCore(source, output, period);
    }

    public static (TSeries Results, Wma Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Wma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        double divisor = (double)period * (period + 1) * 0.5;
        double sum = 0;
        double wsum = 0;
        double lastValid = double.NaN;

        Span<double> buffer = period <= 512 ? stackalloc double[period] : new double[period];
        int bufferIdx = 0;
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
            wsum = Math.FusedMultiplyAdd(i + 1, val, wsum);
            buffer[i] = val;

            double currentDivisor = (double)(i + 1) * (i + 2) * 0.5;
            output[i] = wsum / currentDivisor;
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

            double oldSum = sum;
            double oldest = buffer[bufferIdx];
            sum = Math.FusedMultiplyAdd(-1.0, oldest, sum + val);
            wsum = Math.FusedMultiplyAdd(-1.0, oldSum, wsum + period * val);

            buffer[bufferIdx] = val;
            bufferIdx++;
            if (bufferIdx >= period)
            {
                bufferIdx = 0;
            }

            output[i] = wsum / divisor;

            tickCount++;
            bool isNaN = double.IsNaN(sum) || double.IsNaN(wsum);
            if (tickCount >= ResyncInterval || (isNaN && double.IsFinite(val)))
            {
                tickCount = 0;
                double recalcSum = 0;
                double recalcWsum = 0;

                for (int k = 0; k < period; k++)
                {
                    int idx = bufferIdx + k;
                    if (idx >= period)
                    {
                        idx -= period;
                    }

                    double v = buffer[idx];
                    recalcSum += v;
                    recalcWsum = Math.FusedMultiplyAdd(k + 1, v, recalcWsum);
                }
                sum = recalcSum;
                wsum = recalcWsum;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateAvx512Core(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int vectorWidth = 8;

        ref double srcRef = ref MemoryMarshal.GetReference(source);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        double divisor = (double)period * (period + 1) * 0.5;
        double invDivisor = 1.0 / divisor;

        int warmupEnd = Math.Min(period, len);
        double sum = 0;
        double wsum = 0;
        for (int i = 0; i < warmupEnd; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            sum += val;
            wsum += (i + 1) * val;
            double currentDivisor = (double)(i + 1) * (i + 2) * 0.5;
            Unsafe.Add(ref outRef, i) = wsum / currentDivisor;
        }

        if (len <= period)
        {
            return;
        }

        var vInvDivisor = Vector512.Create(invDivisor);
        var vPeriod = Vector512.Create((double)period);
        int simdEnd = period + (len - period) / vectorWidth * vectorWidth;

        var vSumState = Vector512.Create(sum);
        var vWsumState = Vector512.Create(wsum);

        int idx = period;
        while (idx < simdEnd)
        {
            int nextSync = Math.Min(simdEnd, idx + ResyncInterval);

            for (; idx < nextSync; idx += vectorWidth)
            {
                var vNew = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx));
                var vOld = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx - period));

                var vDeltaS = Avx512F.Subtract(vNew, vOld);

                // Prefix sum of DeltaS
                var vShiftS1 = Avx512F.Multiply(Avx512F.PermuteVar8x64(vDeltaS, V512Idx1), V512Mask1);
                var vPs1 = Avx512F.Add(vDeltaS, vShiftS1);

                var vShiftS2 = Avx512F.Multiply(Avx512F.PermuteVar8x64(vPs1, V512Idx2), V512Mask2);
                var vPs2 = Avx512F.Add(vPs1, vShiftS2);

                var vShiftS4 = Avx512F.Multiply(Avx512F.PermuteVar8x64(vPs2, V512Idx4), V512Mask4);
                var vPs4 = Avx512F.Add(vPs2, vShiftS4);

                var vSums = Avx512F.Add(vSumState, vPs4);

                // Calculate Wsum update
                var vSumsShifted = Avx512F.Subtract(vSums, vDeltaS);
                var vU = Avx512F.FusedMultiplySubtract(vPeriod, vNew, vSumsShifted);

                // Prefix sum of vU
                var vShiftW1 = Avx512F.Multiply(Avx512F.PermuteVar8x64(vU, V512Idx1), V512Mask1);
                var vPw1 = Avx512F.Add(vU, vShiftW1);

                var vShiftW2 = Avx512F.Multiply(Avx512F.PermuteVar8x64(vPw1, V512Idx2), V512Mask2);
                var vPw2 = Avx512F.Add(vPw1, vShiftW2);

                var vShiftW4 = Avx512F.Multiply(Avx512F.PermuteVar8x64(vPw2, V512Idx4), V512Mask4);
                var vPw4 = Avx512F.Add(vPw2, vShiftW4);

                var vWsums = Avx512F.Add(vWsumState, vPw4);

                var vResult = Avx512F.Multiply(vWsums, vInvDivisor);
                vResult.StoreUnsafe(ref Unsafe.Add(ref outRef, idx));

                // Update state for next iteration
                vSumState = Vector512.Create(vSums.GetElement(7));
                vWsumState = Vector512.Create(vWsums.GetElement(7));
            }

            if (idx < len)
            {
                int lastIdx = idx - 1;
                double recalcSum = 0;
                double recalcWsum = 0;
                for (int k = 0; k < period; k++)
                {
                    double val = Unsafe.Add(ref srcRef, lastIdx - k);
                    recalcSum += val;
                    recalcWsum += (period - k) * val;
                }
                sum = recalcSum;
                wsum = recalcWsum;

                vSumState = Vector512.Create(sum);
                vWsumState = Vector512.Create(wsum);
            }
        }

        sum = vSumState.GetElement(0);
        wsum = vWsumState.GetElement(0);

        for (; idx < len; idx++)
        {
            double val = Unsafe.Add(ref srcRef, idx);
            double oldSum = sum;
            double oldest = Unsafe.Add(ref srcRef, idx - period);
            sum = sum - oldest + val;
            wsum = wsum - oldSum + period * val;
            Unsafe.Add(ref outRef, idx) = wsum * invDivisor;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateSimdCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int vectorWidth = 4;

        ref double srcRef = ref MemoryMarshal.GetReference(source);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        double divisor = (double)period * (period + 1) * 0.5;
        double invDivisor = 1.0 / divisor;

        int warmupEnd = Math.Min(period, len);
        double sum = 0;
        double wsum = 0;
        for (int i = 0; i < warmupEnd; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            sum += val;
            wsum += (i + 1) * val;
            double currentDivisor = (double)(i + 1) * (i + 2) * 0.5;
            Unsafe.Add(ref outRef, i) = wsum / currentDivisor;
        }

        if (len <= period)
        {
            return;
        }

        var vInvDivisor = Vector256.Create(invDivisor);
        var vPeriod = Vector256.Create((double)period);
        var vZero = Vector256<double>.Zero;
        int simdEnd = period + ((len - period) / vectorWidth) * vectorWidth;

        var vSumState = Vector256.Create(sum);
        var vWsumState = Vector256.Create(wsum);

        int idx = period;
        while (idx < simdEnd)
        {
            int nextSync = Math.Min(simdEnd, idx + ResyncInterval);

            int unrolledSync = nextSync - (2 * vectorWidth);
            for (; idx <= unrolledSync; idx += 2 * vectorWidth)
            {
                var vNew1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx));
                var vOld1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx - period));
                var vNew2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx + vectorWidth));
                var vOld2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx + vectorWidth - period));

                var vDeltaS1 = Avx.Subtract(vNew1, vOld1);
                var vDeltaS2 = Avx.Subtract(vNew2, vOld2);

                var vShiftS11 = Avx2.Permute4x64(vDeltaS1.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftS11 = Avx.Blend(vZero, vShiftS11, 0b_1110);
                var vPsDeltaS1 = Avx.Add(vDeltaS1, vShiftS11);
                var vShiftS21 = Avx2.Permute4x64(vPsDeltaS1.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftS21 = Avx.Blend(vZero, vShiftS21, 0b_1100);
                vPsDeltaS1 = Avx.Add(vPsDeltaS1, vShiftS21);

                var vShiftS12 = Avx2.Permute4x64(vDeltaS2.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftS12 = Avx.Blend(vZero, vShiftS12, 0b_1110);
                var vPsDeltaS2 = Avx.Add(vDeltaS2, vShiftS12);
                var vShiftS22 = Avx2.Permute4x64(vPsDeltaS2.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftS22 = Avx.Blend(vZero, vShiftS22, 0b_1100);
                vPsDeltaS2 = Avx.Add(vPsDeltaS2, vShiftS22);

                var vSums1 = Avx.Add(vSumState, vPsDeltaS1);
                var vLastS1 = Avx2.Permute4x64(vSums1.AsUInt64(), 0b_11_11_11_11).AsDouble(); // skipcq: CS-R1131
                var vSums2 = Avx.Add(vLastS1, vPsDeltaS2);

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
                    var vTerm1 = Avx.Multiply(vPeriod, vNew1);
                    var vTerm2 = Avx.Multiply(vPeriod, vNew2);
                    vU1 = Avx.Subtract(vTerm1, vSumsShifted1);
                    vU2 = Avx.Subtract(vTerm2, vSumsShifted2);
                }

                var vShiftW11 = Avx2.Permute4x64(vU1.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftW11 = Avx.Blend(vZero, vShiftW11, 0b_1110);
                var vPw11 = Avx.Add(vU1, vShiftW11);
                var vShiftW21 = Avx2.Permute4x64(vPw11.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftW21 = Avx.Blend(vZero, vShiftW21, 0b_1100);
                var vPw21 = Avx.Add(vPw11, vShiftW21);

                var vShiftW12 = Avx2.Permute4x64(vU2.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftW12 = Avx.Blend(vZero, vShiftW12, 0b_1110);
                var vPw12 = Avx.Add(vU2, vShiftW12);
                var vShiftW22 = Avx2.Permute4x64(vPw12.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftW22 = Avx.Blend(vZero, vShiftW22, 0b_1100);
                var vPw22 = Avx.Add(vPw12, vShiftW22);

                var vWsums1 = Avx.Add(vWsumState, vPw21);
                var vLastW1 = Avx2.Permute4x64(vWsums1.AsUInt64(), 0b_11_11_11_11).AsDouble(); // skipcq: CS-R1131
                var vWsums2 = Avx.Add(vLastW1, vPw22);

                Vector256<double> vResult1, vResult2;
                if (Fma.IsSupported)
                {
                    vResult1 = Fma.MultiplyAdd(vWsums1, vInvDivisor, vZero);
                    vResult2 = Fma.MultiplyAdd(vWsums2, vInvDivisor, vZero);
                }
                else
                {
                    vResult1 = Avx.Multiply(vWsums1, vInvDivisor);
                    vResult2 = Avx.Multiply(vWsums2, vInvDivisor);
                }
                vResult1.StoreUnsafe(ref Unsafe.Add(ref outRef, idx));
                vResult2.StoreUnsafe(ref Unsafe.Add(ref outRef, idx + vectorWidth));

                vSumState = Avx2.Permute4x64(vSums2.AsUInt64(), 0b_11_11_11_11).AsDouble(); // skipcq: CS-R1131
                vWsumState = Avx2.Permute4x64(vWsums2.AsUInt64(), 0b_11_11_11_11).AsDouble(); // skipcq: CS-R1131
            }

            for (; idx < nextSync; idx += vectorWidth)
            {
                var vNew = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx));
                var vOld = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx - period));

                var vDeltaS = Avx.Subtract(vNew, vOld);

                var vShiftS1 = Avx2.Permute4x64(vDeltaS.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftS1 = Avx.Blend(vZero, vShiftS1, 0b_1110);
                var vPs1 = Avx.Add(vDeltaS, vShiftS1);

                var vShiftS2 = Avx2.Permute4x64(vPs1.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftS2 = Avx.Blend(vZero, vShiftS2, 0b_1100);
                var vPs2 = Avx.Add(vPs1, vShiftS2);

                var vSums = Avx.Add(vSumState, vPs2);

                var vSumsShifted = Avx.Subtract(vSums, vDeltaS);
                Vector256<double> vU;
                if (Fma.IsSupported)
                {
                    vU = Fma.MultiplySubtract(vPeriod, vNew, vSumsShifted);
                }
                else
                {
                    var vTerm1 = Avx.Multiply(vPeriod, vNew);
                    vU = Avx.Subtract(vTerm1, vSumsShifted);
                }

                var vShiftW1 = Avx2.Permute4x64(vU.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftW1 = Avx.Blend(vZero, vShiftW1, 0b_1110);
                var vPw1 = Avx.Add(vU, vShiftW1);

                var vShiftW2 = Avx2.Permute4x64(vPw1.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftW2 = Avx.Blend(vZero, vShiftW2, 0b_1100);
                var vPw2 = Avx.Add(vPw1, vShiftW2);

                var vWsums = Avx.Add(vWsumState, vPw2);

                Vector256<double> vResult = Fma.IsSupported
                    ? Fma.MultiplyAdd(vWsums, vInvDivisor, vZero)
                    : Avx.Multiply(vWsums, vInvDivisor);
                vResult.StoreUnsafe(ref Unsafe.Add(ref outRef, idx));

                vSumState = Avx2.Permute4x64(vSums.AsUInt64(), 0b_11_11_11_11).AsDouble(); // skipcq: CS-R1131
                vWsumState = Avx2.Permute4x64(vWsums.AsUInt64(), 0b_11_11_11_11).AsDouble(); // skipcq: CS-R1131
            }

            if (idx < len)
            {
                int lastIdx = idx - 1;
                double recalcSum = 0;
                double recalcWsum = 0;
                for (int k = 0; k < period; k++)
                {
                    double val = Unsafe.Add(ref srcRef, lastIdx - k);
                    recalcSum += val;
                    recalcWsum += (period - k) * val;
                }
                sum = recalcSum;
                wsum = recalcWsum;

                vSumState = Vector256.Create(sum);
                vWsumState = Vector256.Create(wsum);
            }
        }

        sum = vSumState.GetElement(0);
        wsum = vWsumState.GetElement(0);

        for (; idx < len; idx++)
        {
            double val = Unsafe.Add(ref srcRef, idx);
            double oldSum = sum;
            double oldest = Unsafe.Add(ref srcRef, idx - period);
            sum = Math.FusedMultiplyAdd(-1.0, oldest, sum + val);
            wsum = Math.FusedMultiplyAdd(-1.0, oldSum, wsum + period * val);
            Unsafe.Add(ref outRef, idx) = wsum * invDivisor;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateNeonCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int vectorWidth = 2;

        ref double srcRef = ref MemoryMarshal.GetReference(source);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        double divisor = (double)period * (period + 1) * 0.5;
        double invDivisor = 1.0 / divisor;

        int warmupEnd = Math.Min(period, len);
        double sum = 0;
        double wsum = 0;
        for (int i = 0; i < warmupEnd; i++)
        {
            double val = Unsafe.Add(ref srcRef, i);
            sum += val;
            wsum += (i + 1) * val;
            double currentDivisor = (double)(i + 1) * (i + 2) * 0.5;
            Unsafe.Add(ref outRef, i) = wsum / currentDivisor;
        }

        if (len <= period)
        {
            return;
        }

        var vInvDivisor = Vector128.Create(invDivisor);
        int simdEnd = period + ((len - period) / vectorWidth) * vectorWidth;

        double sumState = sum;
        double wsumState = wsum;

        int idx = period;
        while (idx < simdEnd)
        {
            int nextSync = Math.Min(simdEnd, idx + ResyncInterval);

            // Unrolled loop: process 4 elements (2 vectors) at a time
            int unrolledSync = nextSync - (2 * vectorWidth);
            for (; idx <= unrolledSync; idx += 2 * vectorWidth)
            {
                var vNew1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx));
                var vOld1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx - period));
                var vNew2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx + vectorWidth));
                var vOld2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx + vectorWidth - period));

                var vDeltaS1 = AdvSimd.Arm64.Subtract(vNew1, vOld1);
                var vDeltaS2 = AdvSimd.Arm64.Subtract(vNew2, vOld2);

                // Prefix sum for first vector: [d0, d0+d1]
                double d10 = vDeltaS1.GetElement(0);
                double d11 = vDeltaS1.GetElement(1);
                double ps10 = sumState + d10;
                double ps11 = ps10 + d11;

                // Prefix sum for second vector
                double d20 = vDeltaS2.GetElement(0);
                double d21 = vDeltaS2.GetElement(1);
                double ps20 = ps11 + d20;
                double ps21 = ps20 + d21;

                // Calculate Wsum update: W_new = W_old - S_prev + n*new
                // For element i: u_i = period * new_i - S_(i-1)
                double u10 = Math.FusedMultiplyAdd(period, vNew1.GetElement(0), -sumState);
                double u11 = Math.FusedMultiplyAdd(period, vNew1.GetElement(1), -ps10);
                double u20 = Math.FusedMultiplyAdd(period, vNew2.GetElement(0), -ps11);
                double u21 = Math.FusedMultiplyAdd(period, vNew2.GetElement(1), -ps20);

                // Prefix sum of U values
                double pw10 = wsumState + u10;
                double pw11 = pw10 + u11;
                double pw20 = pw11 + u20;
                double pw21 = pw20 + u21;

                var vWsums1 = Vector128.Create(pw10, pw11);
                var vWsums2 = Vector128.Create(pw20, pw21);

                var vResult1 = AdvSimd.Arm64.Multiply(vWsums1, vInvDivisor);
                var vResult2 = AdvSimd.Arm64.Multiply(vWsums2, vInvDivisor);

                vResult1.StoreUnsafe(ref Unsafe.Add(ref outRef, idx));
                vResult2.StoreUnsafe(ref Unsafe.Add(ref outRef, idx + vectorWidth));

                sumState = ps21;
                wsumState = pw21;
            }

            // Process remaining pairs
            for (; idx < nextSync; idx += vectorWidth)
            {
                var vNew = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx));
                var vOld = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx - period));

                var vDeltaS = AdvSimd.Arm64.Subtract(vNew, vOld);

                double d0 = vDeltaS.GetElement(0);
                double d1 = vDeltaS.GetElement(1);
                double ps0 = sumState + d0;
                double ps1 = ps0 + d1;

                double u0 = Math.FusedMultiplyAdd(period, vNew.GetElement(0), -sumState);
                double u1 = Math.FusedMultiplyAdd(period, vNew.GetElement(1), -ps0);

                double pw0 = wsumState + u0;
                double pw1 = pw0 + u1;

                var vWsums = Vector128.Create(pw0, pw1);
                var vResult = AdvSimd.Arm64.Multiply(vWsums, vInvDivisor);

                vResult.StoreUnsafe(ref Unsafe.Add(ref outRef, idx));

                sumState = ps1;
                wsumState = pw1;
            }

            // Resync to prevent floating-point drift
            if (idx < len)
            {
                int lastIdx = idx - 1;
                double recalcSum = 0;
                double recalcWsum = 0;
                for (int k = 0; k < period; k++)
                {
                    double val = Unsafe.Add(ref srcRef, lastIdx - k);
                    recalcSum += val;
                    recalcWsum = Math.FusedMultiplyAdd(period - k, val, recalcWsum);
                }
                sumState = recalcSum;
                wsumState = recalcWsum;
            }
        }

        sum = sumState;
        wsum = wsumState;

        // Scalar tail
        for (; idx < len; idx++)
        {
            double val = Unsafe.Add(ref srcRef, idx);
            double oldSum = sum;
            double oldest = Unsafe.Add(ref srcRef, idx - period);
            sum = Math.FusedMultiplyAdd(-1.0, oldest, sum + val);
            wsum = Math.FusedMultiplyAdd(-1.0, oldSum, wsum + period * val);
            Unsafe.Add(ref outRef, idx) = wsum * invDivisor;
        }
    }
}