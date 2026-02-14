using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// IMPULSE: Elder Impulse System
/// </summary>
/// <remarks>
/// Combines a 13-period EMA (inertia) with MACD(12,26,9) histogram (momentum)
/// to classify each bar as bullish, bearish, or neutral.
///
/// Calculation:
/// <code>
/// Green (+1): EMA rising AND MACD-Histogram rising
/// Red   (-1): EMA falling AND MACD-Histogram falling
/// Blue  ( 0): Mixed signals (neither green nor red)
/// </code>
///
/// <b>Key characteristics:</b>
/// - O(1) update complexity per bar
/// - Composes internal EMA and MACD child indicators
/// - Output value is the 13-period EMA (suitable for overlay plotting)
/// - Signal property provides the discrete impulse state (-1, 0, +1)
/// - Default parameters: EMA(13), MACD(12,26,9) per Alexander Elder
/// </remarks>
/// <seealso href="Impulse.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Impulse : ITValuePublisher, IDisposable
{
    private readonly Ema _ema;
    private readonly Macd _macd;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler _handler;
    private bool _disposed;

    private double _prevEma;
    private double _prevHistogram;
    private int _sampleCount;

    // Snapshot state for bar correction
    private double _p_prevEma;
    private double _p_prevHistogram;
    private int _p_sampleCount;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Current EMA value (suitable for overlay plotting).</summary>
    public TValue Last { get; private set; }

    /// <summary>Current impulse signal: +1 (bullish/green), -1 (bearish/red), 0 (neutral/blue).</summary>
    public int Signal { get; private set; }

    /// <summary>True when both EMA and MACD are warmed up and comparison values exist.</summary>
    public bool IsHot => _sampleCount > 1 && _ema.IsHot && _macd.IsHot;

    /// <summary>Bars required for the indicator to warm up.</summary>
    public int WarmupPeriod { get; }

    /// <summary>The EMA period parameter.</summary>
    public int EmaPeriod { get; }

    /// <summary>The MACD fast period parameter.</summary>
    public int MacdFast { get; }

    /// <summary>The MACD slow period parameter.</summary>
    public int MacdSlow { get; }

    /// <summary>The MACD signal period parameter.</summary>
    public int MacdSignal { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates an Elder Impulse System indicator.
    /// </summary>
    /// <param name="emaPeriod">EMA period for trend inertia (default 13).</param>
    /// <param name="macdFast">MACD fast EMA period (default 12).</param>
    /// <param name="macdSlow">MACD slow EMA period (default 26).</param>
    /// <param name="macdSignal">MACD signal EMA period (default 9).</param>
    public Impulse(int emaPeriod = 13, int macdFast = 12, int macdSlow = 26, int macdSignal = 9)
    {
        if (emaPeriod < 1)
        {
            throw new ArgumentException("EMA period must be at least 1.", nameof(emaPeriod));
        }
        if (macdFast < 1)
        {
            throw new ArgumentException("MACD fast period must be at least 1.", nameof(macdFast));
        }
        if (macdSlow < 1)
        {
            throw new ArgumentException("MACD slow period must be at least 1.", nameof(macdSlow));
        }
        if (macdSignal < 1)
        {
            throw new ArgumentException("MACD signal period must be at least 1.", nameof(macdSignal));
        }

        EmaPeriod = emaPeriod;
        MacdFast = macdFast;
        MacdSlow = macdSlow;
        MacdSignal = macdSignal;

        _ema = new Ema(emaPeriod);
        _macd = new Macd(macdFast, macdSlow, macdSignal);
        _handler = Handle;

        Name = $"Impulse({emaPeriod},{macdFast},{macdSlow},{macdSignal})";
        WarmupPeriod = Math.Max(emaPeriod, macdSlow) + macdSignal - 1;
    }

    /// <summary>
    /// Creates an Elder Impulse System chained to a source publisher.
    /// </summary>
    public Impulse(ITValuePublisher source, int emaPeriod = 13, int macdFast = 12, int macdSlow = 26, int macdSignal = 9)
        : this(emaPeriod, macdFast, macdSlow, macdSignal)
    {
        _source = source;
        _source.Pub += _handler;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= _handler;
            }
            _disposed = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _ema.Reset();
        _macd.Reset();
        _prevEma = 0;
        _prevHistogram = 0;
        _sampleCount = 0;
        _p_prevEma = 0;
        _p_prevHistogram = 0;
        _p_sampleCount = 0;
        Signal = 0;
        Last = default;
    }

    /// <summary>
    /// Updates the Elder Impulse System with a new close price value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        // State management for bar correction
        if (isNew)
        {
            _p_prevEma = _prevEma;
            _p_prevHistogram = _prevHistogram;
            _p_sampleCount = _sampleCount;
        }
        else
        {
            _prevEma = _p_prevEma;
            _prevHistogram = _p_prevHistogram;
            _sampleCount = _p_sampleCount;
        }

        // Update child indicators
        var emaResult = _ema.Update(input, isNew);
        _ = _macd.Update(input, isNew);

        double currentEma = emaResult.Value;
        double currentHistogram = _macd.Histogram.Value;

        // Classify impulse
        if (_sampleCount > 0)
        {
            bool emaRising = currentEma > _prevEma;
            bool emaFalling = currentEma < _prevEma;
            bool histRising = currentHistogram > _prevHistogram;
            bool histFalling = currentHistogram < _prevHistogram;

            if (emaRising && histRising)
            {
                Signal = 1;
            }
            else if (emaFalling && histFalling)
            {
                Signal = -1;
            }
            else
            {
                Signal = 0;
            }
        }
        else
        {
            Signal = 0;
        }

        // Advance state
        _prevEma = currentEma;
        _prevHistogram = currentHistogram;

        if (isNew)
        {
            _sampleCount++;
        }

        Last = emaResult;
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates with a price bar (uses Close price).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return Update(new TValue(bar.Time, bar.Close), isNew);
    }

    /// <summary>
    /// Updates with a value series.
    /// </summary>
    public TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
            tSpan[i] = source[i].Time;
            vSpan[i] = Last.Value;
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Updates with a bar series.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        var times = source.Open.Times;
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
            tSpan[i] = times[i];
            vSpan[i] = Last.Value;
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Primes the indicator with historical data.
    /// </summary>
    public void Prime(TSeries source)
    {
        Reset();
        for (int i = 0; i < source.Count; i++)
        {
            Update(new TValue(new DateTime(source.Times[i], DateTimeKind.Utc), source.Values[i]), isNew: true);
        }
    }

    /// <summary>
    /// Batch calculation returning EMA values.
    /// </summary>
    public static TSeries Batch(TSeries source, int emaPeriod = 13, int macdFast = 12, int macdSlow = 26, int macdSignal = 9)
    {
        var indicator = new Impulse(emaPeriod, macdFast, macdSlow, macdSignal);
        return indicator.Update(source);
    }

    /// <summary>
    /// Returns the indicator and its results.
    /// </summary>
    public static (TSeries Results, Impulse Indicator) Calculate(TSeries source, int emaPeriod = 13, int macdFast = 12, int macdSlow = 26, int macdSignal = 9)
    {
        var indicator = new Impulse(emaPeriod, macdFast, macdSlow, macdSignal);
        var results = indicator.Update(source);
        return (results, indicator);
    }

    /// <summary>
    /// Returns the indicator and its results for a bar series.
    /// </summary>
    public static (TSeries Results, Impulse Indicator) Calculate(TBarSeries source, int emaPeriod = 13, int macdFast = 12, int macdSlow = 26, int macdSignal = 9)
    {
        var indicator = new Impulse(emaPeriod, macdFast, macdSlow, macdSignal);
        var results = indicator.Update(source);
        return (results, indicator);
    }

    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }
}
