using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// AO: Awesome Oscillator
/// </summary>
/// <remarks>
/// The Awesome Oscillator (AO) is a momentum indicator used to measure market momentum.
/// It calculates the difference between a 5-period and 34-period Simple Moving Average (SMA)
/// of the median prices (High + Low) / 2.
///
/// Calculation:
/// Median Price = (High + Low) / 2
/// AO = SMA(Median Price, 5) - SMA(Median Price, 34)
///
/// Sources:
/// https://www.investopedia.com/terms/a/awesomeoscillator.asp
/// https://www.tradingview.com/support/solutions/43000501826-awesome-oscillator-ao/
/// </remarks>
[SkipLocalsInit]
public sealed class Ao : ITValuePublisher
{
    private readonly Sma _smaFast;
    private readonly Sma _smaSlow;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event Action<TValue>? Pub;

    /// <summary>
    /// Current AO value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the AO has enough data to produce valid results.
    /// </summary>
    public bool IsHot => _smaSlow.IsHot;

    /// <summary>
    /// The number of bars required to warm up the indicator.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates AO with specified periods.
    /// </summary>
    /// <param name="fastPeriod">Fast SMA period (default 5)</param>
    /// <param name="slowPeriod">Slow SMA period (default 34)</param>
    public Ao(int fastPeriod = 5, int slowPeriod = 34)
    {
        if (fastPeriod <= 0)
            throw new ArgumentException("Fast period must be greater than 0", nameof(fastPeriod));
        if (slowPeriod <= 0)
            throw new ArgumentException("Slow period must be greater than 0", nameof(slowPeriod));
        if (fastPeriod >= slowPeriod)
            throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));

        _smaFast = new Sma(fastPeriod);
        _smaSlow = new Sma(slowPeriod);
        WarmupPeriod = slowPeriod;
        Name = $"Ao({fastPeriod},{slowPeriod})";
    }

    /// <summary>
    /// Resets the AO state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _smaFast.Reset();
        _smaSlow.Reset();
        Last = default;
    }

    /// <summary>
    /// Updates the AO with a new bar.
    /// </summary>
    /// <param name="input">The new bar data</param>
    /// <param name="isNew">Whether this is a new bar or an update to the last bar</param>
    /// <returns>The updated AO value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        double medianPrice = (input.High + input.Low) * 0.5;
        var val = new TValue(input.Time, medianPrice);

        var sFast = _smaFast.Update(val, isNew);
        var sSlow = _smaSlow.Update(val, isNew);

        double ao = sFast.Value - sSlow.Value;
        Last = new TValue(input.Time, ao);
        Pub?.Invoke(Last);
        return Last;
    }

    /// <summary>
    /// Updates the AO with a new value (assumes value is Median Price).
    /// </summary>
    /// <param name="input">The new value</param>
    /// <param name="isNew">Whether this is a new value or an update to the last value</param>
    /// <returns>The updated AO value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        var sFast = _smaFast.Update(input, isNew);
        var sSlow = _smaSlow.Update(input, isNew);

        double ao = sFast.Value - sSlow.Value;
        Last = new TValue(input.Time, ao);
        Pub?.Invoke(Last);
        return Last;
    }

    /// <summary>
    /// Updates the AO with a series of bars.
    /// </summary>
    /// <param name="source">The source series of bars</param>
    /// <returns>The AO series</returns>
    public TSeries Update(TBarSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates AO for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="fastPeriod">Fast SMA period (default 5)</param>
    /// <param name="slowPeriod">Slow SMA period (default 34)</param>
    /// <returns>AO series</returns>
    public static TSeries Batch(TBarSeries source, int fastPeriod = 5, int slowPeriod = 34)
    {
        var ao = new Ao(fastPeriod, slowPeriod);
        return ao.Update(source);
    }
}
