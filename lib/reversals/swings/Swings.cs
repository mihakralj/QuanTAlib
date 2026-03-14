// SWINGS: Swing High/Low Detection
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SWINGS: Swing High/Low Detection
/// </summary>
/// <remarks>
/// A configurable-lookback pattern detector for swing highs and swing lows.
/// A swing high occurs when the center bar's high is strictly greater than all
/// neighbors' highs within the lookback window on each side. A swing low occurs
/// when the center bar's low is strictly less than all neighbors' lows.
///
/// Calculation:
/// <code>
/// windowSize = 2 * lookback + 1
/// center     = lookback  (index into the window)
///
/// SwingHigh = high[center] > ALL high[i] for i in [0..windowSize) where i != center
///             ? high[center] : NaN
/// SwingLow  = low[center]  &lt; ALL low[i]  for i in [0..windowSize) where i != center
///             ? low[center] : NaN
/// </code>
///
/// <b>Key characteristics:</b>
/// - O(1) update via circular buffer (no deques needed)
/// - Outputs are delayed by <c>lookback</c> bars (the swing point is at center)
/// - Dual output: SwingHigh (resistance) and SwingLow (support)
/// - Persistent LastSwingHigh / LastSwingLow hold most recent swing level
/// - WarmupPeriod = 2 * lookback + 1
/// </remarks>
/// <seealso href="Swings.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Swings : ITValuePublisher
{
    private const int DefaultLookback = 5;

    private readonly int _lookback;
    private readonly int _windowSize;

    // Circular buffers for highs and lows
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;

    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose,
        double LastSwingHigh,
        double LastSwingLow);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Bars required for the indicator to warm up.</summary>
    public int WarmupPeriod { get; }

    /// <summary>The lookback period on each side of the center bar.</summary>
    public int Lookback => _lookback;

    /// <summary>Current swing high value (NaN if no swing high at current position).</summary>
    public double SwingHigh { get; private set; }

    /// <summary>Current swing low value (NaN if no swing low at current position).</summary>
    public double SwingLow { get; private set; }

    /// <summary>Most recent confirmed swing high level (persists until next swing high).</summary>
    public double LastSwingHigh => _s.LastSwingHigh;

    /// <summary>Most recent confirmed swing low level (persists until next swing low).</summary>
    public double LastSwingLow => _s.LastSwingLow;

    /// <summary>Primary output value (SwingHigh as TValue for overlay plotting).</summary>
    public TValue Last { get; private set; }

    /// <summary>True when enough bars have been processed for valid output.</summary>
    public bool IsHot => _count >= _windowSize;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates a Swing High/Low detector with the specified lookback period.
    /// </summary>
    /// <param name="lookback">Number of bars on each side to confirm a swing point (default: 5).</param>
    public Swings(int lookback = DefaultLookback)
    {
        if (lookback < 1)
        {
            throw new ArgumentException("Lookback must be >= 1.", nameof(lookback));
        }

        _lookback = lookback;
        _windowSize = (2 * lookback) + 1;

        _hBuf = new double[_windowSize];
        _lBuf = new double[_windowSize];

        _count = 0;
        _index = -1;
        _s = new State(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        _ps = _s;
        SwingHigh = double.NaN;
        SwingLow = double.NaN;

        Name = $"Swings({lookback})";
        WarmupPeriod = _windowSize;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates a Swing High/Low detector chained to a TBarSeries source.
    /// </summary>
    public Swings(TBarSeries source, int lookback = DefaultLookback)
        : this(lookback)
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
            SwingHigh = double.NaN;
            SwingLow = double.NaN;
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Store in circular buffer
        int bufIdx = (int)(_index % _windowSize);
        _hBuf[bufIdx] = high;
        _lBuf[bufIdx] = low;

        // Need at least windowSize bars to evaluate a swing
        if (_count < _windowSize)
        {
            _s = s;
            SwingHigh = double.NaN;
            SwingLow = double.NaN;
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // The swing candidate is at center = lookback bars ago
        // In circular buffer:
        //   bar[0]        = bufIdx (newest)
        //   bar[lookback] = (bufIdx - lookback + windowSize) % windowSize  <- the candidate
        int centerIdx = (bufIdx + _windowSize - _lookback) % _windowSize;
        double centerHigh = _hBuf[centerIdx];
        double centerLow = _lBuf[centerIdx];

        bool isSwingHigh = true;
        bool isSwingLow = true;

        for (int i = 0; i < _windowSize; i++)
        {
            if (i == centerIdx)
            {
                continue;
            }

            if (_hBuf[i] >= centerHigh)
            {
                isSwingHigh = false;
            }

            if (_lBuf[i] <= centerLow)
            {
                isSwingLow = false;
            }

            if (!isSwingHigh && !isSwingLow)
            {
                break;
            }
        }

        SwingHigh = isSwingHigh ? centerHigh : double.NaN;
        SwingLow = isSwingLow ? centerLow : double.NaN;

        // Update persistent last-swing levels
        if (isSwingHigh)
        {
            s.LastSwingHigh = centerHigh;
        }

        if (isSwingLow)
        {
            s.LastSwingLow = centerLow;
        }

        _s = s;

        Last = new TValue(input.Time, SwingHigh);
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
            CollectionsMarshal.AsSpan(v), downBuf, _lookback);

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
        _s = new State(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        _ps = _s;
        SwingHigh = double.NaN;
        SwingLow = double.NaN;
        Last = default;
    }

    /// <summary>
    /// Batch computation of Swing High/Low over span data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        Span<double> highOutput,
        Span<double> lowOutput,
        int lookback = DefaultLookback)
    {
        if (lookback < 1)
        {
            throw new ArgumentException("Lookback must be >= 1.", nameof(lookback));
        }
        if (high.Length != low.Length)
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

        int windowSize = (2 * lookback) + 1;

        // Fill warmup bars with NaN
        int warmup = Math.Min(windowSize - 1, len);
        for (int i = 0; i < warmup; i++)
        {
            highOutput[i] = double.NaN;
            lowOutput[i] = double.NaN;
        }

        // Evaluate swings directly — center is at index [i - lookback]
        for (int i = windowSize - 1; i < len; i++)
        {
            int center = i - lookback;
            double centerHigh = high[center];
            double centerLow = low[center];

            bool isSwingHigh = true;
            bool isSwingLow = true;

            for (int j = center - lookback; j <= center + lookback; j++)
            {
                if (j == center)
                {
                    continue;
                }

                if (high[j] >= centerHigh)
                {
                    isSwingHigh = false;
                }

                if (low[j] <= centerLow)
                {
                    isSwingLow = false;
                }

                if (!isSwingHigh && !isSwingLow)
                {
                    break;
                }
            }

            highOutput[i] = isSwingHigh ? centerHigh : double.NaN;
            lowOutput[i] = isSwingLow ? centerLow : double.NaN;
        }
    }

    public static TSeries Batch(TBarSeries source, int lookback = DefaultLookback)
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
            CollectionsMarshal.AsSpan(v), downBuf, lookback);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch computation returning both SwingHigh and SwingLow TSeries.
    /// </summary>
    public static (TSeries SwingHighs, TSeries SwingLows) BatchDual(TBarSeries source, int lookback = DefaultLookback)
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
            CollectionsMarshal.AsSpan(vUp), CollectionsMarshal.AsSpan(vDown), lookback);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(tUp));
        source.Times.CopyTo(CollectionsMarshal.AsSpan(tDown));

        return (new TSeries(tUp, vUp), new TSeries(tDown, vDown));
    }

    public static (TSeries Results, Swings Indicator) Calculate(TBarSeries source, int lookback = DefaultLookback)
    {
        var indicator = new Swings(lookback);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
