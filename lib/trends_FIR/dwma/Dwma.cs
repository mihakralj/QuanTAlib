using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DWMA: Double Weighted Moving Average
/// </summary>
/// <remarks>
/// Double-pass WMA for enhanced smoothing with slight additional lag.
/// Triangular-like weighting via cascaded linear filters.
///
/// Calculation: <c>DWMA = WMA(WMA(source, n), n)</c>.
/// </remarks>
/// <seealso href="Dwma.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Dwma : AbstractBase
{
    private readonly int _period;
    private readonly Wma _wma1;
    private readonly Wma _wma2;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _handler;
    private bool _disposed;
    private int _sampleCount;

    public override bool IsHot => _sampleCount >= WarmupPeriod;

    /// <summary>
    /// Creates DWMA with specified period.
    /// </summary>
    /// <param name="period">Window size (must be > 0)</param>
    public Dwma(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _wma1 = new Wma(period);
        _wma2 = new Wma(period);
        Name = $"Dwma({period})";
        WarmupPeriod = (period * 2) - 1;
    }

    public Dwma(ITValuePublisher source, int period) : this(period)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _sampleCount++;
        }

        TValue wma1Result = _wma1.Update(input, isNew);
        Last = _wma2.Update(wma1Result, isNew);
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

        source.Times.CopyTo(tSpan);
        Batch(source.Values, vSpan, _period);

        Reset();

        int lookback = WarmupPeriod + 10;
        int startIndex = Math.Max(0, len - lookback);

        for (int i = startIndex; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]));
        }

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
        Reset();
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var dwma = new Dwma(period);
        return dwma.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double[]? tempArray = len > 1024 ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> temp = len <= 1024
            ? stackalloc double[len]
            : tempArray!.AsSpan(0, len);

        try
        {
            Wma.Batch(source, temp, period);
            Wma.Batch(temp, output, period);
        }
        finally
        {
            if (tempArray != null)
            {
                ArrayPool<double>.Shared.Return(tempArray);
            }
        }
    }

    public static (TSeries Results, Dwma Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Dwma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _wma1.Reset();
        _wma2.Reset();
        _sampleCount = 0;
        Last = default;
    }
}