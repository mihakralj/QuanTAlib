// TTM_WAVE: John Carter's TTM Wave Indicator
// Multi-period MACD composite using Fibonacci EMA periods.
// Measures momentum across short (A), medium (B), and long (C) timeframes.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TTM_WAVE: John Carter's TTM Wave Indicator
/// </summary>
/// <remarks>
/// Composite oscillator built from six MACD-histogram channels at Fibonacci EMA periods.
/// All channels share fast EMA period 8; slow/signal periods follow the Fibonacci sequence:
/// 34, 55, 89, 144, 233, 377.
///
/// Wave grouping (matching thinkorswim TTM_Wave_A_B_C):
///   Wave A (short-term): channels 1 (8,34,34) and 2 (8,55,55)
///   Wave B (medium-term): channels 3 (8,89,89) and 4 (8,144,144)
///   Wave C (long-term):   channels 5 (8,233,233) and 6 (8,377,377)
///
/// TOS TTM_Wave compatibility:
///   Wave1    = WaveA2 (channel 1 histogram)
///   Wave2High = max(WaveC1, WaveC2)
///   Wave2Low  = min(WaveC1, WaveC2)
/// </remarks>
[SkipLocalsInit]
public sealed class TtmWave : ITValuePublisher, IDisposable
{
    private const int FastPeriod = 8;

    // Fibonacci slow/signal periods for each channel
    private const int Slow1 = 34;
    private const int Slow2 = 55;
    private const int Slow3 = 89;
    private const int Slow4 = 144;
    private const int Slow5 = 233;
    private const int Slow6 = 377;

    // Six MACD channels — each computes: histogram = (EMA_fast - EMA_slow) - EMA_signal(EMA_fast - EMA_slow)
    private readonly Macd _macd1; // (8,34,34) → Wave A inner
    private readonly Macd _macd2; // (8,55,55) → Wave A outer
    private readonly Macd _macd3; // (8,89,89) → Wave B inner
    private readonly Macd _macd4; // (8,144,144) → Wave B outer
    private readonly Macd _macd5; // (8,233,233) → Wave C inner
    private readonly Macd _macd6; // (8,377,377) → Wave C outer

    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler _handler;
    private bool _disposed;

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <summary>True when all six channels have sufficient warmup data.</summary>
    public bool IsHot => _macd1.IsHot && _macd2.IsHot && _macd3.IsHot
                      && _macd4.IsHot && _macd5.IsHot && _macd6.IsHot;

    /// <summary>Bars required before output is valid (377 + 377 - 2 = 752).</summary>
    public int WarmupPeriod { get; }

    // ── Full ABC histogram outputs ──────────────────────────────────

    /// <summary>Wave A outer histogram: MACD(8,55) - Signal(55). Larger A envelope.</summary>
    public TValue WaveA1 { get; private set; }

    /// <summary>Wave A inner histogram: MACD(8,34) - Signal(34). Smaller A envelope.</summary>
    public TValue WaveA2 { get; private set; }

    /// <summary>Wave B outer histogram: MACD(8,144) - Signal(144). Larger B envelope.</summary>
    public TValue WaveB1 { get; private set; }

    /// <summary>Wave B inner histogram: MACD(8,89) - Signal(89). Smaller B envelope.</summary>
    public TValue WaveB2 { get; private set; }

    /// <summary>Wave C outer histogram: MACD(8,377) - Signal(377). Larger C envelope.</summary>
    public TValue WaveC1 { get; private set; }

    /// <summary>Wave C inner histogram: MACD(8,233) - Signal(233). Smaller C envelope.</summary>
    public TValue WaveC2 { get; private set; }

    // ── TOS-compatible convenience properties ───────────────────────

    /// <summary>TOS Wave1 plot: short-term A wave (= WaveA2, channel 1 histogram).</summary>
    public TValue Wave1 => WaveA2;

    /// <summary>TOS Wave2High: max of long-term C wave histograms.</summary>
    public double Wave2High => Math.Max(WaveC1.Value, WaveC2.Value);

    /// <summary>TOS Wave2Low: min of long-term C wave histograms.</summary>
    public double Wave2Low => Math.Min(WaveC1.Value, WaveC2.Value);

    /// <summary>Primary output = Wave1 (A wave inner, matching TOS default).</summary>
    public TValue Last => Wave1;

    /// <summary>Reactive event publisher.</summary>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates a TTM Wave indicator with canonical Fibonacci periods.
    /// </summary>
    public TtmWave()
    {
        _macd1 = new Macd(FastPeriod, Slow1, Slow1);
        _macd2 = new Macd(FastPeriod, Slow2, Slow2);
        _macd3 = new Macd(FastPeriod, Slow3, Slow3);
        _macd4 = new Macd(FastPeriod, Slow4, Slow4);
        _macd5 = new Macd(FastPeriod, Slow5, Slow5);
        _macd6 = new Macd(FastPeriod, Slow6, Slow6);
        _handler = Handle;

        Name = "TtmWave";
        // Warmup = max channel warmup = max(8, 377) + 377 - 2 = 752
        WarmupPeriod = Math.Max(FastPeriod, Slow6) + Slow6 - 2;
    }

    /// <summary>
    /// Creates a TTM Wave indicator chained to a source publisher.
    /// </summary>
    public TtmWave(ITValuePublisher source) : this()
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
            if (disposing)
            {
                if (_source != null)
                {
                    _source.Pub -= _handler;
                }
                _macd1.Dispose();
                _macd2.Dispose();
                _macd3.Dispose();
                _macd4.Dispose();
                _macd5.Dispose();
                _macd6.Dispose();
            }
            _disposed = true;
        }
    }

    /// <summary>Resets all internal state.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _macd1.Reset();
        _macd2.Reset();
        _macd3.Reset();
        _macd4.Reset();
        _macd5.Reset();
        _macd6.Reset();
        WaveA1 = default;
        WaveA2 = default;
        WaveB1 = default;
        WaveB2 = default;
        WaveC1 = default;
        WaveC2 = default;
    }

    /// <summary>
    /// Updates the indicator with a new value.
    /// </summary>
    /// <param name="input">Price value (typically close).</param>
    /// <param name="isNew">True for new bar; false for current bar update.</param>
    /// <returns>Primary output (Wave1 = A wave inner histogram).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        // Feed all six MACD channels — each handles isNew rollback internally
        _macd1.Update(input, isNew);
        _macd2.Update(input, isNew);
        _macd3.Update(input, isNew);
        _macd4.Update(input, isNew);
        _macd5.Update(input, isNew);
        _macd6.Update(input, isNew);

        // Extract histogram values and compose wave outputs
        // thinkScript mapping: WaveA1 = hist2 (outer), WaveA2 = hist1 (inner)
        WaveA1 = new TValue(input.Time, _macd2.Histogram.Value);
        WaveA2 = new TValue(input.Time, _macd1.Histogram.Value);
        WaveB1 = new TValue(input.Time, _macd4.Histogram.Value);
        WaveB2 = new TValue(input.Time, _macd3.Histogram.Value);
        WaveC1 = new TValue(input.Time, _macd6.Histogram.Value);
        WaveC2 = new TValue(input.Time, _macd5.Histogram.Value);

        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Batch-processes an entire series.
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
    /// Primes the indicator with historical data without producing output.
    /// </summary>
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
    /// Static batch calculation with default parameters.
    /// </summary>
    public static TSeries Batch(TSeries source)
    {
        var indicator = new TtmWave();
        return indicator.Update(source);
    }

    /// <summary>
    /// Static calculation returning both results and the warm indicator.
    /// </summary>
    public static (TSeries Results, TtmWave Indicator) Calculate(TSeries source)
    {
        var indicator = new TtmWave();
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }
}
