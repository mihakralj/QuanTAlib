// RVGI: Relative Vigor Index
// Measures market vigor by comparing closing strength (close-open) to the full
// intrabar range (high-low), smoothed via 4-tap SWMA then averaged over a period.
// John Ehlers, "Rocket Science for Traders" (2002), Chapter 12.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RVGI: Ehlers Relative Vigor Index
/// </summary>
/// <remarks>
/// Dual-output oscillator built in four stages:
/// <list type="number">
///   <item>SWMA(close−open, 4 bars) with weights [1,2,2,1]/6 → numerator per bar</item>
///   <item>SWMA(high−low, 4 bars) with same weights → denominator per bar</item>
///   <item>SMA(numerator, period) / SMA(denominator, period) → RVGI line</item>
///   <item>SWMA(RVGI, 4 bars) → Signal line</item>
/// </list>
/// Both SMA stages use O(1) circular buffers with count-based warmup.
/// Defensive division: denominator SMA == 0 returns 0.
///
/// References:
///   Ehlers, J.F. (2002). Rocket Science for Traders. Wiley.
///   PineScript reference: rvgi.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Rvgi : ITValuePublisher
{
    private readonly int _period;

    // Two circular buffers for O(1) SMA of numerator and denominator
    private readonly double[] _numBuf;
    private readonly double[] _denBuf;

    // Snapshots for idempotent isNew=false rollback (circular-buffer-snapshot-rollback pattern)
    private readonly double[] _numSnap;
    private readonly double[] _denSnap;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double NumSum,
        double DenSum,
        int Idx,
        int Count,
        // SWMA history for 4-bar kernel on bars (3 history slots: t-1, t-2, t-3)
        double Co1, double Co2, double Co3,   // close-open history
        double Hl1, double Hl2, double Hl3,   // high-low history
        // SWMA history for signal line (3 history slots of RVGI)
        double Rv1, double Rv2, double Rv3,
        // Last-valid substitution fields
        double LastValidOpen, double LastValidHigh, double LastValidLow, double LastValidClose,
        double RvgiValue, double SignalValue);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Bars required for the first valid output.</summary>
    public int WarmupPeriod { get; }

    /// <summary>True once the SMA window is fully populated.</summary>
    public bool IsHot => _s.Count >= _period;

    /// <summary>Primary output: the RVGI line value.</summary>
    public TValue Last { get; private set; }

    /// <summary>RVGI line (same as Last.Value).</summary>
    public double RvgiValue => _s.RvgiValue;

    /// <summary>Signal line: 4-bar SWMA of RVGI.</summary>
    public double Signal => _s.SignalValue;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates RVGI with the specified SMA smoothing period.
    /// </summary>
    /// <param name="period">SMA period (must be &gt; 0, default 10)</param>
    public Rvgi(int period = 10)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _numBuf = new double[period];
        _denBuf = new double[period];
        _numSnap = new double[period];
        _denSnap = new double[period];

        _s = new State(
            NumSum: 0.0, DenSum: 0.0, Idx: 0, Count: 0,
            Co1: 0.0, Co2: 0.0, Co3: 0.0,
            Hl1: 0.0, Hl2: 0.0, Hl3: 0.0,
            Rv1: 0.0, Rv2: 0.0, Rv3: 0.0,
            LastValidOpen: double.NaN, LastValidHigh: double.NaN,
            LastValidLow: double.NaN, LastValidClose: double.NaN,
            RvgiValue: 0.0, SignalValue: 0.0);
        _ps = _s;

        WarmupPeriod = period;
        Name = $"Rvgi({period})";
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates RVGI chained to a TBarSeries source.
    /// </summary>
    public Rvgi(TBarSeries source, int period = 10) : this(period)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    /// <summary>Resets all state to initial conditions.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(
            NumSum: 0.0, DenSum: 0.0, Idx: 0, Count: 0,
            Co1: 0.0, Co2: 0.0, Co3: 0.0,
            Hl1: 0.0, Hl2: 0.0, Hl3: 0.0,
            Rv1: 0.0, Rv2: 0.0, Rv3: 0.0,
            LastValidOpen: double.NaN, LastValidHigh: double.NaN,
            LastValidLow: double.NaN, LastValidClose: double.NaN,
            RvgiValue: 0.0, SignalValue: 0.0);
        _ps = _s;
        Last = default;
        Array.Clear(_numBuf);
        Array.Clear(_denBuf);
        Array.Clear(_numSnap);
        Array.Clear(_denSnap);
    }

    /// <summary>
    /// Updates RVGI with a new bar.
    /// </summary>
    /// <param name="input">OHLCV bar data</param>
    /// <param name="isNew">True to advance state; false to rewrite the latest bar</param>
    /// <returns>Current RVGI value as TValue (primary output)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        var s = _s;

        if (isNew)
        {
            // Snapshot all circular buffers before mutation — required for idempotent rollback
            _ps = s;
            Array.Copy(_numBuf, _numSnap, _period);
            Array.Copy(_denBuf, _denSnap, _period);
            s.Count++;
        }
        else
        {
            // Restore scalar state and buffer snapshots atomically
            s = _ps;
            Array.Copy(_numSnap, _numBuf, _period);
            Array.Copy(_denSnap, _denBuf, _period);
        }

        // Sanitize OHLC inputs — last-valid substitution on NaN/Infinity
        double open = input.Open;
        double high = input.High;
        double low = input.Low;
        double close = input.Close;

        if (double.IsFinite(open)) { s.LastValidOpen = open; } else { open = double.IsNaN(s.LastValidOpen) ? 0.0 : s.LastValidOpen; }
        if (double.IsFinite(high)) { s.LastValidHigh = high; } else { high = double.IsNaN(s.LastValidHigh) ? open : s.LastValidHigh; }
        if (double.IsFinite(low)) { s.LastValidLow = low; } else { low = double.IsNaN(s.LastValidLow) ? open : s.LastValidLow; }
        if (double.IsFinite(close)) { s.LastValidClose = close; } else { close = double.IsNaN(s.LastValidClose) ? open : s.LastValidClose; }

        // Step 1: Per-bar contributions to SWMA kernel
        double co0 = close - open;
        double hl0 = high - low;

        // Step 2: SWMA(close-open, 4) = (co3 + 2*co2 + 2*co1 + co0) / 6
        double swmaNum = Math.FusedMultiplyAdd(2.0, s.Co1, Math.FusedMultiplyAdd(2.0, s.Co2, s.Co3 + co0)) / 6.0;
        // Step 3: SWMA(high-low, 4) = (hl3 + 2*hl2 + 2*hl1 + hl0) / 6
        double swmaDen = Math.FusedMultiplyAdd(2.0, s.Hl1, Math.FusedMultiplyAdd(2.0, s.Hl2, s.Hl3 + hl0)) / 6.0;

        // Shift bar SWMA history
        s.Co3 = s.Co2;
        s.Co2 = s.Co1;
        s.Co1 = co0;
        s.Hl3 = s.Hl2;
        s.Hl2 = s.Hl1;
        s.Hl1 = hl0;

        // Step 4: O(1) circular-buffer SMA for numerator
        int idx = s.Idx;
        s.NumSum = s.NumSum - _numBuf[idx] + swmaNum;
        s.DenSum = s.DenSum - _denBuf[idx] + swmaDen;
        _numBuf[idx] = swmaNum;
        _denBuf[idx] = swmaDen;

        // Advance circular index on new bars only
        if (isNew)
        {
            s.Idx = (idx + 1) % _period;
        }

        // Step 5: RVGI = SMA(num) / SMA(den) — defensive against zero denominator
        int effective = Math.Min(s.Count, _period);
        if (effective < 1) { effective = 1; }
        double smaNum = s.NumSum / effective;
        double smaDen = s.DenSum / effective;
        double rvgiVal = smaDen != 0.0 ? smaNum / smaDen : 0.0;

        // Step 6: Signal = SWMA(RVGI, 4) = (rv3 + 2*rv2 + 2*rv1 + rvgi) / 6
        double sigVal = Math.FusedMultiplyAdd(2.0, s.Rv1, Math.FusedMultiplyAdd(2.0, s.Rv2, s.Rv3 + rvgiVal)) / 6.0;

        // Shift RVGI history
        s.Rv3 = s.Rv2;
        s.Rv2 = s.Rv1;
        s.Rv1 = rvgiVal;

        s.RvgiValue = rvgiVal;
        s.SignalValue = sigVal;

        _s = s;

        Last = new TValue(input.Time, rvgiVal);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates RVGI from a TValue (creates a synthetic bar with all OHLC == value).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true) =>
        Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);

    /// <summary>
    /// Updates RVGI from a TBarSeries, computing RVGI and Signal series.
    /// </summary>
    public (TSeries Rvgi, TSeries Signal) UpdateAll(TBarSeries source)
    {
        int len = source.Count;
        if (len == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        var rvgiList = new List<double>(len);
        var sigList = new List<double>(len);
        CollectionsMarshal.SetCount(rvgiList, len);
        CollectionsMarshal.SetCount(sigList, len);

        var rvgiSpan = CollectionsMarshal.AsSpan(rvgiList);
        var sigSpan = CollectionsMarshal.AsSpan(sigList);

        Batch(
            source.OpenValues, source.HighValues,
            source.LowValues, source.CloseValues,
            rvgiSpan, sigSpan, _period);

        var tList = new List<long>(len);
        CollectionsMarshal.SetCount(tList, len);
        source.Open.Times.CopyTo(CollectionsMarshal.AsSpan(tList));

        // Re-prime internal state for continued streaming
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return (new TSeries(tList, rvgiList), new TSeries(tList, sigList));
    }

    /// <summary>
    /// Batch-computes RVGI over raw OHLC spans. Zero-allocation path for large datasets.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> open,
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> rvgiOutput,
        Span<double> signalOutput,
        int period = 10)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = open.Length;

        if (high.Length != len)
        {
            throw new ArgumentException("High length must match open length", nameof(high));
        }
        if (low.Length != len)
        {
            throw new ArgumentException("Low length must match open length", nameof(low));
        }
        if (close.Length != len)
        {
            throw new ArgumentException("Close length must match open length", nameof(close));
        }
        if (rvgiOutput.Length != len)
        {
            throw new ArgumentException("rvgiOutput length must match input length", nameof(rvgiOutput));
        }
        if (signalOutput.Length != len)
        {
            throw new ArgumentException("signalOutput length must match input length", nameof(signalOutput));
        }

        if (len == 0)
        {
            return;
        }

        const int StackallocThreshold = 256;

        double[]? rentedNum = null;
        double[]? rentedDen = null;

        scoped Span<double> numBuf;
        scoped Span<double> denBuf;

        if (period <= StackallocThreshold)
        {
            numBuf = stackalloc double[period];
            denBuf = stackalloc double[period];
        }
        else
        {
            rentedNum = ArrayPool<double>.Shared.Rent(period);
            rentedDen = ArrayPool<double>.Shared.Rent(period);
            numBuf = rentedNum.AsSpan(0, period);
            denBuf = rentedDen.AsSpan(0, period);
        }

        try
        {
            numBuf.Clear();
            denBuf.Clear();

            double numSum = 0.0;
            double denSum = 0.0;
            int idx = 0;
            int count = 0;

            // SWMA bar history
            double co1 = 0.0, co2 = 0.0, co3 = 0.0;
            double hl1 = 0.0, hl2 = 0.0, hl3 = 0.0;
            // Signal SWMA history
            double rv1 = 0.0, rv2 = 0.0, rv3 = 0.0;

            for (int i = 0; i < len; i++)
            {
                double o = open[i];
                double h = high[i];
                double l = low[i];
                double c = close[i];

                double co0 = c - o;
                double hl0 = h - l;

                double swmaNum = Math.FusedMultiplyAdd(2.0, co1, Math.FusedMultiplyAdd(2.0, co2, co3 + co0)) / 6.0;
                double swmaDen = Math.FusedMultiplyAdd(2.0, hl1, Math.FusedMultiplyAdd(2.0, hl2, hl3 + hl0)) / 6.0;

                co3 = co2; co2 = co1; co1 = co0;
                hl3 = hl2; hl2 = hl1; hl1 = hl0;

                numSum = numSum - numBuf[idx] + swmaNum;
                denSum = denSum - denBuf[idx] + swmaDen;
                numBuf[idx] = swmaNum;
                denBuf[idx] = swmaDen;

                idx = (idx + 1) % period;
                count++;

                int effective = Math.Min(count, period);
                double smaNum = numSum / effective;
                double smaDen = denSum / effective;
                double rvgiVal = smaDen != 0.0 ? smaNum / smaDen : 0.0;

                double sigVal = Math.FusedMultiplyAdd(2.0, rv1, Math.FusedMultiplyAdd(2.0, rv2, rv3 + rvgiVal)) / 6.0;
                rv3 = rv2; rv2 = rv1; rv1 = rvgiVal;

                rvgiOutput[i] = rvgiVal;
                signalOutput[i] = sigVal;
            }
        }
        finally
        {
            if (rentedNum != null) { ArrayPool<double>.Shared.Return(rentedNum); }
            if (rentedDen != null) { ArrayPool<double>.Shared.Return(rentedDen); }
        }
    }

    /// <summary>Primes the indicator by replaying historical data without firing events.</summary>
    public void Prime(TBarSeries source)
    {
        foreach (var bar in source)
        {
            Update(bar, isNew: true);
        }
    }
}
