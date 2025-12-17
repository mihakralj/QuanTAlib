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

    public string Name { get; }
    public TValue Last { get; private set; }
    public bool IsHot => _pwma.IsHot && _wma.IsHot;
    public int WarmupPeriod { get; }
    public event Action<TValue>? Pub;

    public Vel(int period)
    {
        if (period <= 0) throw new ArgumentException("Period must be greater than 0", nameof(period));

        _pwma = new Pwma(period);
        _wma = new Wma(period);
        WarmupPeriod = period;
        Name = $"Vel({period})";
    }

    public Vel(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += (item) => Update(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        var pwma = _pwma.Update(input, isNew);
        var wma = _wma.Update(input, isNew);

        Last = new TValue(input.Time, pwma.Value - wma.Value);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        // Update internal indicators to ensure their state is correct
        var pwmaSeries = _pwma.Update(source);
        var wmaSeries = _wma.Update(source);

        // Calculate VEL series
        int len = source.Count;
        List<long> t = new(len);
        List<double> v = new(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var vSpan = CollectionsMarshal.AsSpan(v);

        SimdExtensions.Subtract(pwmaSeries.Values, wmaSeries.Values, vSpan);
        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        Last = new TValue(t[len - 1], v[len - 1]);
        return new TSeries(t, v);
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var vel = new Vel(period);
        return vel.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");

        Span<double> pwma = source.Length <= 1024 ? stackalloc double[source.Length] : new double[source.Length];
        Span<double> wma = source.Length <= 1024 ? stackalloc double[source.Length] : new double[source.Length];

        Pwma.Calculate(source, pwma, period);
        Wma.Batch(source, wma, period);

        SimdExtensions.Subtract(pwma, wma, output);
    }

    public void Reset()
    {
        _pwma.Reset();
        _wma.Reset();
        Last = default;
    }
}
