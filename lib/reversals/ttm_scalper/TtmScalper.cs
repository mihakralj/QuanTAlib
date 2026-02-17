// TTM_SCALPER: TTM Scalper Alert (John Carter)
// Three-bar pivot pattern detecting potential reversal points for scalping entries.
// Simpler cousin of Williams Fractals — uses 3-bar window instead of 5.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TTM_SCALPER: TTM Scalper Alert
/// </summary>
/// <remarks>
/// A retrospective 3-bar pattern detector. A pivot high occurs when bar[1].High
/// is strictly greater than both neighbors' highs. A pivot low occurs when
/// bar[1].Low is strictly less than both neighbors' lows. Optional close-based mode
/// uses close prices instead of high/low.
///
/// Calculation (default high/low mode):
/// <code>
/// PivotHigh = high[1] &gt; high[2] AND high[1] &gt; high[0] ? high[1] : NaN
/// PivotLow  = low[1]  &lt; low[2]  AND low[1]  &lt; low[0]  ? low[1]  : NaN
/// </code>
///
/// Calculation (close-based mode):
/// <code>
/// PivotHigh = close[1] &gt; close[2] AND close[1] &gt; close[0] ? close[1] : NaN
/// PivotLow  = close[1] &lt; close[2] AND close[1] &lt; close[0] ? close[1] : NaN
/// </code>
///
/// <b>Key characteristics:</b>
/// - O(1) update via 3-element circular buffer
/// - Outputs are naturally delayed by 1 bar (the pivot is at bar[1])
/// - Dual output: PivotHigh (bearish reversal) and PivotLow (bullish reversal)
/// - Optional UseCloses parameter for close-based detection
/// - WarmupPeriod = 3 (need exactly 3 bars to detect the first pivot)
/// </remarks>
/// <seealso href="TtmScalper.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class TtmScalper : ITValuePublisher
{
    private const int WindowSize = 3;

    // Circular buffers for highs, lows, and closes — fixed 3 elements
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;
    private readonly double[] _cBuf;

    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose);

    private State _s;
    private State _ps;

    private readonly bool _useCloses;
    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Bars required for the indicator to warm up.</summary>
    public int WarmupPeriod { get; }

    /// <summary>Whether to use close prices instead of high/low for detection.</summary>
    public bool UseCloses => _useCloses;

    /// <summary>Current pivot high price (NaN if no pivot high at current position).</summary>
    public double PivotHigh { get; private set; }

    /// <summary>Current pivot low price (NaN if no pivot low at current position).</summary>
    public double PivotLow { get; private set; }

    /// <summary>Primary output value (PivotHigh as TValue for overlay plotting).</summary>
    public TValue Last { get; private set; }

    /// <summary>True when enough bars have been processed for valid output.</summary>
    public bool IsHot => _count >= WindowSize;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates a TTM Scalper Alert indicator.
    /// </summary>
    /// <param name="useCloses">Use close prices instead of high/low for pivot detection.</param>
    public TtmScalper(bool useCloses = false)
    {
        _useCloses = useCloses;
        _hBuf = new double[WindowSize];
        _lBuf = new double[WindowSize];
        _cBuf = new double[WindowSize];

        _count = 0;
        _index = -1;
        _s = new State(double.NaN, double.NaN, double.NaN);
        _ps = _s;
        PivotHigh = double.NaN;
        PivotLow = double.NaN;

        Name = $"TtmScalper({useCloses})";
        WarmupPeriod = WindowSize;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates a TTM Scalper Alert indicator chained to a TBarSeries source.
    /// </summary>
    public TtmScalper(TBarSeries source, bool useCloses = false)
        : this(useCloses)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _index++;
            _count++;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        // Validate inputs — substitute last-valid on NaN/Infinity
        double high = input.High;
        double low = input.Low;
        double close = input.Close;

        if (double.IsFinite(high)) { s.LastValidHigh = high; }
        else { high = s.LastValidHigh; }

        if (double.IsFinite(low)) { s.LastValidLow = low; }
        else { low = s.LastValidLow; }

        if (double.IsFinite(close)) { s.LastValidClose = close; }
        else { close = s.LastValidClose; }

        // If still no valid data, return NaN
        if (double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
        {
            _s = s;
            PivotHigh = double.NaN;
            PivotLow = double.NaN;
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Store in circular buffer
        int bufIdx = (int)(_index % WindowSize);
        _hBuf[bufIdx] = high;
        _lBuf[bufIdx] = low;
        _cBuf[bufIdx] = close;

        // Need at least 3 bars to evaluate a pivot
        if (_count < WindowSize)
        {
            _s = s;
            PivotHigh = double.NaN;
            PivotLow = double.NaN;
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // The pivot candidate is at position [1] relative to current:
        // Current bar = index 0 (newest), we look at bar[1] = 1 bar ago
        // In circular buffer terms:
        //   bar[0] = bufIdx
        //   bar[1] = (bufIdx - 1 + 3) % 3  <- the candidate
        //   bar[2] = (bufIdx - 2 + 3) % 3

        int i0 = bufIdx;
        int i1 = (bufIdx + WindowSize - 1) % WindowSize; // candidate
        int i2 = (bufIdx + WindowSize - 2) % WindowSize;

        if (_useCloses)
        {
            double c1 = _cBuf[i1];

            // Pivot high: close[1] > close[2] AND close[1] > close[0]
            PivotHigh = (c1 > _cBuf[i2] && c1 > _cBuf[i0])
                ? c1
                : double.NaN;

            // Pivot low: close[1] < close[2] AND close[1] < close[0]
            PivotLow = (c1 < _cBuf[i2] && c1 < _cBuf[i0])
                ? c1
                : double.NaN;
        }
        else
        {
            double h1 = _hBuf[i1];
            double l1 = _lBuf[i1];

            // Pivot high: high[1] > high[2] AND high[1] > high[0]
            PivotHigh = (h1 > _hBuf[i2] && h1 > _hBuf[i0])
                ? h1
                : double.NaN;

            // Pivot low: low[1] < low[2] AND low[1] < low[0]
            PivotLow = (l1 < _lBuf[i2] && l1 < _lBuf[i0])
                ? l1
                : double.NaN;
        }

        _s = s;

        Last = new TValue(input.Time, PivotHigh);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true) =>
        Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);

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

        var downBuf = new double[len];
        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(v), downBuf, _useCloses);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        // Prime internal state for continued streaming
        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        Last = new TValue(lastTime, CollectionsMarshal.AsSpan(v)[^1]);

        return new TSeries(t, v);
    }

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

    public void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        Reset();

        if (source.Length == 0)
        {
            return;
        }

        long t = DateTime.UtcNow.Ticks;
        long stepTicks = (step ?? TimeSpan.FromMinutes(1)).Ticks;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            Update(new TBar(t, val, val, val, val, 0), isNew: true);
            t += stepTicks;
        }
    }

    public void Reset()
    {
        Array.Clear(_hBuf);
        Array.Clear(_lBuf);
        Array.Clear(_cBuf);
        _count = 0;
        _index = -1;
        _s = new State(double.NaN, double.NaN, double.NaN);
        _ps = _s;
        PivotHigh = double.NaN;
        PivotLow = double.NaN;
        Last = default;
    }

    /// <summary>
    /// Batch computation of TTM Scalper pivots over span data.
    /// Writes PivotHigh values to <paramref name="highOutput"/> and PivotLow values to <paramref name="lowOutput"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> highOutput,
        Span<double> lowOutput,
        bool useCloses = false)
    {
        if (high.Length != low.Length || high.Length != close.Length)
        {
            throw new ArgumentException("Input spans must have the same length.", nameof(high));
        }
        if (highOutput.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least as long as input.", nameof(highOutput));
        }
        if (lowOutput.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least as long as input.", nameof(lowOutput));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // Fill first 2 bars with NaN (need 3 bars for first pivot)
        int warmup = Math.Min(WindowSize - 1, len);
        for (int i = 0; i < warmup; i++)
        {
            highOutput[i] = double.NaN;
            lowOutput[i] = double.NaN;
        }

        if (useCloses)
        {
            for (int i = WindowSize - 1; i < len; i++)
            {
                double c1 = close[i - 1];

                highOutput[i] = (c1 > close[i - 2] && c1 > close[i])
                    ? c1
                    : double.NaN;

                lowOutput[i] = (c1 < close[i - 2] && c1 < close[i])
                    ? c1
                    : double.NaN;
            }
        }
        else
        {
            for (int i = WindowSize - 1; i < len; i++)
            {
                double h1 = high[i - 1];
                double l1 = low[i - 1];

                highOutput[i] = (h1 > high[i - 2] && h1 > high[i])
                    ? h1
                    : double.NaN;

                lowOutput[i] = (l1 < low[i - 2] && l1 < low[i])
                    ? l1
                    : double.NaN;
            }
        }
    }

    public static TSeries Batch(TBarSeries source, bool useCloses = false)
    {
        if (source == null || source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);

        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var downBuf = new double[len];
        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(v), downBuf, useCloses);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch computation returning both PivotHigh and PivotLow TSeries.
    /// </summary>
    public static (TSeries PivotHighs, TSeries PivotLows) BatchDual(TBarSeries source, bool useCloses = false)
    {
        if (source == null || source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tHigh = new List<long>(len);
        var vHigh = new List<double>(len);
        var tLow = new List<long>(len);
        var vLow = new List<double>(len);

        CollectionsMarshal.SetCount(tHigh, len);
        CollectionsMarshal.SetCount(vHigh, len);
        CollectionsMarshal.SetCount(tLow, len);
        CollectionsMarshal.SetCount(vLow, len);

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(vHigh), CollectionsMarshal.AsSpan(vLow), useCloses);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(tHigh));
        source.Times.CopyTo(CollectionsMarshal.AsSpan(tLow));

        return (new TSeries(tHigh, vHigh), new TSeries(tLow, vLow));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (TSeries Results, TtmScalper Indicator) Calculate(TBarSeries source, bool useCloses = false)
    {
        var indicator = new TtmScalper(useCloses);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
