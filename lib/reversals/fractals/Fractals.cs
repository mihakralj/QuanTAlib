// FRACTALS: Williams Fractals
// Five-bar pattern identifying local highs (up fractals) and local lows (down fractals).
// Created by Larry Williams (1995, "Trading Chaos").

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// FRACTALS: Williams Fractals
/// </summary>
/// <remarks>
/// A retrospective 5-bar pattern detector. An up-fractal occurs when bar[2].High
/// is strictly greater than all four neighbors' highs. A down-fractal occurs when
/// bar[2].Low is strictly less than all four neighbors' lows.
///
/// Calculation:
/// <code>
/// UpFractal   = high[2] &gt; high[0] AND high[2] &gt; high[1] AND high[2] &gt; high[3] AND high[2] &gt; high[4]
///               ? high[2] : NaN
/// DownFractal = low[2]  &lt; low[0]  AND low[2]  &lt; low[1]  AND low[2]  &lt; low[3]  AND low[2]  &lt; low[4]
///               ? low[2] : NaN
/// </code>
///
/// <b>Key characteristics:</b>
/// - O(1) update via 5-element circular buffer (no deques needed)
/// - Outputs are naturally delayed by 2 bars (the fractal is at bar[2])
/// - Dual output: UpFractal (bearish reversal / resistance) and DownFractal (bullish reversal / support)
/// - No configurable parameters -- fixed 5-bar pattern per Williams' definition
/// - WarmupPeriod = 5 (need exactly 5 bars to detect the first fractal)
/// </remarks>
/// <seealso href="Fractals.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Fractals : ITValuePublisher
{
    private const int WindowSize = 5;

    // Circular buffers for highs and lows -- fixed 5 elements
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;

    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Bars required for the indicator to warm up.</summary>
    public int WarmupPeriod { get; }

    /// <summary>Current up-fractal value (NaN if no up-fractal at current position).</summary>
    public double UpFractal { get; private set; }

    /// <summary>Current down-fractal value (NaN if no down-fractal at current position).</summary>
    public double DownFractal { get; private set; }

    /// <summary>Primary output value (UpFractal as TValue for overlay plotting).</summary>
    public TValue Last { get; private set; }

    /// <summary>True when enough bars have been processed for valid output.</summary>
    public bool IsHot => _count >= WindowSize;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates a Williams Fractals indicator.
    /// </summary>
    public Fractals()
    {
        _hBuf = new double[WindowSize];
        _lBuf = new double[WindowSize];

        _count = 0;
        _index = -1;
        _s = new State(double.NaN, double.NaN, double.NaN);
        _ps = _s;
        UpFractal = double.NaN;
        DownFractal = double.NaN;

        Name = "Fractals";
        WarmupPeriod = WindowSize;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates a Williams Fractals indicator chained to a TBarSeries source.
    /// </summary>
    public Fractals(TBarSeries source)
        : this()
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

        // Validate inputs -- substitute last-valid on NaN/Infinity
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
            UpFractal = double.NaN;
            DownFractal = double.NaN;
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Store in circular buffer
        int bufIdx = (int)(_index % WindowSize);
        _hBuf[bufIdx] = high;
        _lBuf[bufIdx] = low;

        // Need at least 5 bars to evaluate a fractal
        if (_count < WindowSize)
        {
            _s = s;
            UpFractal = double.NaN;
            DownFractal = double.NaN;
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // The fractal candidate is at position [2] relative to current:
        // Current bar = index 0 (newest), we look at bar[2] = 2 bars ago
        // In circular buffer terms:
        //   bar[0] = bufIdx
        //   bar[1] = (bufIdx - 1 + 5) % 5
        //   bar[2] = (bufIdx - 2 + 5) % 5  <- the candidate
        //   bar[3] = (bufIdx - 3 + 5) % 5
        //   bar[4] = (bufIdx - 4 + 5) % 5

        int i0 = bufIdx;
        int i1 = (bufIdx + WindowSize - 1) % WindowSize;
        int i2 = (bufIdx + WindowSize - 2) % WindowSize; // candidate
        int i3 = (bufIdx + WindowSize - 3) % WindowSize;
        int i4 = (bufIdx + WindowSize - 4) % WindowSize;

        double h2 = _hBuf[i2];
        double l2 = _lBuf[i2];

        // Up fractal: high[2] > all four neighbors
        UpFractal = (h2 > _hBuf[i0] && h2 > _hBuf[i1] && h2 > _hBuf[i3] && h2 > _hBuf[i4])
            ? h2
            : double.NaN;

        // Down fractal: low[2] < all four neighbors
        DownFractal = (l2 < _lBuf[i0] && l2 < _lBuf[i1] && l2 < _lBuf[i3] && l2 < _lBuf[i4])
            ? l2
            : double.NaN;

        _s = s;

        Last = new TValue(input.Time, UpFractal);
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
        Batch(source.HighValues, source.LowValues,
            CollectionsMarshal.AsSpan(v), downBuf);

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
        _count = 0;
        _index = -1;
        _s = new State(double.NaN, double.NaN, double.NaN);
        _ps = _s;
        UpFractal = double.NaN;
        DownFractal = double.NaN;
        Last = default;
    }

    /// <summary>
    /// Batch computation of Williams Fractals over span data.
    /// Writes UpFractal values to <paramref name="upOutput"/> and DownFractal values to <paramref name="downOutput"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        Span<double> upOutput,
        Span<double> downOutput)
    {
        if (high.Length != low.Length)
        {
            throw new ArgumentException("Input spans must have the same length.", nameof(high));
        }
        if (upOutput.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least as long as input.", nameof(upOutput));
        }
        if (downOutput.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least as long as input.", nameof(downOutput));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // Fill first 4 bars with NaN (need 5 bars for first fractal)
        int warmup = Math.Min(WindowSize - 1, len);
        for (int i = 0; i < warmup; i++)
        {
            upOutput[i] = double.NaN;
            downOutput[i] = double.NaN;
        }

        // Evaluate fractals directly -- no streaming overhead needed
        for (int i = WindowSize - 1; i < len; i++)
        {
            double h2 = high[i - 2];
            double l2 = low[i - 2];

            upOutput[i] = (h2 > high[i] && h2 > high[i - 1] && h2 > high[i - 3] && h2 > high[i - 4])
                ? h2
                : double.NaN;

            downOutput[i] = (l2 < low[i] && l2 < low[i - 1] && l2 < low[i - 3] && l2 < low[i - 4])
                ? l2
                : double.NaN;
        }
    }

    public static TSeries Batch(TBarSeries source)
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
        Batch(source.HighValues, source.LowValues,
            CollectionsMarshal.AsSpan(v), downBuf);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch computation returning both UpFractal and DownFractal TSeries.
    /// </summary>
    public static (TSeries UpFractals, TSeries DownFractals) BatchDual(TBarSeries source)
    {
        if (source == null || source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tUp = new List<long>(len);
        var vUp = new List<double>(len);
        var tDown = new List<long>(len);
        var vDown = new List<double>(len);

        CollectionsMarshal.SetCount(tUp, len);
        CollectionsMarshal.SetCount(vUp, len);
        CollectionsMarshal.SetCount(tDown, len);
        CollectionsMarshal.SetCount(vDown, len);

        Batch(source.HighValues, source.LowValues,
            CollectionsMarshal.AsSpan(vUp), CollectionsMarshal.AsSpan(vDown));

        source.Times.CopyTo(CollectionsMarshal.AsSpan(tUp));
        source.Times.CopyTo(CollectionsMarshal.AsSpan(tDown));

        return (new TSeries(tUp, vUp), new TSeries(tDown, vDown));
    }

    public static (TSeries Results, Fractals Indicator) Calculate(TBarSeries source)
    {
        var indicator = new Fractals();
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
