using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// Convolution Indicator
/// </summary>
/// <remarks>
/// Applies a custom kernel (weights) to the data window.
/// The kernel is applied such that kernel[0] multiplies the oldest data point in the window,
/// and kernel[n-1] multiplies the newest data point.
///
/// Calculation:
/// Result = Sum(kernel[i] * data[i]) for i = 0 to n-1
///
/// Complexity:
/// Update: O(K) where K is kernel length.
/// </remarks>
[SkipLocalsInit]
public sealed class Conv : ITValuePublisher
{
    private readonly int _period;
    private readonly double[] _kernel;
    private readonly RingBuffer _buffer;

    private double _lastValidValue;
    private int _head;

    // State for bar correction
    private double _p_lastValidValue;

    public string Name { get; }
    public TValue Last { get; private set; }
    public bool IsHot => _buffer.IsFull;
    public event Action<TValue>? Pub;

    public Conv(double[] kernel)
    {
        if (kernel == null || kernel.Length == 0)
            throw new ArgumentException("Kernel must not be empty", nameof(kernel));

        _period = kernel.Length;
        _kernel = new double[_period];
        Array.Copy(kernel, _kernel, _period);
        _buffer = new RingBuffer(_period);
        Name = $"Conv({_period})";
        _lastValidValue = double.NaN;
        _p_lastValidValue = double.NaN;
    }

    public Conv(ITValuePublisher source, double[] kernel) : this(kernel)
    {
        source.Pub += (item) => Update(item);
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
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _lastValidValue = _p_lastValidValue;
        }

        double val = GetValidValue(input.Value);

        if (isNew)
        {
            _buffer.Add(val);
            _head = (_head + 1 == _period) ? 0 : _head + 1;
        }
        else
        {
            _buffer.UpdateNewest(val);
        }

        double result = 0;
        if (_buffer.Count > 0)
        {
            int count = _buffer.Count;
            int kernelOffset = _period - count;
            ReadOnlySpan<double> kernelSpan = _kernel.AsSpan().Slice(kernelOffset);
            ReadOnlySpan<double> internalBuf = _buffer.InternalBuffer;

            if (count < _period)
            {
                result = DotProduct(internalBuf.Slice(0, count), kernelSpan);
            }
            else
            {
                // Full: data is split at _head (which points to oldest)
                int part1Len = _period - _head;
                result = DotProduct(internalBuf.Slice(_head, part1Len), kernelSpan.Slice(0, part1Len))
                       + DotProduct(internalBuf.Slice(0, _head), kernelSpan.Slice(part1Len));
            }
        }

        Last = new TValue(input.Time, result);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries();

        int len = source.Count;
        List<long> t = new(len);
        List<double> v = new(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        source.Times.CopyTo(tSpan);
        var sourceValues = source.Values;

        Calculate(sourceValues, vSpan, _kernel);

        // Restore state
        // We need to replay the last few updates to restore _buffer and _lastValidValue
        int windowSize = Math.Min(len, _period);
        int startIndex = len - windowSize;

        // Find last valid value before the window if possible
        if (startIndex > 0)
        {
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
            _lastValidValue = double.NaN;
        }

        _buffer.Clear();

        // Replay
        for (int i = startIndex; i < len; i++)
        {
            double val = GetValidValue(sourceValues[i]);
            _buffer.Add(val);
        }

        // Sync _head with buffer state
        _head = windowSize % _period;

        // Set Last
        Last = new TValue(source.Times[len - 1], vSpan[len - 1]);

        // Save state for isNew=false
        _p_lastValidValue = _lastValidValue;

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProduct(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;
        
        int len = a.Length;

        // Fast path for very small kernels (avoid SIMD overhead)
        if (len <= 3)
        {
            ref double aRef = ref MemoryMarshal.GetReference(a);
            ref double bRef = ref MemoryMarshal.GetReference(b);

            double sum = aRef * bRef;
            if (len > 1) sum += Unsafe.Add(ref aRef, 1) * Unsafe.Add(ref bRef, 1);
            if (len > 2) sum += Unsafe.Add(ref aRef, 2) * Unsafe.Add(ref bRef, 2);
            return sum;
        }

        if (Avx512F.IsSupported)
            return DotProductAvx512(a, b);

        if (Avx2.IsSupported)
            return DotProductAvx2(a, b);

        if (Sse2.IsSupported)
            return DotProductSse2(a, b);

        if (AdvSimd.Arm64.IsSupported)
            return DotProductNeon(a, b);

        double s = 0;
        ref double ar = ref MemoryMarshal.GetReference(a);
        ref double br = ref MemoryMarshal.GetReference(b);

        int i = 0;
        // Unroll scalar loop
        for (; i <= len - 4; i += 4)
        {
            s += Unsafe.Add(ref ar, i) * Unsafe.Add(ref br, i);
            s += Unsafe.Add(ref ar, i + 1) * Unsafe.Add(ref br, i + 1);
            s += Unsafe.Add(ref ar, i + 2) * Unsafe.Add(ref br, i + 2);
            s += Unsafe.Add(ref ar, i + 3) * Unsafe.Add(ref br, i + 3);
        }

        for (; i < len; i++)
        {
            s += Unsafe.Add(ref ar, i) * Unsafe.Add(ref br, i);
        }
        return s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProductAvx512(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        int len = a.Length;
        int i = 0;
        Vector512<double> vSum = Vector512<double>.Zero;
        Vector512<double> vSum2 = Vector512<double>.Zero;
        Vector512<double> vSum3 = Vector512<double>.Zero;
        Vector512<double> vSum4 = Vector512<double>.Zero;

        ref double aRef = ref MemoryMarshal.GetReference(a);
        ref double bRef = ref MemoryMarshal.GetReference(b);

        // Unroll loop: Process 32 doubles (4 vectors) at a time
        if (len >= 32)
        {
            for (; i <= len - 32; i += 32)
            {
                var va1 = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
                var vb1 = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, i));

                var va2 = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 8));
                var vb2 = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 8));

                var va3 = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 16));
                var vb3 = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 16));

                var va4 = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 24));
                var vb4 = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 24));

                vSum = Avx512F.FusedMultiplyAdd(va1, vb1, vSum);
                vSum2 = Avx512F.FusedMultiplyAdd(va2, vb2, vSum2);
                vSum3 = Avx512F.FusedMultiplyAdd(va3, vb3, vSum3);
                vSum4 = Avx512F.FusedMultiplyAdd(va4, vb4, vSum4);
            }
        }

        // Process remaining vectors (8 doubles at a time)
        for (; i <= len - 8; i += 8)
        {
            var va = Vector512.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
            var vb = Vector512.LoadUnsafe(ref Unsafe.Add(ref bRef, i));
            vSum = Avx512F.FusedMultiplyAdd(va, vb, vSum);
        }

        // Combine accumulators
        vSum = Avx512F.Add(vSum, vSum2);
        vSum3 = Avx512F.Add(vSum3, vSum4);
        vSum = Avx512F.Add(vSum, vSum3);
        
        // Horizontal sum - reduce to Vector256, then Vector128
        Vector256<double> v256 = Avx512F.Add(vSum.GetLower(), vSum.GetUpper());
        Vector128<double> lower = v256.GetLower();
        Vector128<double> upper = v256.GetUpper();
        Vector128<double> combined = Sse2.Add(lower, upper);
        double sum = combined.GetElement(0) + combined.GetElement(1);

        // Scalar remainder
        for (; i < len; i++)
        {
            sum += Unsafe.Add(ref aRef, i) * Unsafe.Add(ref bRef, i);
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProductAvx2(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        int len = a.Length;
        int i = 0;
        Vector256<double> vSum = Vector256<double>.Zero;
        Vector256<double> vSum2 = Vector256<double>.Zero;
        Vector256<double> vSum3 = Vector256<double>.Zero;
        Vector256<double> vSum4 = Vector256<double>.Zero;

        ref double aRef = ref MemoryMarshal.GetReference(a);
        ref double bRef = ref MemoryMarshal.GetReference(b);

        // Unroll loop: Process 16 doubles (4 vectors) at a time
        if (len >= 16)
        {
            for (; i <= len - 16; i += 16)
            {
                var va1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
                var vb1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, i));

                var va2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 4));
                var vb2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 4));

                var va3 = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 8));
                var vb3 = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 8));

                var va4 = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 12));
                var vb4 = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 12));

                if (Fma.IsSupported)
                {
                    vSum = Fma.MultiplyAdd(va1, vb1, vSum);
                    vSum2 = Fma.MultiplyAdd(va2, vb2, vSum2);
                    vSum3 = Fma.MultiplyAdd(va3, vb3, vSum3);
                    vSum4 = Fma.MultiplyAdd(va4, vb4, vSum4);
                }
                else
                {
                    vSum = Avx.Add(vSum, Avx.Multiply(va1, vb1));
                    vSum2 = Avx.Add(vSum2, Avx.Multiply(va2, vb2));
                    vSum3 = Avx.Add(vSum3, Avx.Multiply(va3, vb3));
                    vSum4 = Avx.Add(vSum4, Avx.Multiply(va4, vb4));
                }
            }
        }

        // Process remaining vectors (4 doubles at a time)
        for (; i <= len - 4; i += 4)
        {
            var va = Vector256.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
            var vb = Vector256.LoadUnsafe(ref Unsafe.Add(ref bRef, i));

            vSum = Fma.IsSupported
                ? Fma.MultiplyAdd(va, vb, vSum)
                : Avx.Add(vSum, Avx.Multiply(va, vb));
        }

        // Combine accumulators
        vSum = Avx.Add(vSum, vSum2);
        vSum3 = Avx.Add(vSum3, vSum4);
        vSum = Avx.Add(vSum, vSum3);

        // Horizontal sum
        Vector128<double> lower = vSum.GetLower();
        Vector128<double> upper = vSum.GetUpper();
        Vector128<double> combined = Sse2.Add(lower, upper);
        double sum = combined.GetElement(0) + combined.GetElement(1);

        // Process remaining elements (scalar)
        for (; i < len; i++)
        {
            sum += Unsafe.Add(ref aRef, i) * Unsafe.Add(ref bRef, i);
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProductNeon(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        int len = a.Length;
        int i = 0;
        Vector128<double> vSum = Vector128<double>.Zero;
        Vector128<double> vSum2 = Vector128<double>.Zero;
        Vector128<double> vSum3 = Vector128<double>.Zero;
        Vector128<double> vSum4 = Vector128<double>.Zero;

        ref double aRef = ref MemoryMarshal.GetReference(a);
        ref double bRef = ref MemoryMarshal.GetReference(b);

        // Unroll loop: Process 8 doubles (4 vectors) at a time
        if (len >= 8)
        {
            for (; i <= len - 8; i += 8)
            {
                var va1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
                var vb1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, i));

                var va2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 2));
                var vb2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 2));

                var va3 = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 4));
                var vb3 = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 4));

                var va4 = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 6));
                var vb4 = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 6));

                // NEON has FMA on ARM64
                // Since we are inside DotProductNeon which is guarded by AdvSimd.Arm64.IsSupported,
                // we can assume Arm64 support.
                vSum = AdvSimd.Arm64.FusedMultiplyAdd(vSum, va1, vb1);
                vSum2 = AdvSimd.Arm64.FusedMultiplyAdd(vSum2, va2, vb2);
                vSum3 = AdvSimd.Arm64.FusedMultiplyAdd(vSum3, va3, vb3);
                vSum4 = AdvSimd.Arm64.FusedMultiplyAdd(vSum4, va4, vb4);
            }
        }

        // Process remaining vectors (2 doubles at a time)
        for (; i <= len - 2; i += 2)
        {
            var va = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
            var vb = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, i));

            vSum = AdvSimd.Arm64.FusedMultiplyAdd(vSum, va, vb);
        }

        // Combine accumulators
        vSum = AdvSimd.Arm64.Add(vSum, vSum2);
        vSum3 = AdvSimd.Arm64.Add(vSum3, vSum4);
        vSum = AdvSimd.Arm64.Add(vSum, vSum3);

        // Horizontal sum (NEON has pairwise add)
        double sum = AdvSimd.Arm64.AddPairwiseScalar(vSum).ToScalar();

        // Scalar remainder (0-1 elements)
        for (; i < len; i++)
        {
            sum += Unsafe.Add(ref aRef, i) * Unsafe.Add(ref bRef, i);
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DotProductSse2(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        int len = a.Length;
        int i = 0;
        Vector128<double> vSum = Vector128<double>.Zero;
        Vector128<double> vSum2 = Vector128<double>.Zero;

        ref double aRef = ref MemoryMarshal.GetReference(a);
        ref double bRef = ref MemoryMarshal.GetReference(b);

        // Process 4 doubles at a time using 2 accumulators
        for (; i <= len - 4; i += 4)
        {
            var va1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
            var vb1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, i));
            var va2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, i + 2));
            var vb2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, i + 2));

            if (Fma.IsSupported)
            {
                vSum = Fma.MultiplyAdd(va1, vb1, vSum);
                vSum2 = Fma.MultiplyAdd(va2, vb2, vSum2);
            }
            else
            {
                vSum = Sse2.Add(vSum, Sse2.Multiply(va1, vb1));
                vSum2 = Sse2.Add(vSum2, Sse2.Multiply(va2, vb2));
            }
        }

        // Process remaining 2 doubles if available
        if (i <= len - 2)
        {
            var va = Vector128.LoadUnsafe(ref Unsafe.Add(ref aRef, i));
            var vb = Vector128.LoadUnsafe(ref Unsafe.Add(ref bRef, i));

            vSum = Fma.IsSupported
                ? Fma.MultiplyAdd(va, vb, vSum)
                : Sse2.Add(vSum, Sse2.Multiply(va, vb));
            i += 2;
        }

        vSum = Sse2.Add(vSum, vSum2);
        double sum = vSum.GetElement(0) + vSum.GetElement(1);

        // Scalar remainder (0-1 elements)
        for (; i < len; i++)
        {
            sum += Unsafe.Add(ref aRef, i) * Unsafe.Add(ref bRef, i);
        }

        return sum;
    }

    public static TSeries Calculate(TSeries source, double[] kernel)
    {
        var conv = new Conv(kernel);
        return conv.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double[] kernel)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (kernel == null || kernel.Length == 0)
            throw new ArgumentException("Kernel must not be empty", nameof(kernel));

        int len = source.Length;
        int period = kernel.Length;
        if (len == 0) return;

        // Use stackalloc for small kernels to avoid heap allocation
        Span<double> window = period <= 256 ? stackalloc double[period] : new double[period];

        double lastValid = double.NaN;
        int windowIdx = 0; // Points to where the NEXT value goes (circular)
        int count = 0;

        ReadOnlySpan<double> kernelSpan = kernel.AsSpan();

        for (int i = 0; i < len; i++)
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

            window[windowIdx] = val;
            windowIdx = (windowIdx + 1);
            if (windowIdx >= period) windowIdx = 0;

            if (count < period) count++;

            double sum = 0;

            if (count < period)
            {
                int kernelOffset = period - count;
                // Window is [0..count-1]
                sum = DotProduct(window.Slice(0, count), kernelSpan.Slice(kernelOffset));
            }
            else
            {
                // Full buffer - branchless version
                int part1Len = period - windowIdx;
                sum = DotProduct(window.Slice(windowIdx, part1Len), kernelSpan.Slice(0, part1Len))
                    + DotProduct(window.Slice(0, windowIdx), kernelSpan.Slice(part1Len));
            }

            output[i] = sum;
        }
    }

    public void Reset()
    {
        _buffer.Clear();
        _lastValidValue = double.NaN;
        _p_lastValidValue = double.NaN;
        _head = 0;
        Last = default;
    }
}
