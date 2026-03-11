using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// AC: Accelerator Oscillator
/// </summary>
/// <remarks>
/// Bill Williams' Acceleration Oscillator measures the acceleration or deceleration
/// of the current market driving force. AC is the second derivative of price momentum:
///
/// Median Price = (High + Low) / 2
/// AO = SMA(Median Price, fastPeriod) - SMA(Median Price, slowPeriod)
/// AC = AO - SMA(AO, acPeriod)
///
/// Design note: Ac implements <see cref="ITValuePublisher"/> directly rather than inheriting
/// from AbstractBase. This is intentional: Ac is an OHLC-based indicator whose primary input
/// is a <see cref="TBar"/> (requiring High and Low), not a single <see cref="TValue"/>.
/// AbstractBase's contract (Update(TValue), Prime(ReadOnlySpan&lt;double&gt;)) does not fit
/// OHLC indicators. The practical entry points are Update(TBar) and Prime(TBarSeries).
/// If a future TBarIndicatorBase is introduced, Ac would be a candidate to migrate.
///
/// Sources:
/// https://www.investopedia.com/terms/a/accelerationdeceleration-indicator.asp
/// https://www.tradingview.com/support/solutions/43000501837-accelerator-oscillator-ac/
/// </remarks>
[SkipLocalsInit]
public sealed class Ac : ITValuePublisher
{
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
    private readonly int _acPeriod;
    private readonly Sma _smaFast;
    private readonly Sma _smaSlow;
    private readonly Sma _smaAc;

    private TValue _p_Last;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>Current AC value.</summary>
    public TValue Last { get; private set; }

    /// <summary>True if the AC has enough data to produce valid results.</summary>
    public bool IsHot => _smaAc.IsHot;

    /// <summary>The number of bars required to warm up the indicator.</summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates AC with specified periods.
    /// </summary>
    /// <param name="fastPeriod">Fast SMA period for AO calculation (default 5)</param>
    /// <param name="slowPeriod">Slow SMA period for AO calculation (default 34)</param>
    /// <param name="acPeriod">SMA period applied to AO for AC calculation (default 5)</param>
    public Ac(int fastPeriod = 5, int slowPeriod = 34, int acPeriod = 5)
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

        if (acPeriod <= 0)
        {
            throw new ArgumentException("AC period must be greater than 0", nameof(acPeriod));
        }

        _fastPeriod = fastPeriod;
        _slowPeriod = slowPeriod;
        _acPeriod = acPeriod;

        _smaFast = new Sma(fastPeriod);
        _smaSlow = new Sma(slowPeriod);
        _smaAc = new Sma(acPeriod);
        WarmupPeriod = slowPeriod + acPeriod - 1;
        Name = $"Ac({fastPeriod},{slowPeriod},{acPeriod})";
    }

    /// <summary>Resets the AC state.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _smaFast.Reset();
        _smaSlow.Reset();
        _smaAc.Reset();
        Last = default;
        _p_Last = default;
    }

    /// <summary>
    /// Updates the AC with a new bar.
    /// </summary>
    /// <param name="input">The new bar data</param>
    /// <param name="isNew">Whether this is a new bar or an update to the last bar</param>
    /// <returns>The updated AC value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (!double.IsFinite(input.High) || !double.IsFinite(input.Low))
        {
            Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = false });
            return Last;
        }

        double medianPrice = (input.High + input.Low) * 0.5;
        var val = new TValue(input.Time, medianPrice);

        if (isNew)
        {
            _p_Last = Last;
        }
        else
        {
            Last = _p_Last;
        }

        var sFast = _smaFast.Update(val, isNew);
        var sSlow = _smaSlow.Update(val, isNew);

        double ao = sFast.Value - sSlow.Value;
        var aoVal = new TValue(input.Time, ao);

        var sAc = _smaAc.Update(aoVal, isNew);
        double ac = ao - sAc.Value;

        Last = new TValue(input.Time, ac);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the AC with a new value (assumes value is Median Price).
    /// </summary>
    /// <param name="input">The new value</param>
    /// <param name="isNew">Whether this is a new value or an update to the last value</param>
    /// <returns>The updated AC value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (!double.IsFinite(input.Value))
        {
            Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = false });
            return Last;
        }

        if (isNew)
        {
            _p_Last = Last;
        }
        else
        {
            Last = _p_Last;
        }

        var sFast = _smaFast.Update(input, isNew);
        var sSlow = _smaSlow.Update(input, isNew);

        double ao = sFast.Value - sSlow.Value;
        var aoVal = new TValue(input.Time, ao);

        var sAc = _smaAc.Update(aoVal, isNew);
        double ac = ao - sAc.Value;

        Last = new TValue(input.Time, ac);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the AC with a series of bars.
    /// </summary>
    /// <param name="source">The source series of bars</param>
    /// <returns>The AC series</returns>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var v = new double[len];

        Batch(source.High.Values, source.Low.Values, v, _fastPeriod, _slowPeriod, _acPeriod);

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
    /// Calculates AC over OHLC spans into a preallocated output span.
    /// Median price is computed as (High + Low) / 2.
    /// </summary>
    /// <param name="high">High prices</param>
    /// <param name="low">Low prices</param>
    /// <param name="destination">Output AC values</param>
    /// <param name="fastPeriod">Fast SMA period (default 5)</param>
    /// <param name="slowPeriod">Slow SMA period (default 34)</param>
    /// <param name="acPeriod">AC SMA period (default 5)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> destination, int fastPeriod = 5, int slowPeriod = 34, int acPeriod = 5)
    {
        if (fastPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fastPeriod), "Fast period must be greater than 0.");
        }

        if (slowPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slowPeriod), "Slow period must be greater than 0.");
        }

        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException("Fast period must be less than slow period.", nameof(fastPeriod));
        }

        if (acPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(acPeriod), "AC period must be greater than 0.");
        }

        if (high.Length != low.Length || high.Length != destination.Length)
        {
            throw new ArgumentException("High, low, and destination spans must have the same length.", nameof(destination));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // Rent buffers: median + fast + slow + ao = 4 * len
        double[] rentedBuffer = ArrayPool<double>.Shared.Rent(len * 4);
        try
        {
            Span<double> median = rentedBuffer.AsSpan(0, len);
            Span<double> fast = rentedBuffer.AsSpan(len, len);
            Span<double> slow = rentedBuffer.AsSpan(len * 2, len);
            Span<double> ao = rentedBuffer.AsSpan(len * 3, len);

            for (int i = 0; i < len; i++)
            {
                median[i] = (high[i] + low[i]) * 0.5;
            }

            Sma.Batch(median, fast, fastPeriod);
            Sma.Batch(median, slow, slowPeriod);

            // AO = fast - slow
            SimdExtensions.Subtract(fast, slow, ao);

            // AC = AO - SMA(AO, acPeriod)
            Sma.Batch(ao, destination, acPeriod);
            SimdExtensions.Subtract(ao, destination, destination);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Calculates AC for the entire series using a stateless batch path.
    /// </summary>
    /// <param name="source">Input bar series</param>
    /// <param name="fastPeriod">Fast SMA period (default 5)</param>
    /// <param name="slowPeriod">Slow SMA period (default 34)</param>
    /// <param name="acPeriod">AC SMA period (default 5)</param>
    /// <returns>AC series</returns>
    public static TSeries Batch(TBarSeries source, int fastPeriod = 5, int slowPeriod = 34, int acPeriod = 5)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var v = new double[len];

        Batch(source.High.Values, source.Low.Values, v, fastPeriod, slowPeriod, acPeriod);

        var tList = new List<long>(len);
        CollectionsMarshal.SetCount(tList, len);
        var tSpan = CollectionsMarshal.AsSpan(tList);
        source.Open.Times.CopyTo(tSpan);

        var vList = new List<double>(len);
        CollectionsMarshal.SetCount(vList, len);
        var vSpan = CollectionsMarshal.AsSpan(vList);
        v.AsSpan().CopyTo(vSpan);

        return new TSeries(tList, vList);
    }

    public static (TSeries Results, Ac Indicator) Calculate(TBarSeries source, int fastPeriod = 5, int slowPeriod = 34, int acPeriod = 5)
    {
        var indicator = new Ac(fastPeriod, slowPeriod, acPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
