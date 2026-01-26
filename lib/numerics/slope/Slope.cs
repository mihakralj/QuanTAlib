using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// SLOPE: First Derivative (Rate of Change)
/// Measures the velocity of price movement - the instantaneous rate of change.
/// </summary>
/// <remarks>
/// The first derivative approximates velocity: how fast the value is changing.
///
/// Formula:
/// Slope_t = Value_t - Value_{t-1}
///
/// Key properties:
/// - O(1) streaming complexity
/// - Zero allocations in hot path
/// - SIMD-optimized batch calculation
/// </remarks>
[SkipLocalsInit]
public sealed class Slope : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double PrevValue, double LastValidValue, int Count);
    private State _state;
    private State _p_state;
    private readonly TValuePublishedHandler _handler;

    public override bool IsHot => _state.Count >= 2;

    /// <summary>
    /// Creates a new Slope (first derivative) indicator.
    /// </summary>
    public Slope()
    {
        Name = "Slope";
        WarmupPeriod = 2;
        _handler = Handle;
    }

    /// <summary>
    /// Creates a new Slope indicator with event subscription.
    /// </summary>
    public Slope(ITValuePublisher source) : this()
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

            result = _state.Count >= 1 ? val - _state.PrevValue : 0.0;

            _state.PrevValue = val;
            _state.Count = Math.Min(_state.Count + 1, 2);
        }
        else
        {
            // Rollback for bar correction
            _state.LastValidValue = _p_state.LastValidValue;
            double val = GetValidValue(input.Value);

            result = _p_state.Count >= 1 ? val - _p_state.PrevValue : 0.0;

            _state.PrevValue = val;
            _state.Count = Math.Max(_p_state.Count, 1);
        }

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

        // Prime state with last value
        if (len >= 1)
        {
            _state.PrevValue = double.IsFinite(sourceValues[len - 1]) ? sourceValues[len - 1] : _state.LastValidValue;
            _state.Count = Math.Min(len, 2);
            _state.LastValidValue = _state.PrevValue;
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
        var slope = new Slope();
        return slope.Update(source);
    }

    /// <summary>
    /// Calculates first derivative (slope) for a span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // First element has no previous - set to 0
        output[0] = 0.0;
        if (len == 1)
        {
            return;
        }

        int i = 1;

        // Check if all values are finite before using SIMD
        // SIMD paths don't handle NaN/Infinity properly
        bool allFinite = !source.ContainsNonFinite();

        // Only use SIMD if all values are finite
        if (allFinite)
        {
            // AVX512: 8 doubles at once
            if (Avx512F.IsSupported && len >= 9)
            {
                const int VectorWidth = 8;
                int simdEnd = len - VectorWidth + 1;
                ref double srcRef = ref MemoryMarshal.GetReference(source);
                ref double outRef = ref MemoryMarshal.GetReference(output);

                for (; i < simdEnd; i += VectorWidth)
                {
                    var current = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
                    var prev = Vector512.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 1));
                    var diff = Avx512F.Subtract(current, prev);
                    diff.StoreUnsafe(ref Unsafe.Add(ref outRef, i));
                }
            }
            // AVX: 4 doubles at once
            else if (Avx.IsSupported && len >= 5)
            {
                const int VectorWidth = 4;
                int simdEnd = len - VectorWidth + 1;
                ref double srcRef = ref MemoryMarshal.GetReference(source);
                ref double outRef = ref MemoryMarshal.GetReference(output);

                for (; i < simdEnd; i += VectorWidth)
                {
                    var current = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
                    var prev = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 1));
                    var diff = Avx.Subtract(current, prev);
                    diff.StoreUnsafe(ref Unsafe.Add(ref outRef, i));
                }
            }
            // ARM64 Neon: 2 doubles at once
            else if (AdvSimd.Arm64.IsSupported && len >= 3)
            {
                const int VectorWidth = 2;
                int simdEnd = len - VectorWidth + 1;
                ref double srcRef = ref MemoryMarshal.GetReference(source);
                ref double outRef = ref MemoryMarshal.GetReference(output);

                for (; i < simdEnd; i += VectorWidth)
                {
                    var current = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
                    var prev = Vector128.LoadUnsafe(ref Unsafe.Add(ref srcRef, i - 1));
                    var diff = AdvSimd.Arm64.Subtract(current, prev);
                    diff.StoreUnsafe(ref Unsafe.Add(ref outRef, i));
                }
            }
        }

        // Scalar fallback for remaining elements
        // Track last valid value forward to avoid O(n²) backward scanning
        double lastValid = 0.0;
        // Find first valid value if we're starting from the beginning
        if (i == 1)
        {
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(source[k]))
                {
                    lastValid = source[k];
                    break;
                }
            }
        }
        else if (i > 1)
        {
            // We already processed some elements via SIMD, find last valid from processed
            for (int k = i - 1; k >= 0; k--)
            {
                if (double.IsFinite(source[k]))
                {
                    lastValid = source[k];
                    break;
                }
            }
        }

        double prevValid = lastValid;
        for (; i < len; i++)
        {
            double curr = source[i];
            double prev = source[i - 1];

            // Handle NaN/Infinity using tracked last valid values
            if (double.IsFinite(curr))
            {
                lastValid = curr;
            }
            else
            {
                curr = lastValid;
            }

            if (double.IsFinite(prev))
            {
                prevValid = prev;
            }
            else
            {
                prev = prevValid;
            }

            output[i] = curr - prev;
        }
    }
}
