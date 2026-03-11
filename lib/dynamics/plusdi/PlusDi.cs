using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// PLUS_DI: Plus Directional Indicator (Wilder, 1978)
/// </summary>
/// <remarks>
/// Measures upward directional movement as a percentage of true range.
/// Extracted from the DX calculation: +DI = Smoothed(+DM) / Smoothed(TR) × 100.
/// Range: 0 to 100. Higher values indicate stronger upward movement.
/// </remarks>
[SkipLocalsInit]
public sealed class PlusDi : ITValuePublisher
{
    private readonly Dx _dx;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>Current +DI value.</summary>
    public TValue Last { get; private set; }

    /// <summary>True when the indicator has warmed up.</summary>
    public bool IsHot => _dx.IsHot;

    /// <summary>Bars required for warmup.</summary>
    public int WarmupPeriod => _dx.WarmupPeriod;

    /// <summary>The period parameter.</summary>
    public int Period => _dx.Period;

    /// <summary>
    /// Creates PlusDi with specified period.
    /// </summary>
    /// <param name="period">Wilder smoothing period (must be &gt; 0)</param>
    public PlusDi(int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        _dx = new Dx(period);
        Name = $"PlusDi({period})";
    }

    /// <summary>
    /// Creates PlusDi and immediately processes the bar series.
    /// </summary>
    public PlusDi(TBarSeries source, int period = 14) : this(period)
    {
        var result = Batch(source, period);
        Last = result[^1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        _dx.Update(input, isNew);
        Last = _dx.DiPlus;
        if (isNew)
        {
            Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        }
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        // DI requires OHLC data — scalar update not meaningful
        return Last;
    }

    public TSeries Update(TBarSeries source)
    {
        var result = new TSeries(source.Count);
        foreach (var bar in source)
        {
            result.Add(Update(bar));
        }
        return result;
    }

    /// <summary>
    /// Initializes the indicator state using the provided bar series history.
    /// </summary>
    public void Prime(TBarSeries source)
    {
        foreach (var bar in source)
        {
            Update(bar);
        }
    }

    public void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        // Not applicable — DI requires OHLC bar data, not scalar values
    }

    public static TSeries Batch(TBarSeries source, int period = 14)
    {
        var indicator = new PlusDi(period);
        return indicator.Update(source);
    }

    public static (TSeries Results, PlusDi Indicator) Calculate(TBarSeries source, int period = 14)
    {
        var indicator = new PlusDi(period);
        return (indicator.Update(source), indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _dx.Reset();
        Last = default;
    }
}
