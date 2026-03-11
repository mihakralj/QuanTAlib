using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SAREXT: Parabolic SAR Extended (TA-Lib)
/// </summary>
/// <remarks>
/// Extended Parabolic Stop And Reverse with asymmetric acceleration factors.
/// Separate AF initialization, increment, and maximum for long vs short positions.
/// Sign-encoded output: positive = long (SAR below price), negative = short (SAR above price).
///
/// Calculation extends Wilder's PSAR:
/// <code>
/// Long:  SAR = SAR + AF_long  × (EP - SAR), output = +SAR
/// Short: SAR = SAR + AF_short × (EP - SAR), output = -SAR
///
/// Bar 0:  Collect OHLC data
/// Bar 1:  Determine direction from startValue or DM auto-detect
/// Bar 2+: Standard SAR state machine with asymmetric AF parameters
/// </code>
///
/// <b>Key characteristics:</b>
/// - O(1) per-bar state machine with long/short mode transitions
/// - Asymmetric acceleration factors for long and short positions
/// - startValue parameter forces initial direction (0 = auto-detect from DM)
/// - offsetOnReverse adds gap buffer on trend reversal
/// - Sign-encoded output matches TA-Lib SAREXT convention
/// - Default parameters: afInitLong/Short=0.02, afLong/Short=0.02, afMaxLong/Short=0.20
/// </remarks>
/// <seealso href="Sarext.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Sarext : ITValuePublisher
{
    private const double DefaultStartValue = 0;
    private const double DefaultOffsetOnReverse = 0;
    private const double DefaultAfInitLong = 0.02;
    private const double DefaultAfLong = 0.02;
    private const double DefaultAfMaxLong = 0.20;
    private const double DefaultAfInitShort = 0.02;
    private const double DefaultAfShort = 0.02;
    private const double DefaultAfMaxShort = 0.20;

    private readonly double _startValue;
    private readonly double _offsetOnReverse;
    private readonly double _afInitLong;
    private readonly double _afLong;
    private readonly double _afMaxLong;
    private readonly double _afInitShort;
    private readonly double _afShort;
    private readonly double _afMaxShort;

    private int _samples;
    private int _p_samples;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        bool IsLong,
        double Sar,
        double Ep,
        double Af,
        double Prev1High,
        double Prev1Low,
        double Prev2High,
        double Prev2Low,
        double LastValidOpen,
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

    /// <summary>Current SAR value (unsigned).</summary>
    public double Sar => _s.Sar;

    /// <summary>True when the SAREXT is in long (uptrend) mode.</summary>
    public bool IsLong => _s.IsLong;

    /// <summary>Primary output value (sign-encoded SAR: positive = long, negative = short).</summary>
    public TValue Last { get; private set; }

    /// <summary>True when enough bars have been processed for valid output.</summary>
    public bool IsHot => _samples >= 2;

    /// <inheritdoc />
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates a Parabolic SAR Extended indicator.
    /// </summary>
    /// <param name="startValue">Initial direction: positive = long, negative = short, 0 = auto-detect from DM.</param>
    /// <param name="offsetOnReverse">Gap added to SAR on reversal (default 0).</param>
    /// <param name="afInitLong">Initial acceleration factor for long positions (default 0.02).</param>
    /// <param name="afLong">AF increment per new EP in long positions (default 0.02).</param>
    /// <param name="afMaxLong">Maximum AF for long positions (default 0.20).</param>
    /// <param name="afInitShort">Initial acceleration factor for short positions (default 0.02).</param>
    /// <param name="afShort">AF increment per new EP in short positions (default 0.02).</param>
    /// <param name="afMaxShort">Maximum AF for short positions (default 0.20).</param>
    public Sarext(
        double startValue = DefaultStartValue,
        double offsetOnReverse = DefaultOffsetOnReverse,
        double afInitLong = DefaultAfInitLong,
        double afLong = DefaultAfLong,
        double afMaxLong = DefaultAfMaxLong,
        double afInitShort = DefaultAfInitShort,
        double afShort = DefaultAfShort,
        double afMaxShort = DefaultAfMaxShort)
    {
        if (afInitLong <= 0)
        {
            throw new ArgumentException("afInitLong must be > 0.", nameof(afInitLong));
        }
        if (afLong <= 0)
        {
            throw new ArgumentException("afLong must be > 0.", nameof(afLong));
        }
        if (afMaxLong <= afInitLong)
        {
            throw new ArgumentException("afMaxLong must be > afInitLong.", nameof(afMaxLong));
        }
        if (afInitShort <= 0)
        {
            throw new ArgumentException("afInitShort must be > 0.", nameof(afInitShort));
        }
        if (afShort <= 0)
        {
            throw new ArgumentException("afShort must be > 0.", nameof(afShort));
        }
        if (afMaxShort <= afInitShort)
        {
            throw new ArgumentException("afMaxShort must be > afInitShort.", nameof(afMaxShort));
        }
        if (offsetOnReverse < 0)
        {
            throw new ArgumentException("offsetOnReverse must be >= 0.", nameof(offsetOnReverse));
        }

        _startValue = startValue;
        _offsetOnReverse = offsetOnReverse;
        _afInitLong = afInitLong;
        _afLong = afLong;
        _afMaxLong = afMaxLong;
        _afInitShort = afInitShort;
        _afShort = afShort;
        _afMaxShort = afMaxShort;

        _samples = 0;
        _p_samples = 0;
        _s = new State(
            IsLong: true,
            Sar: double.NaN,
            Ep: double.NaN,
            Af: afInitLong,
            Prev1High: double.NaN,
            Prev1Low: double.NaN,
            Prev2High: double.NaN,
            Prev2Low: double.NaN,
            LastValidOpen: double.NaN,
            LastValidHigh: double.NaN,
            LastValidLow: double.NaN,
            LastValidClose: double.NaN);
        _ps = _s;

        Name = "Sarext";
        WarmupPeriod = 2;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates a SAREXT indicator chained to a TBarSeries source.
    /// </summary>
    public Sarext(TBarSeries source,
        double startValue = DefaultStartValue,
        double offsetOnReverse = DefaultOffsetOnReverse,
        double afInitLong = DefaultAfInitLong,
        double afLong = DefaultAfLong,
        double afMaxLong = DefaultAfMaxLong,
        double afInitShort = DefaultAfInitShort,
        double afShort = DefaultAfShort,
        double afMaxShort = DefaultAfMaxShort)
        : this(startValue, offsetOnReverse, afInitLong, afLong, afMaxLong, afInitShort, afShort, afMaxShort)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    /// <summary>
    /// Updates the SAREXT with a new OHLC bar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _p_samples = _samples;
            _samples++;
        }
        else
        {
            _s = _ps;
            _samples = _p_samples + 1;
        }

        var s = _s;

        // Validate inputs — substitute last-valid on NaN/Infinity
        double open = input.Open;
        double high = input.High;
        double low = input.Low;
        double close = input.Close;

        if (double.IsFinite(open)) { s.LastValidOpen = open; }
        else { open = s.LastValidOpen; }

        if (double.IsFinite(high)) { s.LastValidHigh = high; }
        else { high = s.LastValidHigh; }

        if (double.IsFinite(low)) { s.LastValidLow = low; }
        else { low = s.LastValidLow; }

        if (double.IsFinite(close)) { s.LastValidClose = close; }
        else { close = s.LastValidClose; }

        // If still no valid data, return NaN
        if (double.IsNaN(open) || double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
        {
            _s = s;
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        double sarResult;

        if (_samples == 1)
        {
            // Bar 0: Collect first bar's OHLC, no output yet
            s.Prev1High = high;
            s.Prev1Low = low;
            s.Prev2High = high;
            s.Prev2Low = low;
            s.LastValidOpen = open;
            s.LastValidHigh = high;
            s.LastValidLow = low;
            s.LastValidClose = close;

            // Tentative initialization — will be finalized on bar 1
            s.Sar = high;
            s.Ep = low;
            s.Af = _afInitShort;
            s.IsLong = false;

            _s = s;
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }
        else if (_samples == 2)
        {
            // Bar 1: Determine initial direction
            double prevHigh = s.Prev1High;
            double prevLow = s.Prev1Low;

            if (_startValue > 0)
            {
                // Force long
                s.IsLong = true;
                s.Sar = Math.Min(prevLow, low);
                s.Ep = Math.Max(prevHigh, high);
                s.Af = _afInitLong;
            }
            else if (_startValue < 0)
            {
                // Force short
                s.IsLong = false;
                s.Sar = Math.Max(prevHigh, high);
                s.Ep = Math.Min(prevLow, low);
                s.Af = _afInitShort;
            }
            else
            {
                // Auto-detect from DM: compare plusDM vs minusDM
                double plusDM = high - prevHigh;
                double minusDM = prevLow - low;

                if (plusDM > minusDM && plusDM > 0)
                {
                    // Long
                    s.IsLong = true;
                    s.Sar = Math.Min(prevLow, low);
                    s.Ep = Math.Max(prevHigh, high);
                    s.Af = _afInitLong;
                }
                else
                {
                    // Short (default when equal or minusDM dominates)
                    s.IsLong = false;
                    s.Sar = Math.Max(prevHigh, high);
                    s.Ep = Math.Min(prevLow, low);
                    s.Af = _afInitShort;
                }
            }

            sarResult = s.Sar;

            // Update prev-bar tracking
            s.Prev2High = s.Prev1High;
            s.Prev2Low = s.Prev1Low;
            s.Prev1High = high;
            s.Prev1Low = low;

            _s = s;
            double output = s.IsLong ? sarResult : -sarResult;
            Last = new TValue(input.Time, output);
            PubEvent(Last, isNew);
            return Last;
        }

        // Bar 2+: Standard SAR state machine with asymmetric AF
        // Compute new SAR using FMA: sar + af * (ep - sar)
        double newSar = Math.FusedMultiplyAdd(s.Af, s.Ep - s.Sar, s.Sar);

        if (s.IsLong)
        {
            // Long mode: SAR must be at or below prior two bars' lows
            newSar = Math.Min(newSar, s.Prev1Low);
            newSar = Math.Min(newSar, s.Prev2Low);

            // Check for reversal: price crosses below SAR
            if (low <= newSar)
            {
                // Reverse to short
                s.IsLong = false;
                newSar = s.Ep + _offsetOnReverse;
                s.Ep = low;
                s.Af = _afInitShort;
            }
            else
            {
                // Check for new extreme point (new high)
                if (high > s.Ep)
                {
                    s.Ep = high;
                    s.Af = Math.Min(s.Af + _afLong, _afMaxLong);
                }
            }
        }
        else
        {
            // Short mode: SAR must be at or above prior two bars' highs
            newSar = Math.Max(newSar, s.Prev1High);
            newSar = Math.Max(newSar, s.Prev2High);

            // Check for reversal: price crosses above SAR
            if (high >= newSar)
            {
                // Reverse to long
                s.IsLong = true;
                newSar = s.Ep - _offsetOnReverse;
                s.Ep = high;
                s.Af = _afInitLong;
            }
            else
            {
                // Check for new extreme point (new low)
                if (low < s.Ep)
                {
                    s.Ep = low;
                    s.Af = Math.Min(s.Af + _afShort, _afMaxShort);
                }
            }
        }

        s.Sar = newSar;
        sarResult = newSar;

        // Shift prior bar tracking
        if (isNew)
        {
            s.Prev2High = s.Prev1High;
            s.Prev2Low = s.Prev1Low;
            s.Prev1High = high;
            s.Prev1Low = low;
        }
        else
        {
            // Bar correction: update current bar's values
            s.Prev1High = high;
            s.Prev1Low = low;
        }

        _s = s;

        double signedResult = s.IsLong ? sarResult : -sarResult;
        Last = new TValue(input.Time, signedResult);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the SAREXT with a TValue (uses value as OHLC proxy).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true) =>
        Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);

    /// <summary>
    /// Processes a full TBarSeries and returns sign-encoded SAREXT output.
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

        Batch(source.OpenValues, source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(v), len,
            _startValue, _offsetOnReverse,
            _afInitLong, _afLong, _afMaxLong,
            _afInitShort, _afShort, _afMaxShort);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        // Prime internal state for continued streaming
        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        Last = new TValue(lastTime, CollectionsMarshal.AsSpan(v)[^1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Primes the indicator from a TBarSeries (replays all bars to set state).
    /// </summary>
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
    /// Primes the indicator from a span of doubles (uses each value as OHLC proxy).
    /// </summary>
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

    /// <summary>
    /// Resets the indicator to its initial state.
    /// </summary>
    public void Reset()
    {
        _samples = 0;
        _p_samples = 0;
        _s = new State(
            IsLong: true,
            Sar: double.NaN,
            Ep: double.NaN,
            Af: _afInitLong,
            Prev1High: double.NaN,
            Prev1Low: double.NaN,
            Prev2High: double.NaN,
            Prev2Low: double.NaN,
            LastValidOpen: double.NaN,
            LastValidHigh: double.NaN,
            LastValidLow: double.NaN,
            LastValidClose: double.NaN);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Span-based batch computation of SAREXT.
    /// </summary>
    /// <param name="open">Input open prices.</param>
    /// <param name="high">Input high prices.</param>
    /// <param name="low">Input low prices.</param>
    /// <param name="close">Input close prices.</param>
    /// <param name="output">Output span for sign-encoded SAR values.</param>
    /// <param name="n">Number of bars to process.</param>
    /// <param name="startValue">Initial direction: positive = long, negative = short, 0 = auto-detect.</param>
    /// <param name="offsetOnReverse">Gap added to SAR on reversal.</param>
    /// <param name="afInitLong">Initial AF for long positions.</param>
    /// <param name="afLong">AF increment for long positions.</param>
    /// <param name="afMaxLong">Maximum AF for long positions.</param>
    /// <param name="afInitShort">Initial AF for short positions.</param>
    /// <param name="afShort">AF increment for short positions.</param>
    /// <param name="afMaxShort">Maximum AF for short positions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> open,
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int n,
        double startValue = DefaultStartValue,
        double offsetOnReverse = DefaultOffsetOnReverse,
        double afInitLong = DefaultAfInitLong,
        double afLong = DefaultAfLong,
        double afMaxLong = DefaultAfMaxLong,
        double afInitShort = DefaultAfInitShort,
        double afShort = DefaultAfShort,
        double afMaxShort = DefaultAfMaxShort)
    {
        if (afInitLong <= 0 || afInitLong >= afMaxLong)
        {
            throw new ArgumentException("afInitLong must be > 0 and < afMaxLong.", nameof(afInitLong));
        }
        if (afLong <= 0)
        {
            throw new ArgumentException("afLong must be > 0.", nameof(afLong));
        }
        if (afInitShort <= 0 || afInitShort >= afMaxShort)
        {
            throw new ArgumentException("afInitShort must be > 0 and < afMaxShort.", nameof(afInitShort));
        }
        if (afShort <= 0)
        {
            throw new ArgumentException("afShort must be > 0.", nameof(afShort));
        }
        if (high.Length != low.Length || high.Length != close.Length || high.Length != open.Length)
        {
            throw new ArgumentException("Input spans must have the same length.", nameof(high));
        }
        if (output.Length < n)
        {
            throw new ArgumentException("Output span must be at least n elements.", nameof(output));
        }

        if (n == 0)
        {
            return;
        }

        // State machine prevents SIMD — compute via streaming instance
        var indicator = new Sarext(startValue, offsetOnReverse,
            afInitLong, afLong, afMaxLong, afInitShort, afShort, afMaxShort);

        long baseTime = DateTime.UtcNow.Ticks;
        for (int i = 0; i < n; i++)
        {
            _ = indicator.Update(
                new TBar(baseTime + i, open[i], high[i], low[i], close[i], 0),
                isNew: true);
            output[i] = indicator.Last.Value;
        }
    }

    /// <summary>
    /// Batch computation from a TBarSeries.
    /// </summary>
    public static TSeries Batch(
        TBarSeries source,
        double startValue = DefaultStartValue,
        double offsetOnReverse = DefaultOffsetOnReverse,
        double afInitLong = DefaultAfInitLong,
        double afLong = DefaultAfLong,
        double afMaxLong = DefaultAfMaxLong,
        double afInitShort = DefaultAfInitShort,
        double afShort = DefaultAfShort,
        double afMaxShort = DefaultAfMaxShort)
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

        Batch(source.OpenValues, source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(v), len,
            startValue, offsetOnReverse,
            afInitLong, afLong, afMaxLong,
            afInitShort, afShort, afMaxShort);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates SAREXT and returns both the result series and the primed indicator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (TSeries Results, Sarext Indicator) Calculate(
        TBarSeries source,
        double startValue = DefaultStartValue,
        double offsetOnReverse = DefaultOffsetOnReverse,
        double afInitLong = DefaultAfInitLong,
        double afLong = DefaultAfLong,
        double afMaxLong = DefaultAfMaxLong,
        double afInitShort = DefaultAfInitShort,
        double afShort = DefaultAfShort,
        double afMaxShort = DefaultAfMaxShort)
    {
        var indicator = new Sarext(startValue, offsetOnReverse,
            afInitLong, afLong, afMaxLong, afInitShort, afShort, afMaxShort);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
