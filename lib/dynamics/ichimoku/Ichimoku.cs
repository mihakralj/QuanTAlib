// ICHIMOKU: Ichimoku Kinko Hyo (One Glance Equilibrium Chart)
// A comprehensive trend-following indicator system with five components.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ICHIMOKU: Ichimoku Kinko Hyo (One Glance Equilibrium Chart)
/// </summary>
/// <remarks>
/// The Ichimoku Cloud is a multi-functional indicator developed by Japanese journalist
/// Goichi Hosoda, published in 1969. It provides support/resistance levels, trend direction,
/// momentum, and trading signals in a single view.
///
/// Five Components:
/// 1. Tenkan-sen (Conversion Line): (9-period high + 9-period low) / 2
///    - Short-term equilibrium, similar to fast MA
///    - Indicates short-term trend direction
///
/// 2. Kijun-sen (Base Line): (26-period high + 26-period low) / 2
///    - Medium-term equilibrium, similar to slow MA
///    - Key support/resistance level, used for stop-loss placement
///
/// 3. Senkou Span A (Leading Span A): (Tenkan-sen + Kijun-sen) / 2, plotted 26 periods ahead
///    - First boundary of the cloud (Kumo)
///    - Average of short and medium equilibrium
///
/// 4. Senkou Span B (Leading Span B): (52-period high + 52-period low) / 2, plotted 26 periods ahead
///    - Second boundary of the cloud (Kumo)
///    - Long-term equilibrium, usually flatter than Span A
///
/// 5. Chikou Span (Lagging Span): Current close plotted 26 periods behind
///    - Confirms trend by comparing current price to past
///
/// Cloud (Kumo): The area between Senkou Span A and B
/// - Provides key support/resistance zones
/// - Green cloud (A above B) = bullish
/// - Red cloud (B above A) = bearish
/// - Cloud thickness indicates strength of support/resistance
///
/// Default Parameters:
/// - Tenkan period: 9 (conversion line, short-term)
/// - Kijun period: 26 (base line, medium-term)
/// - Senkou B period: 52 (leading span B, long-term)
/// - Displacement: 26 (forward/backward shift for spans)
///
/// Sources:
///     Goichi Hosoda, "Ichimoku Kinko Hyo" (1969)
///     https://school.stockcharts.com/doku.php?id=technical_indicators:ichimoku_cloud
///     https://www.investopedia.com/terms/i/ichimoku-cloud.asp
/// </remarks>
/// <seealso href="ichimoku.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Ichimoku : ITValuePublisher
{
    private readonly int _tenkanPeriod;
    private readonly int _kijunPeriod;
    private readonly int _senkouBPeriod;
    private readonly int _displacement;

    // Ring buffers for high/low tracking
    private readonly double[] _highBuffer;
    private readonly double[] _lowBuffer;
    private readonly double[] _p_highBuffer;
    private readonly double[] _p_lowBuffer;

    // State tracking
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        int Head,
        int Count,
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose,
        bool IsHot);

    private State _state;
    private State _p_state;

    public string Name { get; }
    public int WarmupPeriod { get; }

    /// <summary>
    /// Tenkan-sen (Conversion Line): Short-term equilibrium.
    /// Calculated as (9-period high + 9-period low) / 2.
    /// </summary>
    public TValue Tenkan { get; private set; }

    /// <summary>
    /// Kijun-sen (Base Line): Medium-term equilibrium.
    /// Calculated as (26-period high + 26-period low) / 2.
    /// Key support/resistance level.
    /// </summary>
    public TValue Kijun { get; private set; }

    /// <summary>
    /// Senkou Span A (Leading Span A): First cloud boundary.
    /// Calculated as (Tenkan + Kijun) / 2.
    /// Note: This is the current value; displacement to future is applied in charting.
    /// </summary>
    public TValue SenkouA { get; private set; }

    /// <summary>
    /// Senkou Span B (Leading Span B): Second cloud boundary.
    /// Calculated as (52-period high + 52-period low) / 2.
    /// Note: This is the current value; displacement to future is applied in charting.
    /// </summary>
    public TValue SenkouB { get; private set; }

    /// <summary>
    /// Chikou Span (Lagging Span): Current close value.
    /// Note: This value is plotted 26 periods behind in charting.
    /// </summary>
    public TValue Chikou { get; private set; }

    /// <summary>
    /// Primary output (Kijun-sen) for compatibility.
    /// Kijun is often used as the main trend reference.
    /// </summary>
    public TValue Last => Kijun;

    /// <summary>
    /// True when all components have sufficient data.
    /// </summary>
    public bool IsHot => _state.IsHot;

    /// <summary>
    /// The displacement period for Senkou Spans and Chikou Span.
    /// </summary>
    public int Displacement => _displacement;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates an Ichimoku Cloud indicator with default parameters.
    /// Default: Tenkan=9, Kijun=26, Senkou B=52, Displacement=26.
    /// </summary>
    public Ichimoku() : this(9, 26, 52, 26)
    {
    }

    /// <summary>
    /// Creates an Ichimoku Cloud indicator with specified parameters.
    /// </summary>
    /// <param name="tenkanPeriod">Period for Tenkan-sen (Conversion Line), typically 9</param>
    /// <param name="kijunPeriod">Period for Kijun-sen (Base Line), typically 26</param>
    /// <param name="senkouBPeriod">Period for Senkou Span B (Leading Span B), typically 52</param>
    /// <param name="displacement">Forward/backward shift for Senkou/Chikou spans, typically 26</param>
    public Ichimoku(int tenkanPeriod, int kijunPeriod, int senkouBPeriod, int displacement)
    {
        if (tenkanPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tenkanPeriod), "Tenkan period must be greater than 0");
        }
        if (kijunPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(kijunPeriod), "Kijun period must be greater than 0");
        }
        if (senkouBPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(senkouBPeriod), "Senkou B period must be greater than 0");
        }
        if (displacement <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displacement), "Displacement must be greater than 0");
        }

        _tenkanPeriod = tenkanPeriod;
        _kijunPeriod = kijunPeriod;
        _senkouBPeriod = senkouBPeriod;
        _displacement = displacement;

        int maxPeriod = Math.Max(Math.Max(tenkanPeriod, kijunPeriod), senkouBPeriod);
        _highBuffer = new double[maxPeriod];
        _lowBuffer = new double[maxPeriod];
        _p_highBuffer = new double[maxPeriod];
        _p_lowBuffer = new double[maxPeriod];

        WarmupPeriod = maxPeriod;
        Name = $"Ichimoku({tenkanPeriod},{kijunPeriod},{senkouBPeriod},{displacement})";

        Reset();
    }

    /// <summary>
    /// Creates an Ichimoku Cloud indicator and primes it with a source series.
    /// </summary>
    public Ichimoku(TBarSeries source, int tenkanPeriod = 9, int kijunPeriod = 26,
                    int senkouBPeriod = 52, int displacement = 26)
        : this(tenkanPeriod, kijunPeriod, senkouBPeriod, displacement)
    {
        Prime(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _state = new State(0, 0, double.NaN, double.NaN, double.NaN, false);
        _p_state = _state;
        Array.Fill(_highBuffer, double.NaN);
        Array.Fill(_lowBuffer, double.NaN);
        Array.Copy(_highBuffer, _p_highBuffer!, _highBuffer.Length);
        Array.Copy(_lowBuffer, _p_lowBuffer!, _lowBuffer.Length);
        Tenkan = default;
        Kijun = default;
        SenkouA = default;
        SenkouB = default;
        Chikou = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double high, double low, double close) GetValidHLC(TBar bar)
    {
        double high = bar.High;
        double low = bar.Low;
        double close = bar.Close;

        if (double.IsFinite(high))
        {
            _state = _state with { LastValidHigh = high };
        }
        else
        {
            high = double.IsFinite(_state.LastValidHigh) ? _state.LastValidHigh : 0.0;
        }

        if (double.IsFinite(low))
        {
            _state = _state with { LastValidLow = low };
        }
        else
        {
            low = double.IsFinite(_state.LastValidLow) ? _state.LastValidLow : 0.0;
        }

        if (double.IsFinite(close))
        {
            _state = _state with { LastValidClose = close };
        }
        else
        {
            close = double.IsFinite(_state.LastValidClose) ? _state.LastValidClose : 0.0;
        }

        return (high, low, close);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double high, double low) GetDonchianMidpoint(int period)
    {
        int count = Math.Min(_state.Count, period);
        if (count == 0)
        {
            return (double.NaN, double.NaN);
        }

        double highest = double.MinValue;
        double lowest = double.MaxValue;

        int head = _state.Head;
        int bufLen = _highBuffer.Length;

        for (int i = 0; i < count; i++)
        {
            int idx = (head - 1 - i + bufLen) % bufLen;
            double h = _highBuffer[idx];
            double l = _lowBuffer[idx];

            if (double.IsFinite(h) && h > highest)
            {
                highest = h;
            }
            if (double.IsFinite(l) && l < lowest)
            {
                lowest = l;
            }
        }

        return (highest, lowest);
    }

    /// <summary>
    /// Updates the indicator with a new price bar.
    /// </summary>
    /// <param name="bar">Price bar with High, Low, Close</param>
    /// <param name="isNew">True for new bar, false for bar update/correction</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            Array.Copy(_highBuffer, _p_highBuffer!, _highBuffer.Length);
            Array.Copy(_lowBuffer, _p_lowBuffer!, _lowBuffer.Length);
        }
        else
        {
            _state = _p_state;
            Array.Copy(_p_highBuffer!, _highBuffer, _highBuffer.Length);
            Array.Copy(_p_lowBuffer!, _lowBuffer, _lowBuffer.Length);
        }

        var (high, low, close) = GetValidHLC(bar);

        // Add to ring buffer
        int head = _state.Head;
        _highBuffer[head] = high;
        _lowBuffer[head] = low;

        int newHead = (head + 1) % _highBuffer.Length;
        int newCount = Math.Min(_state.Count + 1, _highBuffer.Length);

        _state = _state with { Head = newHead, Count = newCount };

        // Calculate Tenkan-sen (9-period)
        var (tenkanHigh, tenkanLow) = GetDonchianMidpoint(_tenkanPeriod);
        double tenkanValue = (tenkanHigh + tenkanLow) / 2.0;

        // Calculate Kijun-sen (26-period)
        var (kijunHigh, kijunLow) = GetDonchianMidpoint(_kijunPeriod);
        double kijunValue = (kijunHigh + kijunLow) / 2.0;

        // Calculate Senkou Span A: (Tenkan + Kijun) / 2
        double senkouAValue = (tenkanValue + kijunValue) / 2.0;

        // Calculate Senkou Span B (52-period)
        var (senkouBHigh, senkouBLow) = GetDonchianMidpoint(_senkouBPeriod);
        double senkouBValue = (senkouBHigh + senkouBLow) / 2.0;

        // Chikou Span is just the current close (plotted backwards in charting)
        double chikouValue = close;

        // Check if warmed up
        if (!_state.IsHot && _state.Count >= WarmupPeriod)
        {
            _state = _state with { IsHot = true };
        }

        // Set outputs
        Tenkan = new TValue(bar.Time, tenkanValue);
        Kijun = new TValue(bar.Time, kijunValue);
        SenkouA = new TValue(bar.Time, senkouAValue);
        SenkouB = new TValue(bar.Time, senkouBValue);
        Chikou = new TValue(bar.Time, chikouValue);

        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a single value (uses value as high, low, and close).
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True for new bar, false for bar update/correction</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        // Treat single value as H=L=C
        var bar = new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0);
        return Update(bar, isNew);
    }

    /// <summary>
    /// Processes a TBarSeries and returns tuple of all component series.
    /// </summary>
    public (TSeries Tenkan, TSeries Kijun, TSeries SenkouA, TSeries SenkouB, TSeries Chikou) Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []), new TSeries([], []),
                    new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tList = new List<long>(len);
        var tenkanList = new List<double>(len);
        var kijunList = new List<double>(len);
        var senkouAList = new List<double>(len);
        var senkouBList = new List<double>(len);
        var chikouList = new List<double>(len);

        for (int i = 0; i < len; i++)
        {
            var bar = source[i];
            Update(bar, isNew: true);
            tList.Add(bar.Time);
            tenkanList.Add(Tenkan.Value);
            kijunList.Add(Kijun.Value);
            senkouAList.Add(SenkouA.Value);
            senkouBList.Add(SenkouB.Value);
            chikouList.Add(Chikou.Value);
        }

        return (
            new TSeries(tList, tenkanList),
            new TSeries(tList, kijunList),
            new TSeries(tList, senkouAList),
            new TSeries(tList, senkouBList),
            new TSeries(tList, chikouList)
        );
    }

    /// <summary>
    /// Primes the indicator with historical bar data.
    /// </summary>
    public void Prime(TBarSeries source)
    {
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    /// <summary>
    /// Primes the indicator with historical value data.
    /// </summary>
    public void Prime(TSeries source)
    {
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    /// <summary>
    /// Calculates Ichimoku for the entire bar series using default parameters.
    /// </summary>
    public static (TSeries Tenkan, TSeries Kijun, TSeries SenkouA, TSeries SenkouB, TSeries Chikou) Batch(TBarSeries source)
    {
        var ichimoku = new Ichimoku();
        return ichimoku.Update(source);
    }

    /// <summary>
    /// Calculates Ichimoku for the entire bar series using custom parameters.
    /// </summary>
    public static (TSeries Tenkan, TSeries Kijun, TSeries SenkouA, TSeries SenkouB, TSeries Chikou) Batch(
        TBarSeries source, int tenkanPeriod, int kijunPeriod, int senkouBPeriod, int displacement)
    {
        var ichimoku = new Ichimoku(tenkanPeriod, kijunPeriod, senkouBPeriod, displacement);
        return ichimoku.Update(source);
    }

    /// <summary>
    /// Calculates Ichimoku and returns both results and the warm indicator.
    /// </summary>
    public static ((TSeries Tenkan, TSeries Kijun, TSeries SenkouA, TSeries SenkouB, TSeries Chikou) Results, Ichimoku Indicator)
        Calculate(TBarSeries source, int tenkanPeriod = 9, int kijunPeriod = 26, int senkouBPeriod = 52, int displacement = 26)
    {
        var ichimoku = new Ichimoku(tenkanPeriod, kijunPeriod, senkouBPeriod, displacement);
        var results = ichimoku.Update(source);
        return (results, ichimoku);
    }

    /// <summary>
    /// Gets the Tenkan-sen period.
    /// </summary>
    public int TenkanPeriod => _tenkanPeriod;

    /// <summary>
    /// Gets the Kijun-sen period.
    /// </summary>
    public int KijunPeriod => _kijunPeriod;

    /// <summary>
    /// Gets the Senkou Span B period.
    /// </summary>
    public int SenkouBPeriod => _senkouBPeriod;
}
