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
/// Calculation:
/// WMA = (n*P_n + (n-1)*P_(n-1) + ... + 1*P_1) / (n*(n+1)/2)
///
/// O(1) update:
/// S_new = S - oldest + newest
/// W_new = W - S_old + n*newest
/// </remarks>
[SkipLocalsInit]
public sealed class Wma
{
    private readonly int _period;
    private readonly double _divisor;
    private readonly RingBuffer _buffer;

    private double _sum, _wsum;
    private double _p_sum, _p_wsum, _p_lastInput;
    private double _lastValidValue, _p_lastValidValue;
    private int _tickCount;
    private const int ResyncInterval = 1000;

    public string Name { get; }
    public TValue Value { get; private set; }
    public bool IsHot => _buffer.IsFull;

    public Wma(int period)
    {
        if (period <= 0) throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _divisor = period * (period + 1) * 0.5;
        _buffer = new RingBuffer(period);
        Name = $"Wma({period})";
    }

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
            double oldSum = _sum;
            double oldest = _buffer.Oldest;
            _sum = _sum - oldest + val;
            _wsum = _wsum - oldSum + (_period * val);
        }
        else
        {
            int count = _buffer.Count + 1;
            _sum += val;
            _wsum += count * val;
        }

        _buffer.Add(val);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            double val = GetValidValue(input.Value);
            UpdateState(val);

            _p_sum = _sum;
            _p_wsum = _wsum;
            _p_lastInput = val;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _lastValidValue = _p_lastValidValue;
            double val = GetValidValue(input.Value);

            _sum = _p_sum;
            _wsum = _p_wsum;

            int weight = _buffer.IsFull ? _period : _buffer.Count;
            _sum = _sum - _p_lastInput + val;
            _wsum += weight * (val - _p_lastInput);

            _buffer.UpdateNewest(val);
        }

        double currentDivisor = _buffer.IsFull ? _divisor : _buffer.Count * (_buffer.Count + 1) * 0.5;
        Value = new TValue(input.Time, _wsum / currentDivisor);
        return Value;
    }

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
        _wsum = 0;
        _tickCount = 0;

        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(source.Values[i]);
            UpdateState(val);
        }

        _p_sum = _sum;
        _p_wsum = _wsum;
        _p_lastInput = source.Values[len - 1];
        _p_lastValidValue = _lastValidValue;

        Value = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public static TSeries Calculate(TSeries source, int period)
    {
        var wma = new Wma(period);
        return wma.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = source.Length;
        if (len == 0) return;

        const int SimdThreshold = 256;
        if (Avx2.IsSupported && len >= SimdThreshold && !HasNonFiniteValues(source))
        {
            CalculateSimdCore(source, output, period);
            return;
        }

        CalculateScalarCore(source, output, period);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        double divisor = period * (period + 1) * 0.5;
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

            double currentDivisor = (i + 1) * (i + 2) * 0.5;
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
    private static unsafe void CalculateSimdCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int VectorWidth = 4;

        fixed (double* srcPtr = source)
        fixed (double* outPtr = output)
        {
            double divisor = period * (period + 1) * 0.5;
            double invDivisor = 1.0 / divisor;

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
                    var vNew1 = Avx.LoadVector256(srcPtr + idx);
                    var vOld1 = Avx.LoadVector256(srcPtr + idx - period);
                    var vNew2 = Avx.LoadVector256(srcPtr + idx + VectorWidth);
                    var vOld2 = Avx.LoadVector256(srcPtr + idx + VectorWidth - period);

                    var vDeltaS1 = Avx.Subtract(vNew1, vOld1);
                    var vDeltaS2 = Avx.Subtract(vNew2, vOld2);

                    var vShiftS1_1 = Avx2.Permute4x64(vDeltaS1.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vShiftS1_1 = Avx.Blend(vZero, vShiftS1_1, 0b_1110);
                    var vPS_DeltaS1 = Avx.Add(vDeltaS1, vShiftS1_1);
                    var vShiftS2_1 = Avx2.Permute4x64(vPS_DeltaS1.AsUInt64(), 0b_01_00_00_00).AsDouble();
                    vShiftS2_1 = Avx.Blend(vZero, vShiftS2_1, 0b_1100);
                    vPS_DeltaS1 = Avx.Add(vPS_DeltaS1, vShiftS2_1);

                    var vShiftS1_2 = Avx2.Permute4x64(vDeltaS2.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vShiftS1_2 = Avx.Blend(vZero, vShiftS1_2, 0b_1110);
                    var vPS_DeltaS2 = Avx.Add(vDeltaS2, vShiftS1_2);
                    var vShiftS2_2 = Avx2.Permute4x64(vPS_DeltaS2.AsUInt64(), 0b_01_00_00_00).AsDouble();
                    vShiftS2_2 = Avx.Blend(vZero, vShiftS2_2, 0b_1100);
                    vPS_DeltaS2 = Avx.Add(vPS_DeltaS2, vShiftS2_2);

                    var vSums1 = Avx.Add(vSumState, vPS_DeltaS1);
                    var vLastS1 = Avx2.Permute4x64(vSums1.AsUInt64(), 0b_11_11_11_11).AsDouble();
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

                    var vShiftW1_1 = Avx2.Permute4x64(vU1.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vShiftW1_1 = Avx.Blend(vZero, vShiftW1_1, 0b_1110);
                    var vPW1_1 = Avx.Add(vU1, vShiftW1_1);
                    var vShiftW2_1 = Avx2.Permute4x64(vPW1_1.AsUInt64(), 0b_01_00_00_00).AsDouble();
                    vShiftW2_1 = Avx.Blend(vZero, vShiftW2_1, 0b_1100);
                    var vPW2_1 = Avx.Add(vPW1_1, vShiftW2_1);

                    var vShiftW1_2 = Avx2.Permute4x64(vU2.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vShiftW1_2 = Avx.Blend(vZero, vShiftW1_2, 0b_1110);
                    var vPW1_2 = Avx.Add(vU2, vShiftW1_2);
                    var vShiftW2_2 = Avx2.Permute4x64(vPW1_2.AsUInt64(), 0b_01_00_00_00).AsDouble();
                    vShiftW2_2 = Avx.Blend(vZero, vShiftW2_2, 0b_1100);
                    var vPW2_2 = Avx.Add(vPW1_2, vShiftW2_2);

                    var vWsums1 = Avx.Add(vWsumState, vPW2_1);
                    var vLastW1 = Avx2.Permute4x64(vWsums1.AsUInt64(), 0b_11_11_11_11).AsDouble();
                    var vWsums2 = Avx.Add(vLastW1, vPW2_2);

                    Avx.Store(outPtr + idx, Avx.Multiply(vWsums1, vInvDivisor));
                    Avx.Store(outPtr + idx + VectorWidth, Avx.Multiply(vWsums2, vInvDivisor));

                    vSumState = Avx2.Permute4x64(vSums2.AsUInt64(), 0b_11_11_11_11).AsDouble();
                    vWsumState = Avx2.Permute4x64(vWsums2.AsUInt64(), 0b_11_11_11_11).AsDouble();
                }

                for (; idx < nextSync; idx += VectorWidth)
                {
                    var vNew = Avx.LoadVector256(srcPtr + idx);
                    var vOld = Avx.LoadVector256(srcPtr + idx - period);

                    var vDeltaS = Avx.Subtract(vNew, vOld);

                    var vShiftS1 = Avx2.Permute4x64(vDeltaS.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vShiftS1 = Avx.Blend(vZero, vShiftS1, 0b_1110);
                    var vPS1 = Avx.Add(vDeltaS, vShiftS1);

                    var vShiftS2 = Avx2.Permute4x64(vPS1.AsUInt64(), 0b_01_00_00_00).AsDouble();
                    vShiftS2 = Avx.Blend(vZero, vShiftS2, 0b_1100);
                    var vPS2 = Avx.Add(vPS1, vShiftS2);

                    var vSums = Avx.Add(vSumState, vPS2);

                    var vSumsShifted = Avx2.Permute4x64(vSums.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vSumsShifted = Avx.Blend(vSumState, vSumsShifted, 0b_1110);

                    var vTerm1 = Avx.Multiply(vPeriod, vNew);
                    var vU = Avx.Subtract(vTerm1, vSumsShifted);

                    var vShiftW1 = Avx2.Permute4x64(vU.AsUInt64(), 0b_10_01_00_00).AsDouble();
                    vShiftW1 = Avx.Blend(vZero, vShiftW1, 0b_1110);
                    var vPW1 = Avx.Add(vU, vShiftW1);

                    var vShiftW2 = Avx2.Permute4x64(vPW1.AsUInt64(), 0b_01_00_00_00).AsDouble();
                    vShiftW2 = Avx.Blend(vZero, vShiftW2, 0b_1100);
                    var vPW2 = Avx.Add(vPW1, vShiftW2);

                    var vWsums = Avx.Add(vWsumState, vPW2);

                    var vResult = Avx.Multiply(vWsums, vInvDivisor);
                    Avx.Store(outPtr + idx, vResult);

                    vSumState = Avx2.Permute4x64(vSums.AsUInt64(), 0b_11_11_11_11).AsDouble();
                    vWsumState = Avx2.Permute4x64(vWsums.AsUInt64(), 0b_11_11_11_11).AsDouble();
                }

                if (idx < len)
                {
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
                    
                    vSumState = Vector256.Create(sum);
                    vWsumState = Vector256.Create(wsum);
                }
            }
            
            sum = vSumState.GetElement(0);
            wsum = vWsumState.GetElement(0);

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

    public void Reset()
    {
        _buffer.Clear();
        _sum = _wsum = _p_sum = _p_wsum = _p_lastInput = _lastValidValue = _p_lastValidValue = 0;
        Value = default;
    }
}
