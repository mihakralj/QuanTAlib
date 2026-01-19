using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// ACCEL: Second Derivative (Acceleration)
/// Measures the rate of change of velocity - the acceleration of price movement.
/// </summary>
/// <remarks>
/// The second derivative approximates acceleration: how fast the velocity is changing.
///
/// Formula:
/// Accel_t = Slope_t - Slope_{t-1}
///         = (Value_t - Value_{t-1}) - (Value_{t-1} - Value_{t-2})
///         = Value_t - 2*Value_{t-1} + Value_{t-2}
///
/// Key properties:
/// - O(1) streaming complexity
/// - Zero allocations in hot path
/// - SIMD-optimized batch calculation
/// </remarks>
[SkipLocalsInit]
public sealed class Accel : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Prev1, double Prev2, double LastValidValue, int Count);
    private State _state;
    private State _p_state;
    private readonly TValuePublishedHandler _handler;

    public override bool IsHot => _state.Count >= 3;

    /// <summary>
    /// Creates a new Accel (second derivative) indicator.
    /// </summary>
    public Accel()
    {
        Name = "Accel";
        WarmupPeriod = 3;
        _handler = Handle;
    }

    /// <summary>
    /// Creates a new Accel indicator with event subscription.
    /// </summary>
    public Accel(ITValuePublisher source) : this()
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

            // accel = val - 2*prev1 + prev2
            result = _state.Count >= 2
                ? Math.FusedMultiplyAdd(-2.0, _state.Prev1, val + _state.Prev2)
                : 0.0;

            // Shift history
            _state.Prev2 = _state.Prev1;
            _state.Prev1 = val;
            _state.Count = Math.Min(_state.Count + 1, 3);
        }
        else
        {
            // Rollback for bar correction
            _state.LastValidValue = _p_state.LastValidValue;
            double val = GetValidValue(input.Value);

            // accel = val - 2*prev1 + prev2
            result = _p_state.Count >= 2
                ? Math.FusedMultiplyAdd(-2.0, _p_state.Prev1, val + _p_state.Prev2)
                : 0.0;

            // Update current state from previous (don't shift)
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

        // Prime state with last two values using cached span
        if (len >= 2)
        {
            double v1 = double.IsFinite(sourceValues[len - 1]) ? sourceValues[len - 1] : _state.LastValidValue;
            double v2 = double.IsFinite(sourceValues[len - 2]) ? sourceValues[len - 2] : v1;
            _state.Prev1 = v1;
            _state.Prev2 = v2;
            _state.LastValidValue = v1;
            _state.Count = Math.Min(len, 3);
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
        // TValue is a readonly record struct - no heap allocation occurs
        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        DateTime time = DateTime.UtcNow - (interval * source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(time, source[i]), true);
            time += interval;
        }
    }

    public static TSeries Calculate(TSeries source)
    {
        var accel = new Accel();
        return accel.Update(source);
    }

    /// <summary>
    /// Calculates second derivative (acceleration) for a span.
    /// accel[i] = source[i] - 2*source[i-1] + source[i-2]
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));

        int len = source.Length;
        if (len == 0) return;

        // First two elements have insufficient history
        output[0] = 0.0;
        if (len == 1) return;
        output[1] = 0.0;
        if (len == 2) return;

        int i = 2;

        // Check for non-finite values - if any exist, use scalar path only
        bool hasNonFinite = false;
        for (int k = 0; k < len && !hasNonFinite; k++)
        {
            hasNonFinite = !double.IsFinite(source[k]);
        }

        // AVX512: 8 doubles at once (only if all values are finite)
        if (!hasNonFinite && Avx512F.IsSupported && len >= 10)
        {
            var two = Vector512.Create(2.0);
            const int VectorWidth = 8;
            int simdEnd = len - ((len - 2) % VectorWidth);
            ref double srcRef = ref MemoryMarshal.GetReference(source);
            ref double outRef = ref MemoryMarshal.GetReference(output);

            for (; i < simdEnd; i += VectorWidth)
            {
                var current = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
                var prev1 = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 1));
                var prev2 = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 2));
                // accel = current - 2*prev1 + prev2
                var twoTimesP1 = Avx512F.Multiply(two, prev1);
                var diff = Avx512F.Subtract(current, twoTimesP1);
                var result = Avx512F.Add(diff, prev2);
                result.StoreUnsafe(ref Unsafe.Add(ref outRef, i));
            }
        }
        // AVX: 4 doubles at once (only if all values are finite)
        else if (!hasNonFinite && Avx.IsSupported && len >= 6)
        {
            var two = Vector256.Create(2.0);
            const int VectorWidth = 4;
            int simdEnd = len - ((len - 2) % VectorWidth);
            ref double srcRef = ref MemoryMarshal.GetReference(source);
            ref double outRef = ref MemoryMarshal.GetReference(output);

            for (; i < simdEnd; i += VectorWidth)
            {
                var current = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
                var prev1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 1));
                var prev2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 2));
                var twoTimesP1 = Avx.Multiply(two, prev1);
                var diff = Avx.Subtract(current, twoTimesP1);
                var result = Avx.Add(diff, prev2);
                result.StoreUnsafe(ref Unsafe.Add(ref outRef, i));
            }
        }
        // ARM64 Neon: 2 doubles at once (only if all values are finite)
        else if (!hasNonFinite && AdvSimd.Arm64.IsSupported && len >= 4)
        {
            var two = Vector128.Create(2.0);
            const int VectorWidth = 2;
            int simdEnd = len - ((len - 2) % VectorWidth);
            ref double srcRef = ref MemoryMarshal.GetReference(source);
            ref double outRef = ref MemoryMarshal.GetReference(output);

            for (; i < simdEnd; i += VectorWidth)
            {
                var current = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
                var prev1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 1));
                var prev2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 2));
                var twoTimesP1 = AdvSimd.Arm64.Multiply(two, prev1);
                var diff = AdvSimd.Arm64.Subtract(current, twoTimesP1);
                var result = AdvSimd.Arm64.Add(diff, prev2);
                result.StoreUnsafe(ref Unsafe.Add(ref outRef, i));
            }
        }

        // Scalar fallback for remaining elements
        // Initialize prev values from actual data at position i-1 and i-2
        for (; i < len; i++)
        {
            double curr = source[i];
            double p1 = source[i - 1];
            double p2 = source[i - 2];

            // Handle NaN/Infinity by substitution (find first finite value)
            double fallback = FindFinite(curr, p1, p2);
            if (!double.IsFinite(curr)) curr = fallback;
            if (!double.IsFinite(p1)) p1 = fallback;
            if (!double.IsFinite(p2)) p2 = fallback;

            // accel = curr - 2*prev1 + prev2
            output[i] = Math.FusedMultiplyAdd(-2.0, p1, curr + p2);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double FindFinite(double a, double b, double c)
    {
        if (double.IsFinite(a)) return a;
        if (double.IsFinite(b)) return b;
        if (double.IsFinite(c)) return c;
        return 0.0;
    }
}
