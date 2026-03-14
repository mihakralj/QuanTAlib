using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VEL: Jurik Velocity
/// </summary>
/// <remarks>
/// Momentum oscillator measuring smoothed rate of price change using differential weighting.
/// Compares parabolic vs linear weight distributions for trend sensitivity.
///
/// Calculation: <c>VEL = PWMA(Period) - WMA(Period)</c>.
/// </remarks>
/// <seealso href="Vel.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Vel : ITValuePublisher, IDisposable
{
    private const int StackallocThreshold = 256;

    private readonly Pwma _pwma;
    private readonly Wma _wma;
    private readonly int _period;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _publisher;
    private bool _disposed;

    public string Name { get; }
    public TValue Last { get; private set; }
    public bool IsHot => _pwma.IsHot && _wma.IsHot;
    public int WarmupPeriod { get; }
    public event TValuePublishedHandler? Pub;

    public Vel(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _pwma = new Pwma(period);
        _wma = new Wma(period);
        _period = period;
        WarmupPeriod = period;
        Name = $"Vel({period})";
        _handler = Handle;
    }

    public Vel(ITValuePublisher source, int period) : this(period)
    {
        _publisher = source;
        source.Pub += _handler;
    }

    /// <summary>
    /// Unsubscribes from the source publisher and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_publisher != null)
        {
            _publisher.Pub -= _handler;
        }

        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        var pwma = _pwma.Update(input, isNew);
        var wma = _wma.Update(input, isNew);

        Last = new TValue(input.Time, pwma.Value - wma.Value);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        int len = source.Count;
        if (len == 0)
        {
            return [];
        }

        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        // Span-based batch calculation
        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Restore streaming state by replaying the tail of the series
        Reset();
        int start = Math.Max(0, len - WarmupPeriod - 1);
        for (int i = start; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <summary>
    /// Initializes the indicator state using the provided series history.
    /// </summary>
    /// <param name="source">Historical data.</param>
    public void Prime(TSeries source)
    {
        Reset();
        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(new TValue(new DateTime(source.Times[i], DateTimeKind.Utc), source.Values[i]), isNew: true);
        }
    }

    public static TSeries Batch(TSeries source, int period)
    {
        int len = source.Count;
        if (len == 0)
        {
            return [];
        }

        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, period);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        int len = source.Length;

        if (len <= StackallocThreshold)
        {
            BatchStackalloc(source, output, period, len);
        }
        else
        {
            BatchPooled(source, output, period, len);
        }
    }

    public static (TSeries Results, Vel Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Vel(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BatchStackalloc(ReadOnlySpan<double> source, Span<double> output, int period, int len)
    {
        Span<double> pwma = stackalloc double[len];
        Span<double> wma = stackalloc double[len];

        Pwma.Batch(source, pwma, period);
        Wma.Batch(source, wma, period);
        SimdExtensions.Subtract(pwma, wma, output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BatchPooled(ReadOnlySpan<double> source, Span<double> output, int period, int len)
    {
        double[] rentedPwma = ArrayPool<double>.Shared.Rent(len);
        double[] rentedWma = ArrayPool<double>.Shared.Rent(len);

        try
        {
            Span<double> pwma = rentedPwma.AsSpan(0, len);
            Span<double> wma = rentedWma.AsSpan(0, len);

            Pwma.Batch(source, pwma, period);
            Wma.Batch(source, wma, period);
            SimdExtensions.Subtract(pwma, wma, output);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rentedPwma);
            ArrayPool<double>.Shared.Return(rentedWma);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _pwma.Reset();
        _wma.Reset();
        Last = default;
    }
}