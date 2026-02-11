using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TRIMA: Triangular Moving Average
/// </summary>
/// <remarks>
/// Triangular weighting emphasizing the middle via double SMA. O(1) updates.
///
/// Calculation: <c>TRIMA = SMA(SMA(p1), p2)</c> where <c>p1 = (n+1)/2</c>, <c>p2 = n/2+1</c>.
/// </remarks>
/// <seealso href="Trima.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Trima : AbstractBase
{
    private readonly int _period;
    private readonly Sma _sma1;
    private readonly Sma _sma2;
    private readonly TValuePublishedHandler _handler;
    private ITValuePublisher? _publisher;
    private bool _isNew;

    public Trima(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        int p1 = (period + 1) / 2;
        int p2 = period / 2 + 1;

        _sma1 = new Sma(p1);
        _sma2 = new Sma(p2);
        _handler = Handle;

        Name = $"Trima({period})";
        WarmupPeriod = p1 + p2 - 1;
    }

    public Trima(ITValuePublisher source, int period) : this(period)
    {
        _publisher = source;
        source.Pub += _handler;
    }

    protected override void Dispose(bool disposing)
    {
        if (_publisher != null)
        {
            _publisher.Pub -= _handler;
            _publisher = null;
        }
        base.Dispose(disposing);
    }

    public override bool IsHot => _sma1.IsHot && _sma2.IsHot;
    public bool IsNew => _isNew;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        TValue v1 = _sma1.Update(input, isNew);
        TValue v2 = _sma2.Update(v1, isNew);

        Last = v2;
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

        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        _isNew = true; // Ensure _isNew is consistent after batch update
        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        _sma1.Reset();
        _sma2.Reset();

        _sma1.Prime(source);

        // Calculate intermediate SMA series to prime the second SMA
        int p1 = (_period + 1) / 2;
        double[] tempArray = ArrayPool<double>.Shared.Rent(source.Length);
        Span<double> tempSpan = tempArray.AsSpan(0, source.Length);

        try
        {
            Sma.Batch(source, tempSpan, p1);
            _sma2.Prime(tempSpan);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(tempArray);
        }
    }

    public override void Reset()
    {
        _sma1.Reset();
        _sma2.Reset();
        Last = default;
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var trima = new Trima(period);
        return trima.Update(source);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int p1 = (period + 1) / 2;
        int p2 = period / 2 + 1;

        double[] tempArray = ArrayPool<double>.Shared.Rent(source.Length);
        Span<double> tempSpan = tempArray.AsSpan(0, source.Length);

        try
        {
            Sma.Batch(source, tempSpan, p1);
            Sma.Batch(tempSpan, output, p2);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(tempArray);
        }
    }

    public static (TSeries Results, Trima Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Trima(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}