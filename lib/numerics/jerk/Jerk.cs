using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// JERK: Third Derivative (Rate of Acceleration Change)
/// Measures how fast the acceleration is changing - the "jerk" in physics terms.
/// </summary>
/// <remarks>
/// The third derivative approximates jerk: the rate of change of acceleration.
///
/// Formula:
/// Jerk_t = Accel_t - Accel_{t-1}
///        = (Value_t - 2*Value_{t-1} + Value_{t-2}) - (Value_{t-1} - 2*Value_{t-2} + Value_{t-3})
///        = Value_t - 3*Value_{t-1} + 3*Value_{t-2} - Value_{t-3}
///
/// Key properties:
/// - O(1) streaming complexity
/// - Zero allocations in hot path
/// - SIMD-optimized batch calculation
/// </remarks>
[SkipLocalsInit]
public sealed class Jerk : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Prev1, double Prev2, double Prev3, double LastValidValue, int Count);
    private State _state;
    private State _p_state;
    private readonly TValuePublishedHandler _handler;

    public override bool IsHot => _state.Count >= 4;

    /// <summary>
    /// Creates a new Jerk (third derivative) indicator.
    /// </summary>
    public Jerk()
    {
        Name = "Jerk";
        WarmupPeriod = 4;
        _handler = Handle;
    }

    /// <summary>
    /// Creates a new Jerk indicator with event subscription.
    /// </summary>
    public Jerk(ITValuePublisher source) : this()
    {
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

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
    public override TValue Update(TValue input, bool isNew = true)
    {
        double result;

        if (isNew)
        {
            _p_state = _state;
            double val = GetValidValue(input.Value);

            if (_state.Count >= 3)
            {
            // jerk = val - 3*prev1 + 3*prev2 - prev3
            // Using FMA: val - 3*prev1 + 3*prev2 - prev3
                // = FMA(-3, prev1, val) + FMA(3, prev2, -prev3)
                double term1 = Math.FusedMultiplyAdd(-3.0, _state.Prev1, val);
                double term2 = Math.FusedMultiplyAdd(3.0, _state.Prev2, -_state.Prev3);
                result = term1 + term2;
            }
            else
            {
                result = 0.0;
            }

            // Shift history
            _state.Prev3 = _state.Prev2;
            _state.Prev2 = _state.Prev1;
            _state.Prev1 = val;
            _state.Count = Math.Min(_state.Count + 1, 4);
        }
        else
        {
            // Rollback for bar correction
            _state.LastValidValue = _p_state.LastValidValue;
            double val = GetValidValue(input.Value);

            if (_p_state.Count >= 3)
            {
                double term1 = Math.FusedMultiplyAdd(-3.0, _p_state.Prev1, val);
                double term2 = Math.FusedMultiplyAdd(3.0, _p_state.Prev2, -_p_state.Prev3);
                result = term1 + term2;
            }
            else
            {
                result = 0.0;
            }

            // Update current state from previous (don't shift)
            _state.Prev3 = _p_state.Prev3;
            _state.Prev2 = _p_state.Prev2;
            _state.Prev1 = val;
            _state.Count = Math.Max(_p_state.Count, 1);
        }

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;

        // Cache source spans ONCE before any operations to avoid repeated property access
        ReadOnlySpan<double> sourceValues = source.Values;
        ReadOnlySpan<long> sourceTimes = source.Times;

        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Calculate(sourceValues, vSpan);
        sourceTimes.CopyTo(tSpan);

        // Prime state with last three values using cached span
        if (len >= 3)
        {
            double v1 = double.IsFinite(sourceValues[len - 1]) ? sourceValues[len - 1] : _state.LastValidValue;
            double v2 = double.IsFinite(sourceValues[len - 2]) ? sourceValues[len - 2] : v1;
            double v3 = double.IsFinite(sourceValues[len - 3]) ? sourceValues[len - 3] : v2;
            _state.Prev1 = v1;
            _state.Prev2 = v2;
            _state.Prev3 = v3;
            _state.LastValidValue = v1;
            _state.Count = Math.Min(len, 4);
            _p_state = _state;
        }
        else if (len == 2)
        {
            double v1 = double.IsFinite(sourceValues[1]) ? sourceValues[1] : _state.LastValidValue;
            double v2 = double.IsFinite(sourceValues[0]) ? sourceValues[0] : v1;
            _state.Prev1 = v1;
            _state.Prev2 = v2;
            _state.LastValidValue = v1;
            _state.Count = 2;
            _p_state = _state;
        }
        else if (len == 1)
        {
            double v1 = double.IsFinite(sourceValues[0]) ? sourceValues[0] : _state.LastValidValue;
            _state.Prev1 = v1;
            _state.LastValidValue = v1;
            _state.Count = 1;
            _p_state = _state;
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public override void Reset()
    {
        _state = default;
        _p_state = default;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double val in source)
        {
            Update(new TValue(DateTime.MinValue, val));
        }
    }

    public static TSeries Calculate(TSeries source)
    {
        var jerk = new Jerk();
        return jerk.Update(source);
    }

    /// <summary>
    /// Calculates third derivative (jerk) for a span.
    /// jerk[i] = source[i] - 3*source[i-1] + 3*source[i-2] - source[i-3]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));

        int len = source.Length;
        if (len == 0) return;

        // First three elements have insufficient history
        output[0] = 0.0;
        if (len == 1) return;
        output[1] = 0.0;
        if (len == 2) return;
        output[2] = 0.0;
        if (len == 3) return;

        int i = 3;

        // Check for non-finite values before using SIMD (SIMD doesn't handle NaN properly)
        bool allFinite = !source.ContainsNonFinite();

        // AVX512: 8 doubles at once (only if all values are finite)
        if (allFinite && Avx512F.IsSupported && len >= 11)
        {
            var three = Vector512.Create(3.0);
            var negThree = Vector512.Create(-3.0);
            const int VectorWidth = 8;
            int simdEnd = len - ((len - 3) % VectorWidth);
            ref double srcRef = ref MemoryMarshal.GetReference(source);
            ref double outRef = ref MemoryMarshal.GetReference(output);

            for (; i < simdEnd; i += VectorWidth)
            {
                var current = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
                var prev1 = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 1));
                var prev2 = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 2));
                var prev3 = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 3));
                // jerk = current - 3*prev1 + 3*prev2 - prev3
                // Using FMA: FMA(-3, prev1, current) + FMA(3, prev2, -prev3)
                var term1 = Avx512F.FusedMultiplyAdd(negThree, prev1, current);
                var negPrev3 = Avx512F.Subtract(Vector512<double>.Zero, prev3);
                var term2 = Avx512F.FusedMultiplyAdd(three, prev2, negPrev3);
                var result = Avx512F.Add(term1, term2);
                result.StoreUnsafe(ref Unsafe.Add(ref outRef, i));
            }
        }
        // AVX2 with FMA: 4 doubles at once (only if all values are finite)
        else if (allFinite && Fma.IsSupported && len >= 7)
        {
            var three = Vector256.Create(3.0);
            var negThree = Vector256.Create(-3.0);
            const int VectorWidth = 4;
            int simdEnd = len - ((len - 3) % VectorWidth);
            ref double srcRef = ref MemoryMarshal.GetReference(source);
            ref double outRef = ref MemoryMarshal.GetReference(output);

            for (; i < simdEnd; i += VectorWidth)
            {
                var current = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
                var prev1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 1));
                var prev2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 2));
                var prev3 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 3));
                // jerk = current - 3*prev1 + 3*prev2 - prev3
                // Using FMA: FMA(-3, prev1, current) + FMA(3, prev2, -prev3)
                var term1 = Fma.MultiplyAdd(negThree, prev1, current);
                var negPrev3 = Avx.Subtract(Vector256<double>.Zero, prev3);
                var term2 = Fma.MultiplyAdd(three, prev2, negPrev3);
                var result = Avx.Add(term1, term2);
                result.StoreUnsafe(ref Unsafe.Add(ref outRef, i));
            }
        }
        // AVX fallback (no FMA): 4 doubles at once (only if all values are finite)
        else if (allFinite && Avx.IsSupported && len >= 7)
        {
            var three = Vector256.Create(3.0);
            const int VectorWidth = 4;
            int simdEnd = len - ((len - 3) % VectorWidth);
            ref double srcRef = ref MemoryMarshal.GetReference(source);
            ref double outRef = ref MemoryMarshal.GetReference(output);

            for (; i < simdEnd; i += VectorWidth)
            {
                var current = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
                var prev1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 1));
                var prev2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 2));
                var prev3 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 3));
                var threeTimesP1 = Avx.Multiply(three, prev1);
                var threeTimesP2 = Avx.Multiply(three, prev2);
                var result = Avx.Subtract(current, threeTimesP1);
                result = Avx.Add(result, threeTimesP2);
                result = Avx.Subtract(result, prev3);
                result.StoreUnsafe(ref Unsafe.Add(ref outRef, i));
            }
        }
        // ARM64 Neon with FMA: 2 doubles at once (only if all values are finite)
        else if (allFinite && AdvSimd.Arm64.IsSupported && len >= 5)
        {
            var three = Vector128.Create(3.0);
            var negThree = Vector128.Create(-3.0);
            const int VectorWidth = 2;
            int simdEnd = len - ((len - 3) % VectorWidth);
            ref double srcRef = ref MemoryMarshal.GetReference(source);
            ref double outRef = ref MemoryMarshal.GetReference(output);

            for (; i < simdEnd; i += VectorWidth)
            {
                var current = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
                var prev1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 1));
                var prev2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 2));
                var prev3 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 3));
                // jerk = current - 3*prev1 + 3*prev2 - prev3
                // Using FMA: FMA(-3, prev1, current) + FMA(3, prev2, -prev3)
                var term1 = AdvSimd.Arm64.FusedMultiplyAdd(current, negThree, prev1);
                var negPrev3 = AdvSimd.Arm64.Subtract(Vector128<double>.Zero, prev3);
                var term2 = AdvSimd.Arm64.FusedMultiplyAdd(negPrev3, three, prev2);
                var result = AdvSimd.Arm64.Add(term1, term2);
                result.StoreUnsafe(ref Unsafe.Add(ref outRef, i));
            }
        }

        // Scalar fallback for remaining elements
        // Initialize prev values from actual data at positions i-1, i-2, i-3
        for (; i < len; i++)
        {
            double curr = source[i];
            double p1 = source[i - 1];
            double p2 = source[i - 2];
            double p3 = source[i - 3];

            // Handle NaN/Infinity by substitution (find first finite value)
            double fallback = FindFinite(curr, p1, p2, p3);
            if (!double.IsFinite(curr)) curr = fallback;
            if (!double.IsFinite(p1)) p1 = fallback;
            if (!double.IsFinite(p2)) p2 = fallback;
            if (!double.IsFinite(p3)) p3 = fallback;

            // jerk = curr - 3*prev1 + 3*prev2 - prev3
            double term1 = Math.FusedMultiplyAdd(-3.0, p1, curr);
            double term2 = Math.FusedMultiplyAdd(3.0, p2, -p3);
            output[i] = term1 + term2;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double FindFinite(double a, double b, double c, double d)
    {
        if (double.IsFinite(a)) return a;
        if (double.IsFinite(b)) return b;
        if (double.IsFinite(c)) return c;
        if (double.IsFinite(d)) return d;
        return 0.0;
    }
}