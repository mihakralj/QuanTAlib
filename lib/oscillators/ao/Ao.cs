using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
/// Design note: Ao implements <see cref="ITValuePublisher"/> directly rather than inheriting
/// from AbstractBase. This is intentional: Ao is an OHLC-based indicator whose primary input
/// is a <see cref="TBar"/> (requiring High and Low), not a single <see cref="TValue"/>.
/// AbstractBase's contract (Update(TValue), Prime(ReadOnlySpan&lt;double&gt;)) does not fit
/// OHLC indicators. The practical entry points are Update(TBar) and Prime(TBarSeries).
/// If a future TBarIndicatorBase is introduced, Ao would be a candidate to migrate.
///
/// Sources:
/// https://www.investopedia.com/terms/a/awesomeoscillator.asp
/// https://www.tradingview.com/support/solutions/43000501826-awesome-oscillator-ao/
/// </remarks>
[SkipLocalsInit]
public sealed class Ao : ITValuePublisher
{
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
    private readonly Sma _smaFast;
    private readonly Sma _smaSlow;

    private TValue _p_Last;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

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
        {
            throw new ArgumentException("Fast period must be greater than 0", nameof(fastPeriod));
        }

        if (slowPeriod <= 0)
        {
            throw new ArgumentException("Slow period must be greater than 0", nameof(slowPeriod));
        }

        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));
        }

        _fastPeriod = fastPeriod;
        _slowPeriod = slowPeriod;

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
        _p_Last = default;
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

        // Save state for potential rollback
        if (isNew)
        {
            _p_Last = Last;
        }
        else
        {
            // Rollback to previous state - SMAs handle their own rollback
            Last = _p_Last;
        }

        var sFast = _smaFast.Update(val, isNew);
        var sSlow = _smaSlow.Update(val, isNew);

        double ao = sFast.Value - sSlow.Value;
        Last = new TValue(input.Time, ao);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
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
        // Guard against non-finite input
        if (!double.IsFinite(input.Value))
        {
            // Keep Last unchanged, publish with IsNew=false to indicate no state change
            Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = false });
            return Last;
        }

        // Save state for potential rollback
        if (isNew)
        {
            _p_Last = Last;
        }
        else
        {
            // Rollback to previous state - SMAs handle their own rollback
            Last = _p_Last;
        }

        var sFast = _smaFast.Update(input, isNew);
        var sSlow = _smaSlow.Update(input, isNew);

        double ao = sFast.Value - sSlow.Value;
        Last = new TValue(input.Time, ao);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the AO with a series of bars.
    /// </summary>
    /// <param name="source">The source series of bars</param>
    /// <returns>The AO series</returns>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var v = new double[len];

        Batch(source.High.Values, source.Low.Values, v, _fastPeriod, _slowPeriod);

        // Bulk copy timestamps using CollectionsMarshal
        var tList = new List<long>(len);
        CollectionsMarshal.SetCount(tList, len);
        var tSpan = CollectionsMarshal.AsSpan(tList);
        source.Open.Times.CopyTo(tSpan);

        var vList = new List<double>(len);
        CollectionsMarshal.SetCount(vList, len);
        var vSpan = CollectionsMarshal.AsSpan(vList);
        v.AsSpan().CopyTo(vSpan);

        // Restore streaming state so the instance is hot after batch update
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(tList, vList);
    }

    /// <summary>
    /// Initializes the indicator state using the provided bar series history.
    /// </summary>
    /// <param name="source">Historical bar data.</param>
    public void Prime(TBarSeries source)
    {
        Reset();
        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    /// <summary>
    /// Calculates AO over OHLC spans into a preallocated output span.
    /// Median price is computed as (High + Low) / 2.
    /// </summary>
    /// <param name="high">High prices</param>
    /// <param name="low">Low prices</param>
    /// <param name="fastPeriod">Fast SMA period (default 5)</param>
    /// <param name="slowPeriod">Slow SMA period (default 34)</param>
    /// <param name="destination">Output AO values</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> destination, int fastPeriod = 5, int slowPeriod = 34)
    {
        if (high.Length != low.Length || high.Length != destination.Length)
        {
            throw new ArgumentException("High, low, and destination spans must have the same length.", nameof(destination));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // Always use pooled buffer to avoid CS8353 stackalloc escape issues
        // For small sizes, ArrayPool overhead is minimal
        double[] rentedBuffer = ArrayPool<double>.Shared.Rent(len * 3);
        try
        {
            Span<double> median = rentedBuffer.AsSpan(0, len);
            Span<double> fast = rentedBuffer.AsSpan(len, len);
            Span<double> slow = rentedBuffer.AsSpan(len * 2, len);

            for (int i = 0; i < len; i++)
            {
                median[i] = (high[i] + low[i]) * 0.5;
            }

            Sma.Batch(median, fast, fastPeriod);
            Sma.Batch(median, slow, slowPeriod);

            SimdExtensions.Subtract(fast, slow, destination);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Calculates AO for the entire series using a stateless batch path.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="fastPeriod">Fast SMA period (default 5)</param>
    /// <param name="slowPeriod">Slow SMA period (default 34)</param>
    /// <returns>AO series</returns>
    public static TSeries Batch(TBarSeries source, int fastPeriod = 5, int slowPeriod = 34)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var v = new double[len];

        Batch(source.High.Values, source.Low.Values, v, fastPeriod, slowPeriod);

        // Bulk copy timestamps using CollectionsMarshal
        var tList = new List<long>(len);
        CollectionsMarshal.SetCount(tList, len);
        var tSpan = CollectionsMarshal.AsSpan(tList);
        source.Open.Times.CopyTo(tSpan);

        // Pass values list directly, avoiding spread operator allocation
        var vList = new List<double>(len);
        CollectionsMarshal.SetCount(vList, len);
        var vSpan = CollectionsMarshal.AsSpan(vList);
        v.AsSpan().CopyTo(vSpan);

        return new TSeries(tList, vList);
    }

    public static (TSeries Results, Ao Indicator) Calculate(TBarSeries source, int fastPeriod = 5, int slowPeriod = 34)
    {
        var indicator = new Ao(fastPeriod, slowPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
