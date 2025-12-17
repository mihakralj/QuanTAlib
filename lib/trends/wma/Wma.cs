using System;
using System.Collections.Generic;
using System.Numerics;
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
/// WMA applies linear weighting to data points, giving more weight to recent values.
/// Uses dual running sums for O(1) complexity per update.
///
/// Calculation:
/// WMA = (n*P_n + (n-1)*P_(n-1) + ... + 1*P_1) / (n*(n+1)/2)
///
/// O(1) update:
/// S_new = S - oldest + newest
/// W_new = W - S_old + n*newest
///
/// IsHot:
/// Becomes true when the buffer is full (period samples processed).
/// </remarks>
[SkipLocalsInit]
public sealed class Wma : AbstractBase
{
    private readonly int _period;
    private readonly double _divisor;
    private readonly RingBuffer _buffer;

    private record struct State(double Sum, double WSum, double LastInput, double LastValidValue, int TickCount);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 10000;

    private static readonly Vector512<long> V512_Idx_1 = Vector512.Create(0L, 0, 1, 2, 3, 4, 5, 6);
    private static readonly Vector512<long> V512_Idx_2 = Vector512.Create(0L, 0, 0, 0, 1, 2, 3, 4);
    private static readonly Vector512<long> V512_Idx_4 = Vector512.Create(0L, 0, 0, 0, 0, 0, 1, 2);
    private static readonly Vector512<double> V512_Mask_1 = Vector512.Create(0.0, 1, 1, 1, 1, 1, 1, 1);
    private static readonly Vector512<double> V512_Mask_2 = Vector512.Create(0.0, 0, 1, 1, 1, 1, 1, 1);
    private static readonly Vector512<double> V512_Mask_4 = Vector512.Create(0.0, 0, 0, 0, 1, 1, 1, 1);

    public Wma(int period)
    {
        if (period <= 0) throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _divisor = (double)period * (period + 1) * 0.5;
        _buffer = new RingBuffer(period);
        Name = $"Wma({period})";
        WarmupPeriod = period;
    }

    public Wma(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += (item) => Update(item);
    }

    public override bool IsHot => _buffer.IsFull;

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
            double oldSum = _state.Sum;
            double oldest = _buffer.Oldest;
            _state.Sum = _state.Sum - oldest + val;
            _state.WSum = _state.WSum - oldSum + (_period * val);
        }
        else
        {
            int count = _buffer.Count + 1;
            _state.Sum += val;
            _state.WSum += count * val;
        }

        _buffer.Add(val);

        _state.TickCount++;
        if (_buffer.IsFull && _state.TickCount >= ResyncInterval)
        {
            _state.TickCount = 0;
            double recalcSum = 0;
            double recalcWsum = 0;
            int weight = 1;
            foreach (double item in _buffer)
            {
                recalcSum += item;
                recalcWsum += weight * item;
                weight++;
            }
            _state.Sum = recalcSum;
            _state.WSum = recalcWsum;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            double val = GetValidValue(input.Value);
            UpdateState(val);
            _state.LastInput = val;

            _p_state = _state;
        }
        else
        {
            _state = _p_state;
            double val = GetValidValue(input.Value);

            int weight = _buffer.IsFull ? _period : _buffer.Count;
            _state.Sum = _state.Sum - _state.LastInput + val;
            _state.WSum += weight * (val - _state.LastInput);

            _buffer.UpdateNewest(val);
        }

        double currentDivisor = _buffer.IsFull ? _divisor : (double)_buffer.Count * (_buffer.Count + 1) * 0.5;
        Last = new TValue(input.Time, _state.WSum / currentDivisor);
        PubEvent(Last);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

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

    public override void Prime(ReadOnlySpan<double> source)
    {
        if (source.Length == 0) return;

        int len = source.Length;
        int windowSize = Math.Min(len, _period);
        int startIndex = len - windowSize;

        // Seed LastValidValue
        _state.LastValidValue = 0;
        if (startIndex > 0)
        {
            for (int i = startIndex - 1; i >= 0; i--)
            {
                if (double.IsFinite(source[i]))
                {
                    _state.LastValidValue = source[i];
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

        _p_state = _state;
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
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
            throw new ArgumentException("Source and output must have the same length");
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = source.Length;
        if (len == 0) return;

        const int SimdThreshold = 256;
        if (Avx512F.IsSupported && len >= SimdThreshold && !source.ContainsNonFinite())
        {
            CalculateAvx512Core(source, output, period);
            return;
        }

        if (Avx2.IsSupported && len >= SimdThreshold && !source.ContainsNonFinite())
        {
            CalculateSimdCore(source, output, period);
            return;
        }

        if (AdvSimd.Arm64.IsSupported && len >= SimdThreshold && !source.ContainsNonFinite())
        {
            CalculateNeonCore(source, output, period);
            return;
        }

        CalculateScalarCore(source, output, period);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        double divisor = (double)period * (period + 1) * 0.5;
        double sum = 0;
        double wsum = 0;
        double lastValid = 0;

        Span<double> buffer = period <= 512 ? stackalloc double[period] : new double[period];
        int bufferIdx = 0;
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
            wsum += (i + 1) * val;
            buffer[i] = val;

            double currentDivisor = (double)(i + 1) * (i + 2) * 0.5;
            output[i] = wsum / currentDivisor;
        }

        int tickCount = 0;
        for (; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            double oldSum = sum;
            double oldest = buffer[bufferIdx];
            sum = sum - oldest + val;
            wsum = wsum - oldSum + (period * val);

            buffer[bufferIdx] = val;
            bufferIdx++;
            if (bufferIdx >= period)
                bufferIdx = 0;

            output[i] = wsum / divisor;

            tickCount++;
            if (tickCount >= ResyncInterval)
            {
                tickCount = 0;
                double recalcSum = 0;
                double recalcWsum = 0;

                for (int k = 0; k < period; k++)
                {
                    int idx = bufferIdx + k;
                    if (idx >= period) idx -= period;

                    double v = buffer[idx];
                    recalcSum += v;
                    recalcWsum += (k + 1) * v;
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
        const int VectorWidth = 8;

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
            return;

        var vInvDivisor = Vector512.Create(invDivisor);
        var vPeriod = Vector512.Create((double)period);
        int simdEnd = period + ((len - period) / VectorWidth) * VectorWidth;

        var vSumState = Vector512.Create(sum);
        var vWsumState = Vector512.Create(wsum);

        int idx = period;
        while (idx < simdEnd)
        {
            int nextSync = Math.Min(simdEnd, idx + ResyncInterval);

            for (; idx < nextSync; idx += VectorWidth)
            {
                var vNew = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx));
                var vOld = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx - period));

                var vDeltaS = Avx512F.Subtract(vNew, vOld);

                // Prefix sum of DeltaS
                var vShiftS1 = Avx512F.Multiply(Avx512F.PermuteVar8x64(vDeltaS, V512_Idx_1), V512_Mask_1);
                var vPS1 = Avx512F.Add(vDeltaS, vShiftS1);

                var vShiftS2 = Avx512F.Multiply(Avx512F.PermuteVar8x64(vPS1, V512_Idx_2), V512_Mask_2);
                var vPS2 = Avx512F.Add(vPS1, vShiftS2);

                var vShiftS4 = Avx512F.Multiply(Avx512F.PermuteVar8x64(vPS2, V512_Idx_4), V512_Mask_4);
                var vPS4 = Avx512F.Add(vPS2, vShiftS4);

                var vSums = Avx512F.Add(vSumState, vPS4);

                // Calculate Wsum update
                var vSumsShifted = Avx512F.Subtract(vSums, vDeltaS);
                var vU = Avx512F.FusedMultiplySubtract(vPeriod, vNew, vSumsShifted);

                // Prefix sum of vU
                var vShiftW1 = Avx512F.Multiply(Avx512F.PermuteVar8x64(vU, V512_Idx_1), V512_Mask_1);
                var vPW1 = Avx512F.Add(vU, vShiftW1);

                var vShiftW2 = Avx512F.Multiply(Avx512F.PermuteVar8x64(vPW1, V512_Idx_2), V512_Mask_2);
                var vPW2 = Avx512F.Add(vPW1, vShiftW2);

                var vShiftW4 = Avx512F.Multiply(Avx512F.PermuteVar8x64(vPW2, V512_Idx_4), V512_Mask_4);
                var vPW4 = Avx512F.Add(vPW2, vShiftW4);

                var vWsums = Avx512F.Add(vWsumState, vPW4);

                var vResult = Avx512F.Multiply(vWsums, vInvDivisor);
                Vector512.StoreUnsafe(vResult, ref Unsafe.Add(ref outRef, idx));

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
            wsum = wsum - oldSum + (period * val);
            Unsafe.Add(ref outRef, idx) = wsum * invDivisor;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateSimdCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int VectorWidth = 4;

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
            return;

        var vInvDivisor = Vector256.Create(invDivisor);
        var vPeriod = Vector256.Create((double)period);
        var vZero = Vector256<double>.Zero;
        int simdEnd = period + ((len - period) / VectorWidth) * VectorWidth;

        var vSumState = Vector256.Create(sum);
        var vWsumState = Vector256.Create(wsum);

        int idx = period;
        while (idx < simdEnd)
        {
            int nextSync = Math.Min(simdEnd, idx + ResyncInterval);

            int unrolledSync = nextSync - (2 * VectorWidth);
            for (; idx <= unrolledSync; idx += 2 * VectorWidth)
            {
                var vNew1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx));
                var vOld1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx - period));
                var vNew2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx + VectorWidth));
                var vOld2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx + VectorWidth - period));

                var vDeltaS1 = Avx.Subtract(vNew1, vOld1);
                var vDeltaS2 = Avx.Subtract(vNew2, vOld2);

                var vShiftS1_1 = Avx2.Permute4x64(vDeltaS1.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftS1_1 = Avx.Blend(vZero, vShiftS1_1, 0b_1110);
                var vPS_DeltaS1 = Avx.Add(vDeltaS1, vShiftS1_1);
                var vShiftS2_1 = Avx2.Permute4x64(vPS_DeltaS1.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftS2_1 = Avx.Blend(vZero, vShiftS2_1, 0b_1100);
                vPS_DeltaS1 = Avx.Add(vPS_DeltaS1, vShiftS2_1);

                var vShiftS1_2 = Avx2.Permute4x64(vDeltaS2.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftS1_2 = Avx.Blend(vZero, vShiftS1_2, 0b_1110);
                var vPS_DeltaS2 = Avx.Add(vDeltaS2, vShiftS1_2);
                var vShiftS2_2 = Avx2.Permute4x64(vPS_DeltaS2.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftS2_2 = Avx.Blend(vZero, vShiftS2_2, 0b_1100);
                vPS_DeltaS2 = Avx.Add(vPS_DeltaS2, vShiftS2_2);

                var vSums1 = Avx.Add(vSumState, vPS_DeltaS1);
                var vLastS1 = Avx2.Permute4x64(vSums1.AsUInt64(), 0b_11_11_11_11).AsDouble(); // skipcq: CS-R1131
                var vSums2 = Avx.Add(vLastS1, vPS_DeltaS2);

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

                var vShiftW1_1 = Avx2.Permute4x64(vU1.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftW1_1 = Avx.Blend(vZero, vShiftW1_1, 0b_1110);
                var vPW1_1 = Avx.Add(vU1, vShiftW1_1);
                var vShiftW2_1 = Avx2.Permute4x64(vPW1_1.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftW2_1 = Avx.Blend(vZero, vShiftW2_1, 0b_1100);
                var vPW2_1 = Avx.Add(vPW1_1, vShiftW2_1);

                var vShiftW1_2 = Avx2.Permute4x64(vU2.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftW1_2 = Avx.Blend(vZero, vShiftW1_2, 0b_1110);
                var vPW1_2 = Avx.Add(vU2, vShiftW1_2);
                var vShiftW2_2 = Avx2.Permute4x64(vPW1_2.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftW2_2 = Avx.Blend(vZero, vShiftW2_2, 0b_1100);
                var vPW2_2 = Avx.Add(vPW1_2, vShiftW2_2);

                var vWsums1 = Avx.Add(vWsumState, vPW2_1);
                var vLastW1 = Avx2.Permute4x64(vWsums1.AsUInt64(), 0b_11_11_11_11).AsDouble(); // skipcq: CS-R1131
                var vWsums2 = Avx.Add(vLastW1, vPW2_2);

                Vector256.StoreUnsafe(Avx.Multiply(vWsums1, vInvDivisor), ref Unsafe.Add(ref outRef, idx));
                Vector256.StoreUnsafe(Avx.Multiply(vWsums2, vInvDivisor), ref Unsafe.Add(ref outRef, idx + VectorWidth));

                vSumState = Avx2.Permute4x64(vSums2.AsUInt64(), 0b_11_11_11_11).AsDouble(); // skipcq: CS-R1131
                vWsumState = Avx2.Permute4x64(vWsums2.AsUInt64(), 0b_11_11_11_11).AsDouble(); // skipcq: CS-R1131
            }

            for (; idx < nextSync; idx += VectorWidth)
            {
                var vNew = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx));
                var vOld = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx - period));

                var vDeltaS = Avx.Subtract(vNew, vOld);

                var vShiftS1 = Avx2.Permute4x64(vDeltaS.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftS1 = Avx.Blend(vZero, vShiftS1, 0b_1110);
                var vPS1 = Avx.Add(vDeltaS, vShiftS1);

                var vShiftS2 = Avx2.Permute4x64(vPS1.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftS2 = Avx.Blend(vZero, vShiftS2, 0b_1100);
                var vPS2 = Avx.Add(vPS1, vShiftS2);

                var vSums = Avx.Add(vSumState, vPS2);

                var vSumsShifted = Avx.Subtract(vSums, vDeltaS);
                var vTerm1 = Avx.Multiply(vPeriod, vNew);
                var vU = Avx.Subtract(vTerm1, vSumsShifted);

                var vShiftW1 = Avx2.Permute4x64(vU.AsUInt64(), 0b_10_01_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftW1 = Avx.Blend(vZero, vShiftW1, 0b_1110);
                var vPW1 = Avx.Add(vU, vShiftW1);

                var vShiftW2 = Avx2.Permute4x64(vPW1.AsUInt64(), 0b_01_00_00_00).AsDouble(); // skipcq: CS-R1131
                vShiftW2 = Avx.Blend(vZero, vShiftW2, 0b_1100);
                var vPW2 = Avx.Add(vPW1, vShiftW2);

                var vWsums = Avx.Add(vWsumState, vPW2);

                var vResult = Avx.Multiply(vWsums, vInvDivisor);
                Vector256.StoreUnsafe(vResult, ref Unsafe.Add(ref outRef, idx));

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
            sum = sum - oldest + val;
            wsum = wsum - oldSum + (period * val);
            Unsafe.Add(ref outRef, idx) = wsum * invDivisor;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateNeonCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int VectorWidth = 2;

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
            return;

        var vInvDivisor = Vector128.Create(invDivisor);
        int simdEnd = period + ((len - period) / VectorWidth) * VectorWidth;

        double sumState = sum;
        double wsumState = wsum;

        int idx = period;
        while (idx < simdEnd)
        {
            int nextSync = Math.Min(simdEnd, idx + ResyncInterval);

            // Unrolled loop: process 4 elements (2 vectors) at a time
            int unrolledSync = nextSync - (2 * VectorWidth);
            for (; idx <= unrolledSync; idx += 2 * VectorWidth)
            {
                var vNew1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx));
                var vOld1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx - period));
                var vNew2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx + VectorWidth));
                var vOld2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx + VectorWidth - period));

                var vDeltaS1 = AdvSimd.Arm64.Subtract(vNew1, vOld1);
                var vDeltaS2 = AdvSimd.Arm64.Subtract(vNew2, vOld2);

                // Prefix sum for first vector: [d0, d0+d1]
                double d1_0 = vDeltaS1.GetElement(0);
                double d1_1 = vDeltaS1.GetElement(1);
                double ps1_0 = sumState + d1_0;
                double ps1_1 = ps1_0 + d1_1;

                // Prefix sum for second vector
                double d2_0 = vDeltaS2.GetElement(0);
                double d2_1 = vDeltaS2.GetElement(1);
                double ps2_0 = ps1_1 + d2_0;
                double ps2_1 = ps2_0 + d2_1;

                // Calculate Wsum update: W_new = W_old - S_prev + n*new
                // For element i: u_i = period * new_i - S_(i-1)
                double u1_0 = period * vNew1.GetElement(0) - sumState;
                double u1_1 = period * vNew1.GetElement(1) - ps1_0;
                double u2_0 = period * vNew2.GetElement(0) - ps1_1;
                double u2_1 = period * vNew2.GetElement(1) - ps2_0;

                // Prefix sum of U values
                double pw1_0 = wsumState + u1_0;
                double pw1_1 = pw1_0 + u1_1;
                double pw2_0 = pw1_1 + u2_0;
                double pw2_1 = pw2_0 + u2_1;

                var vWsums1 = Vector128.Create(pw1_0, pw1_1);
                var vWsums2 = Vector128.Create(pw2_0, pw2_1);

                var vResult1 = AdvSimd.Arm64.Multiply(vWsums1, vInvDivisor);
                var vResult2 = AdvSimd.Arm64.Multiply(vWsums2, vInvDivisor);

                Vector128.StoreUnsafe(vResult1, ref Unsafe.Add(ref outRef, idx));
                Vector128.StoreUnsafe(vResult2, ref Unsafe.Add(ref outRef, idx + VectorWidth));

                sumState = ps2_1;
                wsumState = pw2_1;
            }

            // Process remaining pairs
            for (; idx < nextSync; idx += VectorWidth)
            {
                var vNew = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx));
                var vOld = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx - period));

                var vDeltaS = AdvSimd.Arm64.Subtract(vNew, vOld);

                double d0 = vDeltaS.GetElement(0);
                double d1 = vDeltaS.GetElement(1);
                double ps0 = sumState + d0;
                double ps1 = ps0 + d1;

                double u0 = period * vNew.GetElement(0) - sumState;
                double u1 = period * vNew.GetElement(1) - ps0;

                double pw0 = wsumState + u0;
                double pw1 = pw0 + u1;

                var vWsums = Vector128.Create(pw0, pw1);
                var vResult = AdvSimd.Arm64.Multiply(vWsums, vInvDivisor);

                Vector128.StoreUnsafe(vResult, ref Unsafe.Add(ref outRef, idx));

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
                    recalcWsum += (period - k) * val;
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
            sum = sum - oldest + val;
            wsum = wsum - oldSum + (period * val);
            Unsafe.Add(ref outRef, idx) = wsum * invDivisor;
        }
    }
}
