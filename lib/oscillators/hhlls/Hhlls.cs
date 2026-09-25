using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HHLLS: Higher Highs &amp; Lower Lows Stochastics (Apirine).
/// </summary>
/// <remarks>
/// A dual momentum oscillator that tracks trend direction via conditional
/// stochastic calculations on High and Low price series.
///
/// Calculation:
/// <c>hhRange = HighestHigh(period) - LowestHigh(period)</c>
/// <c>llRange = HighestLow(period) - LowestLow(period)</c>
/// <c>hhRaw  = High &gt; prevHigh &amp;&amp; hhRange &gt; 0 ? (High - LowestHigh) / hhRange * 100 : 0</c>
/// <c>llRaw  = Low  &lt; prevLow  &amp;&amp; llRange &gt; 0 ? (HighestLow - Low) / llRange * 100 : 0</c>
/// <c>HHS    = EMA(hhRaw, period)</c>
/// <c>LLS    = EMA(llRaw, period)</c>
///
/// Reference: Apirine, V. (2016). "Higher Highs &amp; Lower Lows."
///            Technical Analysis of Stocks &amp; Commodities, February 2016.
/// </remarks>
/// <seealso href="hhlls.md">Detailed documentation</seealso>
/// <seealso href="hhlls.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Hhlls : ITValuePublisher
{
    private const int DefaultPeriod = 20;

    private readonly int _period;
    private readonly double _emaAlpha;
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;
    private readonly MonotonicDeque _maxHighDeque;
    private readonly MonotonicDeque _minHighDeque;
    private readonly MonotonicDeque _maxLowDeque;
    private readonly MonotonicDeque _minLowDeque;

    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double HhsEma, double LlsEma,
        double PrevHigh, double PrevLow,
        double LastValidHigh, double LastValidLow);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Indicator display name.</summary>
    public string Name { get; }

    /// <summary>Bars required before <see cref="IsHot"/> becomes true.</summary>
    public int WarmupPeriod { get; }

    /// <summary>Most recent calculated value (HHS).</summary>
    public TValue Last { get; private set; }

    /// <summary>Current Higher High Stochastic value.</summary>
    public TValue Hhs { get; private set; }

    /// <summary>Current Lower Low Stochastic value.</summary>
    public TValue Lls { get; private set; }

    /// <summary>True when enough bars have accumulated for valid output.</summary>
    public bool IsHot => _count >= _period;

    /// <inheritdoc/>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates HHLLS with specified period.
    /// </summary>
    /// <param name="period">Lookback period for stochastic window and EMA smoothing (must be ≥ 2).</param>
    public Hhlls(int period = DefaultPeriod)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "Period must be at least 2.");
        }

        _period = period;
        _emaAlpha = 2.0 / (period + 1);

        _hBuf = new double[period];
        _lBuf = new double[period];
        _maxHighDeque = new MonotonicDeque(period);
        _minHighDeque = new MonotonicDeque(period);
        _maxLowDeque = new MonotonicDeque(period);
        _minLowDeque = new MonotonicDeque(period);

        _count = 0;
        _index = -1;
        _s = new State(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        _ps = _s;

        Name = $"Hhlls({period})";
        WarmupPeriod = period;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates HHLLS that subscribes to a TBarSeries source.
    /// Primes from history, then subscribes to live updates.
    /// </summary>
    public Hhlls(TBarSeries source, int period = DefaultPeriod) : this(period)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    /// <summary>
    /// Process a single bar update (streaming).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _index++;
            if (_count < _period)
            {
                _count++;
            }
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        // Validate inputs — substitute last-valid on NaN/Infinity
        double high = input.High;
        double low = input.Low;

        if (double.IsFinite(high)) { s.LastValidHigh = high; }
        else { high = s.LastValidHigh; }

        if (double.IsFinite(low)) { s.LastValidLow = low; }
        else { low = s.LastValidLow; }

        // If still no valid data, return NaN
        if (double.IsNaN(high) || double.IsNaN(low))
        {
            _s = s;
            Last = new TValue(input.Time, double.NaN);
            Hhs = new TValue(input.Time, double.NaN);
            Lls = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        int bufIdx = _index < 0 ? 0 : (int)(_index % _period);
        _hBuf[bufIdx] = high;
        _lBuf[bufIdx] = low;

        if (isNew)
        {
            _maxHighDeque.PushMax(_index, high, _hBuf);
            _minHighDeque.PushMin(_index, high, _hBuf);
            _maxLowDeque.PushMax(_index, low, _lBuf);
            _minLowDeque.PushMin(_index, low, _lBuf);
        }
        else
        {
            _maxHighDeque.RebuildMax(_hBuf, _index, _count);
            _minHighDeque.RebuildMin(_hBuf, _index, _count);
            _maxLowDeque.RebuildMax(_lBuf, _index, _count);
            _minLowDeque.RebuildMin(_lBuf, _index, _count);
        }

        double highestHigh = _maxHighDeque.GetExtremum(_hBuf);
        double lowestHigh = _minHighDeque.GetExtremum(_hBuf);
        double highestLow = _maxLowDeque.GetExtremum(_lBuf);
        double lowestLow = _minLowDeque.GetExtremum(_lBuf);

        double hhRange = highestHigh - lowestHigh;
        double llRange = highestLow - lowestLow;

        // Conditional stochastic: only compute when higher high / lower low confirmed
        double hhRaw = (high > s.PrevHigh && hhRange > 0.0 && !double.IsNaN(s.PrevHigh))
            ? 100.0 * (high - lowestHigh) / hhRange
            : 0.0;

        double llRaw = (low < s.PrevLow && llRange > 0.0 && !double.IsNaN(s.PrevLow))
            ? 100.0 * (highestLow - low) / llRange
            : 0.0;

        // EMA smoothing
        double hhsEma;
        double llsEma;
        if (double.IsNaN(s.HhsEma))
        {
            hhsEma = hhRaw;
            llsEma = llRaw;
        }
        else
        {
            hhsEma = Math.FusedMultiplyAdd(_emaAlpha, hhRaw - s.HhsEma, s.HhsEma);
            llsEma = Math.FusedMultiplyAdd(_emaAlpha, llRaw - s.LlsEma, s.LlsEma);
        }

        s.HhsEma = hhsEma;
        s.LlsEma = llsEma;
        s.PrevHigh = high;
        s.PrevLow = low;

        _s = s;

        Hhs = new TValue(input.Time, hhsEma);
        Lls = new TValue(input.Time, llsEma);
        Last = new TValue(input.Time, hhsEma);

        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Scalar fallback: treats single value as High=Low=value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true) =>
        Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);

    /// <summary>
    /// Batch calculation returning dual TSeries (HHS, LLS).
    /// </summary>
    public (TSeries Hhs, TSeries Lls) Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tH = new List<long>(len);
        var vH = new List<double>(len);
        var tL = new List<long>(len);
        var vL = new List<double>(len);

        CollectionsMarshal.SetCount(tH, len);
        CollectionsMarshal.SetCount(vH, len);
        CollectionsMarshal.SetCount(tL, len);
        CollectionsMarshal.SetCount(vL, len);

        var vHSpan = CollectionsMarshal.AsSpan(vH);
        var vLSpan = CollectionsMarshal.AsSpan(vL);

        Batch(source.HighValues, source.LowValues,
            vHSpan, vLSpan, _period);

        var tSpan = CollectionsMarshal.AsSpan(tH);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tL));

        // Prime internal state for continued streaming
        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        Hhs = new TValue(lastTime, vHSpan[^1]);
        Lls = new TValue(lastTime, vLSpan[^1]);
        Last = new TValue(lastTime, vHSpan[^1]);

        return (new TSeries(tH, vH), new TSeries(tL, vL));
    }

    /// <summary>
    /// Prime the indicator from historical bar data.
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
    /// Reset all state to initial condition.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_hBuf);
        Array.Clear(_lBuf);
        _maxHighDeque.Reset();
        _minHighDeque.Reset();
        _maxLowDeque.Reset();
        _minLowDeque.Reset();
        _count = 0;
        _index = -1;
        _s = new State(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        _ps = _s;
        Last = default;
        Hhs = default;
        Lls = default;
    }

    /// <summary>
    /// Zero-allocation batch calculation writing to pre-allocated output spans.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        Span<double> hhsOut,
        Span<double> llsOut,
        int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "Period must be at least 2.");
        }
        if (high.Length != low.Length)
        {
            throw new ArgumentException("Input spans must have the same length.", nameof(high));
        }
        if (hhsOut.Length < high.Length)
        {
            throw new ArgumentException("HHS output span must be at least as long as input.", nameof(hhsOut));
        }
        if (llsOut.Length < high.Length)
        {
            throw new ArgumentException("LLS output span must be at least as long as input.", nameof(llsOut));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        const int StackallocThreshold = 256;
        double[]? rentedHH = null;
        double[]? rentedLH = null;
        double[]? rentedHL = null;
        double[]? rentedLL = null;
        scoped Span<double> highestHighBuf;
        scoped Span<double> lowestHighBuf;
        scoped Span<double> highestLowBuf;
        scoped Span<double> lowestLowBuf;

        if (len <= StackallocThreshold)
        {
            highestHighBuf = stackalloc double[len];
            lowestHighBuf = stackalloc double[len];
            highestLowBuf = stackalloc double[len];
            lowestLowBuf = stackalloc double[len];
        }
        else
        {
            rentedHH = ArrayPool<double>.Shared.Rent(len);
            rentedLH = ArrayPool<double>.Shared.Rent(len);
            rentedHL = ArrayPool<double>.Shared.Rent(len);
            rentedLL = ArrayPool<double>.Shared.Rent(len);
            highestHighBuf = rentedHH.AsSpan(0, len);
            lowestHighBuf = rentedLH.AsSpan(0, len);
            highestLowBuf = rentedHL.AsSpan(0, len);
            lowestLowBuf = rentedLL.AsSpan(0, len);
        }

        try
        {
            Highest.Batch(high, highestHighBuf, period);
            Lowest.Batch(high, lowestHighBuf, period);
            Highest.Batch(low, highestLowBuf, period);
            Lowest.Batch(low, lowestLowBuf, period);

            double emaAlpha = 2.0 / (period + 1);
            double hhsEma = 0.0;
            double llsEma = 0.0;
            double prevHigh = high[0];
            double prevLow = low[0];

            for (int i = 0; i < len; i++)
            {
                double h = high[i];
                double l = low[i];

                double hhRange = highestHighBuf[i] - lowestHighBuf[i];
                double llRange = highestLowBuf[i] - lowestLowBuf[i];

                double hhRaw;
                double llRaw;
                if (i == 0)
                {
                    // First bar — no previous to compare
                    hhRaw = 0.0;
                    llRaw = 0.0;
                }
                else
                {
                    hhRaw = (h > prevHigh && hhRange > 0.0)
                        ? 100.0 * (h - lowestHighBuf[i]) / hhRange
                        : 0.0;

                    llRaw = (l < prevLow && llRange > 0.0)
                        ? 100.0 * (highestLowBuf[i] - l) / llRange
                        : 0.0;
                }

                // EMA smoothing
                if (i == 0)
                {
                    hhsEma = hhRaw;
                    llsEma = llRaw;
                }
                else
                {
                    hhsEma = Math.FusedMultiplyAdd(emaAlpha, hhRaw - hhsEma, hhsEma);
                    llsEma = Math.FusedMultiplyAdd(emaAlpha, llRaw - llsEma, llsEma);
                }

                hhsOut[i] = hhsEma;
                llsOut[i] = llsEma;
                prevHigh = h;
                prevLow = l;
            }
        }
        finally
        {
            if (rentedHH != null) { ArrayPool<double>.Shared.Return(rentedHH); }
            if (rentedLH != null) { ArrayPool<double>.Shared.Return(rentedLH); }
            if (rentedHL != null) { ArrayPool<double>.Shared.Return(rentedHL); }
            if (rentedLL != null) { ArrayPool<double>.Shared.Return(rentedLL); }
        }
    }

    /// <summary>
    /// TBarSeries batch returning dual TSeries.
    /// </summary>
    public static (TSeries Hhs, TSeries Lls) Batch(TBarSeries source, int period = DefaultPeriod)
    {
        if (source == null || source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tH = new List<long>(len);
        var vH = new List<double>(len);
        var tL = new List<long>(len);
        var vL = new List<double>(len);

        CollectionsMarshal.SetCount(tH, len);
        CollectionsMarshal.SetCount(vH, len);
        CollectionsMarshal.SetCount(tL, len);
        CollectionsMarshal.SetCount(vL, len);

        Batch(source.HighValues, source.LowValues,
            CollectionsMarshal.AsSpan(vH),
            CollectionsMarshal.AsSpan(vL),
            period);

        var tSpan = CollectionsMarshal.AsSpan(tH);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tL));

        return (new TSeries(tH, vH), new TSeries(tL, vL));
    }

    /// <summary>
    /// Creates a hot indicator from historical data, ready for streaming.
    /// </summary>
    public static ((TSeries Hhs, TSeries Lls) Results, Hhlls Indicator) Calculate(
        TBarSeries source, int period = DefaultPeriod)
    {
        var indicator = new Hhlls(period);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
