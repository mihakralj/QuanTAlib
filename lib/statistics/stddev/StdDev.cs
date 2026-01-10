using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// Standard Deviation: Measures the amount of variation or dispersion of a set of values.
/// </summary>
/// <remarks>
/// Standard Deviation is the square root of Variance.
///
/// Formula:
/// StdDev = Sqrt(Variance)
///
/// This implementation wraps the optimized Variance indicator and applies a square root.
/// </remarks>
[SkipLocalsInit]
public sealed class StdDev : AbstractBase
{
    private readonly Variance _variance;
    private readonly int _period;
    private readonly bool _isPopulation;

    public override bool IsHot => _variance.IsHot;

    /// <summary>
    /// Creates a new Standard Deviation indicator.
    /// </summary>
    /// <param name="period">The lookback period.</param>
    /// <param name="isPopulation">If true, calculates Population StdDev (div by N). If false, Sample StdDev (div by N-1). Default is false (Sample).</param>
    public StdDev(int period, bool isPopulation = false)
    {
        _period = period;
        _isPopulation = isPopulation;
        _variance = new Variance(period, isPopulation);
        Name = $"StdDev({period})";
        WarmupPeriod = period;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        TValue varResult = _variance.Update(input, isNew);

        // Sqrt(Variance)
        // Handle potential negative zero or extremely small negative noise from Variance
        double val = varResult.Value;
        double stdDev = (val > 0) ? Math.Sqrt(val) : 0.0;

        Last = new TValue(input.Time, stdDev);
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

        // 1. Calculate Variance
        Variance.Batch(source.Values, vSpan, _period, _isPopulation);

        // 2. Calculate Sqrt in-place
        SqrtSpan(vSpan);

        source.Times.CopyTo(tSpan);

        // Prime the state
        // We need to feed the last 'period' values into the _variance instance
        // so that subsequent streaming updates work correctly.
        int primeStart = Math.Max(0, len - _period);
        for (int i = primeStart; i < len; i++)
        {
            Update(source[i]);
        }

        return new TSeries(t, v);
    }

    public override void Reset()
    {
        _variance.Reset();
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        _variance.Prime(source);
        // Update Last based on _variance.Last
        if (_variance.Last.Time != default)
        {
            double val = _variance.Last.Value;
            Last = new TValue(_variance.Last.Time, (val > 0) ? Math.Sqrt(val) : 0.0);
        }
    }

    public static TSeries Calculate(TSeries source, int period, bool isPopulation = false)
    {
        var stdDev = new StdDev(period, isPopulation);
        return stdDev.Update(source);
    }

    /// <summary>
    /// Calculates Standard Deviation in-place.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, bool isPopulation = false)
    {
        // 1. Calculate Variance
        Variance.Batch(source, output, period, isPopulation);

        // 2. Sqrt
        SqrtSpan(output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SqrtSpan(Span<double> data)
    {
        int i = 0;
        int len = data.Length;

        // AVX512
        if (Avx512F.IsSupported)
        {
            const int VectorWidth = 8;
            int simdEnd = len - (len % VectorWidth);
            ref double dataRef = ref MemoryMarshal.GetReference(data);

            for (; i < simdEnd; i += VectorWidth)
            {
                var v = Vector512.LoadUnsafe(ref Unsafe.Add(ref dataRef, i));
                var vSqrt = Avx512F.Sqrt(v);
                vSqrt.StoreUnsafe(ref Unsafe.Add(ref dataRef, i));
            }
        }
        // AVX
        else if (Avx.IsSupported)
        {
            const int VectorWidth = 4;
            int simdEnd = len - (len % VectorWidth);
            ref double dataRef = ref MemoryMarshal.GetReference(data);

            for (; i < simdEnd; i += VectorWidth)
            {
                var v = Vector256.LoadUnsafe(ref Unsafe.Add(ref dataRef, i));
                var vSqrt = Avx.Sqrt(v);
                vSqrt.StoreUnsafe(ref Unsafe.Add(ref dataRef, i));
            }
        }
        // ARM64 Neon
        else if (AdvSimd.Arm64.IsSupported)
        {
            const int VectorWidth = 2;
            int simdEnd = len - (len % VectorWidth);
            ref double dataRef = ref MemoryMarshal.GetReference(data);

            for (; i < simdEnd; i += VectorWidth)
            {
                var v = Vector128.LoadUnsafe(ref Unsafe.Add(ref dataRef, i));
                var vSqrt = AdvSimd.Arm64.Sqrt(v);
                vSqrt.StoreUnsafe(ref Unsafe.Add(ref dataRef, i));
            }
        }

        // Scalar fallback
        for (; i < len; i++)
        {
            double val = data[i];
            data[i] = (val > 0) ? Math.Sqrt(val) : 0.0;
        }
    }
}
