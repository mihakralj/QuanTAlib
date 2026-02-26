// TD_SEQ: TD Sequential
// Tom DeMark's exhaustion counting system — two-phase state machine.
// Phase 1 (Setup): counts consecutive closes vs close[comparePeriod]; ±9 completes.
// Phase 2 (Countdown): non-consecutive close vs high[2]/low[2]; ±13 completes.
// All state is O(1) scalars — no circular buffers required.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TD_SEQ: TD Sequential
/// </summary>
/// <remarks>
/// Tom DeMark's exhaustion counting system that identifies potential trend reversals
/// through two phases:
/// <list type="bullet">
///   <item>Phase 1 — Setup (±1 to ±9): consecutive closes vs close[comparePeriod].
///     Positive = sell setup, negative = buy setup. Completes at ±9.</item>
///   <item>Phase 2 — Countdown (±1 to ±13): non-consecutive close vs high/low[2].
///     Begins after a completed setup. Completes at ±13.</item>
/// </list>
/// All state maintained in O(1) scalar variables — no buffers needed beyond
/// a small fixed history ring for close[comparePeriod], high[2], and low[2].
/// <para>
/// References:
///   DeMark, T.R. (1994). The New Science of Technical Analysis. Wiley.
///   PineScript reference: td_seq.pine
/// </para>
/// </remarks>
[SkipLocalsInit]
public sealed class TdSeq : ITValuePublisher
{
    private readonly int _comparePeriod;
    private readonly int _closeSize;  // = comparePeriod + 1

    // Close history ring: stores last (comparePeriod+1) values so we can read close[comparePeriod]
    private readonly double[] _closeHist;
    private readonly double[] _closeSnap;
    private int _closeIdx;        // next write slot
    private int _closeCount;      // how many slots filled (0.._closeSize)
    private int _closeIdxSnap;
    private int _closeCountSnap;

    // High/Low history ring: stores last 3 values for high[2] / low[2]
    private readonly double[] _highHist;
    private readonly double[] _lowHist;
    private readonly double[] _highSnap;
    private readonly double[] _lowSnap;
    private int _hlIdx;           // next write slot (mod 3)
    private int _hlCount;         // how many slots filled (0..3)
    private int _hlIdxSnap;
    private int _hlCountSnap;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        int SetupCount,
        int CountdownCount,
        int CountdownDir,
        bool SetupComplete,
        double LastValidClose,
        double LastValidHigh,
        double LastValidLow);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name of the indicator.</summary>
    public string Name { get; }

    /// <summary>Bars required before Phase 1 produces valid output.</summary>
    public int WarmupPeriod { get; }

    /// <summary>True once enough close history exists to compare close[comparePeriod].</summary>
    public bool IsHot => _closeCount > _comparePeriod;

    /// <summary>Current setup count (−9..+9). Positive = sell setup, negative = buy setup.</summary>
    public int Setup => _s.SetupCount;

    /// <summary>Current countdown count (−13..+13). Non-zero only after a completed setup.</summary>
    public int Countdown => _s.CountdownCount;

    /// <summary>Last published TValue. Value = countdown when active; setup otherwise.</summary>
    public TValue Last { get; private set; }

    /// <inheritdoc cref="ITValuePublisher.Pub"/>
    public event TValuePublishedHandler? Pub;

    /// <summary>Creates TD Sequential with the specified compare period.</summary>
    /// <param name="comparePeriod">Bars back for setup comparison (default 4, must be &gt; 0)</param>
    public TdSeq(int comparePeriod = 4)
    {
        if (comparePeriod <= 0)
        {
            throw new ArgumentException("Compare period must be greater than 0", nameof(comparePeriod));
        }

        _comparePeriod = comparePeriod;
        _closeSize = comparePeriod + 1;

        _closeHist = new double[_closeSize];
        _closeSnap = new double[_closeSize];
        _highHist = new double[3];
        _lowHist = new double[3];
        _highSnap = new double[3];
        _lowSnap = new double[3];

        Name = $"TdSeq({comparePeriod})";
        WarmupPeriod = comparePeriod + 1;

        _barHandler = HandleBar;
    }

    /// <summary>Creates TD Sequential subscribed to a bar publisher.</summary>
    public TdSeq(TBarSeries source, int comparePeriod = 4) : this(comparePeriod)
    {
        source.Pub += _barHandler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    /// <summary>
    /// Processes a bar and returns the current indicator value.
    /// </summary>
    /// <param name="input">OHLCV bar (Close for setup, High/Low for countdown)</param>
    /// <param name="isNew">True to advance state; false to rewrite the current bar</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        // Sanitize inputs — substitute last-valid on non-finite
        double close = double.IsFinite(input.Close) ? input.Close : _s.LastValidClose;
        double high = double.IsFinite(input.High) ? input.High : _s.LastValidHigh;
        double low = double.IsFinite(input.Low) ? input.Low : _s.LastValidLow;

        if (isNew)
        {
            // Snapshot before mutation
            _ps = _s;
            Array.Copy(_closeHist, _closeSnap, _closeSize);
            Array.Copy(_highHist, _highSnap, 3);
            Array.Copy(_lowHist, _lowSnap, 3);
            _closeIdxSnap = _closeIdx;
            _closeCountSnap = _closeCount;
            _hlIdxSnap = _hlIdx;
            _hlCountSnap = _hlCount;

            // Advance close ring
            _closeHist[_closeIdx] = close;
            _closeIdx = (_closeIdx + 1) % _closeSize;
            if (_closeCount < _closeSize) { _closeCount++; }

            // Advance hi/lo ring
            _highHist[_hlIdx] = high;
            _lowHist[_hlIdx] = low;
            _hlIdx = (_hlIdx + 1) % 3;
            if (_hlCount < 3) { _hlCount++; }
        }
        else
        {
            // Rollback rings to snapshot
            _s = _ps;
            Array.Copy(_closeSnap, _closeHist, _closeSize);
            Array.Copy(_highSnap, _highHist, 3);
            Array.Copy(_lowSnap, _lowHist, 3);
            _closeIdx = _closeIdxSnap;
            _closeCount = _closeCountSnap;
            _hlIdx = _hlIdxSnap;
            _hlCount = _hlCountSnap;

            // Re-write newest slots with corrected values
            int newestClose = ((_closeIdx - 1) + _closeSize) % _closeSize;
            _closeHist[newestClose] = close;
            int newestHl = ((_hlIdx - 1) + 3) % 3;
            _highHist[newestHl] = high;
            _lowHist[newestHl] = low;
        }

        // Track last-valid prices for NaN substitution
        if (double.IsFinite(input.Close)) { _s.LastValidClose = close; }
        if (double.IsFinite(input.High)) { _s.LastValidHigh = high; }
        if (double.IsFinite(input.Low)) { _s.LastValidLow = low; }

        if (!IsHot)
        {
            Last = new TValue(input.Time, 0.0);
            PubEvent(Last, isNew);
            return Last;
        }

        // close[comparePeriod] = the oldest entry in the close ring:
        // after writing, _closeIdx points to the NEXT write slot.
        // That slot holds the oldest value (it is _comparePeriod bars ago).
        double prevClose = _closeHist[_closeIdx % _closeSize];

        // --- Phase 1: Setup counting ---
        State s = _s;
        int newSetup;
        if (close < prevClose)
        {
            newSetup = s.SetupCount < 0 ? s.SetupCount - 1 : -1;
        }
        else if (close > prevClose)
        {
            newSetup = s.SetupCount > 0 ? s.SetupCount + 1 : 1;
        }
        else
        {
            newSetup = 0;
        }

        if (newSetup > 9) { newSetup = 9; }
        if (newSetup < -9) { newSetup = -9; }

        // Detect completed setup (first time reaching ±9)
        if (Math.Abs(newSetup) == 9 && !s.SetupComplete)
        {
            s.SetupComplete = true;
            s.CountdownCount = 0;
            s.CountdownDir = newSetup > 0 ? 1 : -1;
        }

        // Clear setupComplete if streak broke or reversed
        if (Math.Abs(newSetup) < Math.Abs(s.SetupCount) ||
            (newSetup > 0 && s.SetupCount < 0) ||
            (newSetup < 0 && s.SetupCount > 0))
        {
            s.SetupComplete = false;
        }

        s.SetupCount = newSetup;

        // --- Phase 2: Countdown (non-consecutive) ---
        if (s.CountdownDir != 0 && _hlCount >= 3)
        {
            // high[2] and low[2] = oldest entry in the 3-element hi/lo ring
            // After writing, _hlIdx points to the next write slot = oldest slot
            int oldestHl = _hlIdx % 3;
            double high2 = _highHist[oldestHl];
            double low2 = _lowHist[oldestHl];

            if (s.CountdownDir == -1 && close < low2)
            {
                s.CountdownCount--;
            }
            else if (s.CountdownDir == 1 && close > high2)
            {
                s.CountdownCount++;
            }

            if (Math.Abs(s.CountdownCount) >= 13)
            {
                s.CountdownCount = s.CountdownDir == 1 ? 13 : -13;
                s.CountdownDir = 0;
            }

            // Opposite ±9 setup resets countdown
            if ((s.CountdownDir == 1 && newSetup == -9) ||
                (s.CountdownDir == -1 && newSetup == 9))
            {
                s.CountdownCount = 0;
                s.CountdownDir = newSetup > 0 ? 1 : -1;
            }
        }

        _s = s;

        // Output: countdown value when active; setup value otherwise
        double result = (double)(_s.CountdownDir != 0 ? _s.CountdownCount : _s.SetupCount);
        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>Resets all state and history to zero.</summary>
    public void Reset()
    {
        _s = default;
        _ps = default;
        Array.Clear(_closeHist);
        Array.Clear(_closeSnap);
        Array.Clear(_highHist);
        Array.Clear(_lowHist);
        Array.Clear(_highSnap);
        Array.Clear(_lowSnap);
        _closeIdx = 0;
        _closeCount = 0;
        _closeIdxSnap = 0;
        _closeCountSnap = 0;
        _hlIdx = 0;
        _hlCount = 0;
        _hlIdxSnap = 0;
        _hlCountSnap = 0;
        Last = default;
    }

    /// <summary>
    /// Calculates TD Sequential for an entire bar series.
    /// </summary>
    /// <param name="source">Source bar series</param>
    /// <param name="comparePeriod">Bars back for setup comparison (default 4)</param>
    /// <returns>TSeries containing the combined setup/countdown output per bar</returns>
    public static TSeries Calculate(TBarSeries source, int comparePeriod = 4)
    {
        var indicator = new TdSeq(comparePeriod);
        int len = source.Count;
        var results = new TSeries();
        for (int i = 0; i < len; i++)
        {
            results.Add(indicator.Update(source[i], isNew: true));
        }

        return results;
    }
}
