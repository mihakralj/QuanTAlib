using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace QuanTAlib;

/// <summary>
/// HMA: Hull Moving Average
/// </summary>
/// <remarks>
/// HMA reduces lag by using a combination of weighted moving averages.
///
/// Calculation:
/// HMA = WMA(sqrt(n), 2 * WMA(n/2, price) - WMA(n, price))
///
/// Sources:
/// https://alan.hull.com.au/hma.html
/// </remarks>
[SkipLocalsInit]
public sealed class Hma : ITValuePublisher
{
    private readonly int _period;
    private readonly int _sqrtPeriod;
    private readonly Wma _wmaFull;
    private readonly Wma _wmaHalf;
    private readonly Wma _wmaSqrt;
    private int _sampleCount;

    public string Name { get; }
    public TValue Last { get; private set; }
    public bool IsHot => _sampleCount >= _period + _sqrtPeriod - 1;
    public event Action<TValue>? Pub;

    public Hma(int period)
    {
        if (period <= 1) throw new ArgumentException("Period must be greater than 1", nameof(period));

        _period = period;
        int halfPeriod = period / 2;
        _sqrtPeriod = (int)Math.Sqrt(period);

        _wmaFull = new Wma(period);
        _wmaHalf = new Wma(halfPeriod);
        _wmaSqrt = new Wma(_sqrtPeriod);

        Name = $"Hma({period})";
    }

    public Hma(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += (item) => Update(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew) _sampleCount++;

        // 1. Calculate WMA(n)
        TValue full = _wmaFull.Update(input, isNew);

        // 2. Calculate WMA(n/2)
        TValue half = _wmaHalf.Update(input, isNew);

        // 3. Calculate intermediate: 2 * WMA(n/2) - WMA(n)
        double intermediate = (2.0 * half.Value) - full.Value;

        // 4. Calculate HMA = WMA(sqrt(n), intermediate)
        Last = _wmaSqrt.Update(new TValue(input.Time, intermediate), isNew);

        Pub?.Invoke(Last);
        return Last;
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

        // Restore state for streaming
        _wmaFull.Reset();
        _wmaHalf.Reset();
        _wmaSqrt.Reset();

        int lookback = _period + (int)Math.Sqrt(_period) + 10; // Sufficient lookback
        int startIndex = Math.Max(0, len - lookback);

        for (int i = startIndex; i < len; i++)
        {
            Update(source[i]);
        }

        return new TSeries(t, v);
    }

    public static TSeries Calculate(TSeries source, int period)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        Calculate(source.Values, CollectionsMarshal.AsSpan(v), period);
        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (period <= 1)
            throw new ArgumentException("Period must be greater than 1", nameof(period));

        int len = source.Length;
        if (len == 0) return;

        int halfPeriod = period / 2;
        int sqrtPeriod = (int)Math.Sqrt(period);

        double[] rentedFull = System.Buffers.ArrayPool<double>.Shared.Rent(len);
        Span<double> fullWma = rentedFull.AsSpan(0, len);

        double[] rentedHalf = System.Buffers.ArrayPool<double>.Shared.Rent(len);
        Span<double> halfWma = rentedHalf.AsSpan(0, len);

        // Reuse halfWma buffer for intermediate results
        Span<double> intermediate = halfWma;

        try
        {
            Wma.Calculate(source, fullWma, period);
            Wma.Calculate(source, halfWma, halfPeriod);
            CalculateIntermediate(halfWma, fullWma, intermediate);
            Wma.Calculate(intermediate, output, sqrtPeriod);
        }
        finally
        {
            System.Buffers.ArrayPool<double>.Shared.Return(rentedFull);
            System.Buffers.ArrayPool<double>.Shared.Return(rentedHalf);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateIntermediate(ReadOnlySpan<double> halfWma, ReadOnlySpan<double> fullWma, Span<double> output)
    {
        int len = halfWma.Length;
        int i = 0;

        if (Vector256.IsHardwareAccelerated && len >= Vector256<double>.Count)
        {
            var vTwo = Vector256.Create(2.0);
            ref double halfRef = ref MemoryMarshal.GetReference(halfWma);
            ref double fullRef = ref MemoryMarshal.GetReference(fullWma);
            ref double outRef = ref MemoryMarshal.GetReference(output);

            for (; i <= len - Vector256<double>.Count; i += Vector256<double>.Count)
            {
                var vHalf = Vector256.LoadUnsafe(ref Unsafe.Add(ref halfRef, i));
                var vFull = Vector256.LoadUnsafe(ref Unsafe.Add(ref fullRef, i));

                // vResult = 2 * half - full
                var vResult = (vHalf * vTwo) - vFull;

                Vector256.StoreUnsafe(vResult, ref Unsafe.Add(ref outRef, i));
            }
        }

        for (; i < len; i++)
        {
            output[i] = (2.0 * halfWma[i]) - fullWma[i];
        }
    }

    public void Reset()
    {
        _wmaFull.Reset();
        _wmaHalf.Reset();
        _wmaSqrt.Reset();
        _sampleCount = 0;
        Last = default;
    }
}
