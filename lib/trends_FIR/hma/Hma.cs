using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace QuanTAlib;

/// <summary>
/// HMA: Hull Moving Average
/// </summary>
/// <remarks>
/// Lag-reduced MA combining weighted MAs with square root smoothing.
/// SIMD-accelerated intermediate calculation (AVX-512/AVX2/NEON).
///
/// Calculation: <c>HMA = WMA(√n, 2×WMA(n/2) - WMA(n))</c>.
/// </remarks>
/// <seealso href="Hma.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Hma : AbstractBase
{
    private readonly int _period;
    private readonly int _sqrtPeriod;
    private readonly Wma _wmaFull;
    private readonly Wma _wmaHalf;
    private readonly Wma _wmaSqrt;
    private readonly TValuePublishedHandler _handler;
    private int _sampleCount;

    public override bool IsHot => _sampleCount >= WarmupPeriod;

    public Hma(int period)
    {
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        _period = period;
        int halfPeriod = period / 2;
        _sqrtPeriod = (int)Math.Sqrt(period);

        _wmaFull = new Wma(period);
        _wmaHalf = new Wma(halfPeriod);
        _wmaSqrt = new Wma(_sqrtPeriod);
        _handler = Handle;

        Name = $"Hma({period})";
        WarmupPeriod = period + _sqrtPeriod - 1; // WMA needs period, then WMA(sqrt) needs sqrt_period. Total lag/warmup.
    }

    public Hma(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _sampleCount++;
        }

        // 1. Calculate WMA(n)
        TValue full = _wmaFull.Update(input, isNew);

        // 2. Calculate WMA(n/2)
        TValue half = _wmaHalf.Update(input, isNew);

        // 3. Calculate intermediate: 2 * WMA(n/2) - WMA(n)
        double intermediate = (2.0 * half.Value) - full.Value;

        // 4. Calculate HMA = WMA(sqrt(n), intermediate)
        Last = _wmaSqrt.Update(new TValue(input.Time, intermediate), isNew);

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

        // Restore state for streaming
        Reset();

        // We need to replay enough history to get the state right.
        // HMA depends on 3 WMAs.
        // WMA state depends on the last 'period' values.
        // So we need to replay at least _period + _sqrtPeriod + buffer.
        int lookback = _period + _sqrtPeriod + 10;
        int startIndex = Math.Max(0, len - lookback);

        // We can't easily set _sampleCount without replaying, or we assume it's just count.
        // But WMA internal state needs to be restored.
        // Since WMA doesn't expose Prime/State easily (unless we cast and check), replaying is safer.

        for (int i = startIndex; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]));
        }

        // Adjust sample count to reflect actual total samples processed
        _sampleCount = len;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    public static TSeries Batch(TSeries source, int period)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        Batch(source.Values, CollectionsMarshal.AsSpan(v), period);
        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        int halfPeriod = period / 2;
        int sqrtPeriod = (int)Math.Sqrt(period);

        double[] rentedFull = System.Buffers.ArrayPool<double>.Shared.Rent(len);
        Span<double> fullWma = rentedFull.AsSpan(0, len);

        double[] rentedHalf = System.Buffers.ArrayPool<double>.Shared.Rent(len);
        Span<double> halfWma = rentedHalf.AsSpan(0, len);

        // Reuse halfWma buffer for intermediate results to save memory/allocations
        // But we need halfWma values for the calculation.
        // Wait, CalculateIntermediate reads halfWma and fullWma and writes to output.
        // So we can write to 'halfWma' IF we don't need 'halfWma' anymore.
        // CalculateIntermediate iterates. If we write to halfWma in place, we overwrite values we might need if we were doing something else.
        // But here: output[i] = 2*half[i] - full[i].
        // This is element-wise. So we CAN overwrite half[i] with the result if we process carefully or if we don't need half[i] later.
        // We don't need half[i] later.
        // So we can use halfWma as the intermediate buffer.

        Span<double> intermediate = halfWma;

        try
        {
            Wma.Batch(source, fullWma, period);
            Wma.Batch(source, halfWma, halfPeriod);
            CalculateIntermediate(halfWma, fullWma, intermediate);
            Wma.Batch(intermediate, output, sqrtPeriod);
        }
        finally
        {
            System.Buffers.ArrayPool<double>.Shared.Return(rentedFull);
            System.Buffers.ArrayPool<double>.Shared.Return(rentedHalf);
        }
    }

    public static (TSeries Results, Hma Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Hma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateIntermediate(ReadOnlySpan<double> halfWma, ReadOnlySpan<double> fullWma, Span<double> output)
    {
        int len = halfWma.Length;
        int i = 0;

        ref double halfRef = ref MemoryMarshal.GetReference(halfWma);
        ref double fullRef = ref MemoryMarshal.GetReference(fullWma);
        ref double outRef = ref MemoryMarshal.GetReference(output);

        if (Avx512F.IsSupported && len >= Vector512<double>.Count)
        {
            var vTwo = Vector512.Create(2.0);
            for (; i <= len - Vector512<double>.Count; i += Vector512<double>.Count)
            {
                var vHalf = Vector512.LoadUnsafe(ref Unsafe.Add(ref halfRef, i));
                var vFull = Vector512.LoadUnsafe(ref Unsafe.Add(ref fullRef, i));
                var vResult = Avx512F.Subtract(Avx512F.Multiply(vHalf, vTwo), vFull);
                vResult.StoreUnsafe(ref Unsafe.Add(ref outRef, i));
            }
        }
        else if (Avx2.IsSupported && len >= Vector256<double>.Count)
        {
            var vTwo = Vector256.Create(2.0);
            for (; i <= len - Vector256<double>.Count; i += Vector256<double>.Count)
            {
                var vHalf = Vector256.LoadUnsafe(ref Unsafe.Add(ref halfRef, i));
                var vFull = Vector256.LoadUnsafe(ref Unsafe.Add(ref fullRef, i));
                var vResult = Avx.Subtract(Avx.Multiply(vHalf, vTwo), vFull);
                vResult.StoreUnsafe(ref Unsafe.Add(ref outRef, i));
            }
        }
        else if (AdvSimd.Arm64.IsSupported && len >= Vector128<double>.Count)
        {
            var vTwo = Vector128.Create(2.0);
            for (; i <= len - Vector128<double>.Count; i += Vector128<double>.Count)
            {
                var vHalf = Vector128.LoadUnsafe(ref Unsafe.Add(ref halfRef, i));
                var vFull = Vector128.LoadUnsafe(ref Unsafe.Add(ref fullRef, i));
                var vResult = AdvSimd.Arm64.Subtract(AdvSimd.Arm64.Multiply(vHalf, vTwo), vFull);
                vResult.StoreUnsafe(ref Unsafe.Add(ref outRef, i));
            }
        }

        for (; i < len; i++)
        {
            output[i] = (2.0 * halfWma[i]) - fullWma[i];
        }
    }

    public override void Reset()
    {
        _wmaFull.Reset();
        _wmaHalf.Reset();
        _wmaSqrt.Reset();
        _sampleCount = 0;
        Last = default;
    }
}