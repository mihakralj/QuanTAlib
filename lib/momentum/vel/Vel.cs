using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VEL: Jurik Velocity
/// </summary>
/// <remarks>
/// VEL is a momentum oscillator calculated as the difference between a Parabolic Weighted Moving Average (PWMA)
/// and a Weighted Moving Average (WMA) of the same period.
///
/// Calculation:
/// VEL = PWMA(Period) - WMA(Period)
///
/// This indicator measures the rate of change of the price, smoothed by the difference in weighting schemes.
/// </remarks>
[SkipLocalsInit]
public sealed class Vel : ITValuePublisher
{
    private readonly Pwma _pwma;
    private readonly Wma _wma;
    private readonly int _period;
    private readonly TValuePublishedHandler _handler;

    public string Name { get; }
    public TValue Last { get; private set; }
    public bool IsHot => _pwma.IsHot && _wma.IsHot;
    public int WarmupPeriod { get; }
    public event TValuePublishedHandler? Pub;

    public Vel(int period)
    {
        if (period <= 0) throw new ArgumentException("Period must be greater than 0", nameof(period));

        _pwma = new Pwma(period);
        _wma = new Wma(period);
        _period = period;
        WarmupPeriod = period;
        Name = $"Vel({period})";
        _handler = Handle;
    }

    public Vel(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, TValueEventArgs args) => Update(args.Value, args.IsNew);

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
            return [];

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
            Update(new TValue(source.Times[i], source.Values[i]), true);
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public static TSeries Batch(TSeries source, int period)
    {
        int len = source.Count;
        if (len == 0)
            return [];

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
            throw new ArgumentException("Source and output must have the same length", nameof(output));

        Span<double> pwma = source.Length <= 1024 ? stackalloc double[source.Length] : new double[source.Length];
        Span<double> wma = source.Length <= 1024 ? stackalloc double[source.Length] : new double[source.Length];

        Pwma.Calculate(source, pwma, period);
        Wma.Batch(source, wma, period);

        SimdExtensions.Subtract(pwma, wma, output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _pwma.Reset();
        _wma.Reset();
        Last = default;
    }
}
