using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// APO: Absolute Price Oscillator
/// </summary>
/// <remarks>
/// The Absolute Price Oscillator (APO) is a momentum indicator that shows the difference
/// between two Exponential Moving Averages (EMAs) of a security's price.
///
/// Calculation:
/// APO = FastEMA(Price) - SlowEMA(Price)
///
/// Standard Parameters:
/// Fast Period: 12
/// Slow Period: 26
/// Source: Close price
///
/// Sources:
/// https://www.investopedia.com/terms/a/apo.asp
/// https://school.stockcharts.com/doku.php?id=technical_indicators:price_oscillators_ppo
/// </remarks>
[SkipLocalsInit]
public sealed class Apo : ITValuePublisher, IDisposable
{
    private readonly Ema _emaFast;
    private readonly Ema _emaSlow;
    private readonly TValuePublishedHandler _handler;
    private ITValuePublisher? _source;
    private bool _disposed;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current APO value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the APO has enough data to produce valid results.
    /// </summary>
    public bool IsHot => _emaSlow.IsHot;

    /// <summary>
    /// The number of bars required to warm up the indicator.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates APO with specified periods.
    /// </summary>
    /// <param name="fastPeriod">Fast EMA period (default 12)</param>
    /// <param name="slowPeriod">Slow EMA period (default 26)</param>
    public Apo(int fastPeriod = 12, int slowPeriod = 26)
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

        _emaFast = new Ema(fastPeriod);
        _emaSlow = new Ema(slowPeriod);
        _handler = Handle;
        WarmupPeriod = slowPeriod;
        Name = $"Apo({fastPeriod},{slowPeriod})";
    }

    /// <summary>
    /// Creates APO with specified source and periods.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="fastPeriod">Fast EMA period (default 12)</param>
    /// <param name="slowPeriod">Slow EMA period (default 26)</param>
    public Apo(ITValuePublisher source, int fastPeriod = 12, int slowPeriod = 26) : this(fastPeriod, slowPeriod)
    {
        _source = source;
        _source.Pub += _handler;
    }

    /// <summary>
    /// Resets the APO state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _emaFast.Reset();
        _emaSlow.Reset();
        Last = default;
    }

    /// <summary>
    /// Updates the APO with a new value.
    /// </summary>
    /// <param name="input">The new value</param>
    /// <param name="isNew">Whether this is a new value or an update to the last value</param>
    /// <returns>The updated APO value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        var eFast = _emaFast.Update(input, isNew);
        var eSlow = _emaSlow.Update(input, isNew);

        double apo = eFast.Value - eSlow.Value;
        Last = new TValue(input.Time, apo);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the APO with a new bar (uses Close price).
    /// </summary>
    /// <param name="input">The new bar data</param>
    /// <param name="isNew">Whether this is a new bar or an update to the last bar</param>
    /// <returns>The updated APO value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        return Update(new TValue(input.Time, input.Close), isNew);
    }

    /// <summary>
    /// Updates the APO with a series of values.
    /// </summary>
    /// <param name="source">The source series of values</param>
    /// <returns>The APO series</returns>
    public TSeries Update(TSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], isNew: true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
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

    /// <summary>
    /// Calculates APO for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="fastPeriod">Fast EMA period (default 12)</param>
    /// <param name="slowPeriod">Slow EMA period (default 26)</param>
    /// <returns>APO series</returns>
    public static TSeries Batch(TSeries source, int fastPeriod = 12, int slowPeriod = 26)
    {
        var apo = new Apo(fastPeriod, slowPeriod);
        return apo.Update(source);
    }

    /// <summary>
    /// Calculates APO for the entire span.
    /// </summary>
    /// <param name="source">Input span</param>
    /// <param name="output">Output span</param>
    /// <param name="fastPeriod">Fast EMA period (default 12)</param>
    /// <param name="slowPeriod">Slow EMA period (default 26)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int fastPeriod = 12, int slowPeriod = 26)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of the same length.", nameof(output));
        }

        Span<double> fastEma = source.Length <= 1024 ? stackalloc double[source.Length] : new double[source.Length];
        Span<double> slowEma = source.Length <= 1024 ? stackalloc double[source.Length] : new double[source.Length];

        Ema.Batch(source, fastEma, fastPeriod);
        Ema.Batch(source, slowEma, slowPeriod);

        SimdExtensions.Subtract(fastEma, slowEma, output);
    }

    public static (TSeries Results, Apo Indicator) Calculate(TSeries source, int fastPeriod = 12, int slowPeriod = 26)
    {
        var indicator = new Apo(fastPeriod, slowPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    /// <summary>
    /// Disposes resources and unsubscribes from the source publisher.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        if (_source != null)
        {
            _source.Pub -= _handler;
            _source = null;
        }
    }
}