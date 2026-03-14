using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// GHLA: Gann High-Low Activator
/// SMA-based trailing stop with three-state hysteresis trend detection.
/// Output follows SMA(Low) during uptrends and SMA(High) during downtrends.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>SMA_high = running sum of last N highs / N</item>
/// <item>SMA_low = running sum of last N lows / N</item>
/// <item>Close &gt; SMA_high → trend = +1 (bullish), output = SMA_low</item>
/// <item>Close &lt; SMA_low → trend = -1 (bearish), output = SMA_high</item>
/// <item>Between both SMAs → retain previous trend (hysteresis)</item>
/// </list>
///
/// <b>Sources:</b>
/// Robert Krausz (1998). "The New Gann Swing Chartist" — Stocks &amp; Commodities V.16:1
/// </remarks>
/// <seealso href="Ghla.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Ghla : AbstractBase
{
    private readonly RingBuffer _highBuffer;
    private readonly RingBuffer _lowBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double HighSum,
        double LowSum,
        double HighSumComp,
        double LowSumComp,
        int Trend,
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose
    );

    private State _s;
    private State _ps;

    /// <summary>
    /// Creates GHLA with specified SMA period.
    /// </summary>
    /// <param name="period">SMA lookback period (must be &gt; 0, default 13)</param>
    public Ghla(int period = 13)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _highBuffer = new RingBuffer(period);
        _lowBuffer = new RingBuffer(period);
        Name = $"Ghla({period})";
        WarmupPeriod = period;
        _s = default;
        _ps = _s;
    }

    /// <summary>
    /// Creates GHLA with specified source and period.
    /// </summary>
    public Ghla(ITValuePublisher source, int period = 13) : this(period)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True when both SMA buffers are full.
    /// </summary>
    public override bool IsHot => _highBuffer.IsFull;

    /// <summary>
    /// The current trend direction: +1 bullish, -1 bearish, 0 undetermined.
    /// </summary>
    public int Trend => _s.Trend;

    /// <summary>
    /// Updates the indicator with a TBar input (preferred method).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return UpdateCore(bar.Time, bar.High, bar.Low, bar.Close, isNew);
    }

    /// <summary>
    /// Updates the indicator with a TValue input.
    /// Treats the value as H=L=C (degenerate case, always neutral zone).
    /// Prefer Update(TBar) for standard OHLC data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return UpdateCore(input.Time, input.Value, input.Value, input.Value, isNew);
    }

    /// <summary>
    /// Updates the indicator with a bar series.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        for (int i = 0; i < len; i++)
        {
            tSpan[i] = source[i].Time;
        }

        for (int i = 0; i < len; i++)
        {
            var result = Update(source[i], isNew: true);
            vSpan[i] = result.Value;
        }

        return new TSeries(t, v);
    }

    /// <inheritdoc/>
    public override TSeries Update(TSeries source)
    {
        // TSeries has no OHLC — treat values as H=L=C (degenerate case)
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        var values = source.Values;
        var times = source.Times;

        for (int i = 0; i < len; i++)
        {
            tSpan[i] = times[i];
            var result = Update(new TValue(times[i], values[i]), isNew: true);
            vSpan[i] = result.Value;
        }

        return new TSeries(t, v);
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _highBuffer.Clear();
        _lowBuffer.Clear();
        _s = default;
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Calculates GHLA for the entire bar series using a new instance.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period = 13)
    {
        var ghla = new Ghla(period);
        return ghla.Update(source);
    }

    /// <summary>
    /// Span-based batch calculation for high, low, and close arrays.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output activator values.</param>
    /// <param name="period">SMA lookback period.</param>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int period = 13)
    {
        int len = high.Length;
        if (low.Length != len)
        {
            throw new ArgumentException("High and low spans must have the same length", nameof(low));
        }
        if (close.Length != len)
        {
            throw new ArgumentException("High and close spans must have the same length", nameof(close));
        }
        if (output.Length < len)
        {
            throw new ArgumentException("Output span must be at least as long as input spans", nameof(output));
        }
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(high, low, close, output, period);
    }

    /// <summary>
    /// Calculates GHLA and returns both results and the indicator instance.
    /// </summary>
    public static (TSeries Results, Ghla Indicator) Calculate(TBarSeries source, int period = 13)
    {
        var indicator = new Ghla(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    // ---- Private implementation ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(long timeTicks, double high, double low, double close, bool isNew)
    {
        // Snapshot/restore for bar correction
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        // Handle non-finite values — use last valid per component
        if (!double.IsFinite(high))
        {
            high = s.LastValidHigh;
        }
        else
        {
            s.LastValidHigh = high;
        }

        if (!double.IsFinite(low))
        {
            low = s.LastValidLow;
        }
        else
        {
            s.LastValidLow = low;
        }

        if (!double.IsFinite(close))
        {
            close = s.LastValidClose;
        }
        else
        {
            s.LastValidClose = close;
        }

        // Update running SMA sums via ring buffers
        if (isNew)
        {
            // High buffer — Kahan compensated
            double highRemoved = _highBuffer.Count == _highBuffer.Capacity ? _highBuffer.Oldest : 0.0;
            double hDelta = high - highRemoved - s.HighSumComp;
            double hNewSum = s.HighSum + hDelta;
            s.HighSumComp = (hNewSum - s.HighSum) - hDelta;
            s.HighSum = hNewSum;
            _highBuffer.Add(high);

            // Low buffer — Kahan compensated
            double lowRemoved = _lowBuffer.Count == _lowBuffer.Capacity ? _lowBuffer.Oldest : 0.0;
            double lDelta = low - lowRemoved - s.LowSumComp;
            double lNewSum = s.LowSum + lDelta;
            s.LowSumComp = (lNewSum - s.LowSum) - lDelta;
            s.LowSum = lNewSum;
            _lowBuffer.Add(low);
        }
        else
        {
            // Bar correction: update newest value in both buffers
            _highBuffer.UpdateNewest(high);
            s.HighSum = _highBuffer.Sum;
            s.HighSumComp = 0;

            _lowBuffer.UpdateNewest(low);
            s.LowSum = _lowBuffer.Sum;
            s.LowSumComp = 0;
        }

        // Compute SMAs
        int count = _highBuffer.Count;
        double smaHigh = count > 0 ? s.HighSum / count : 0.0;
        double smaLow = count > 0 ? s.LowSum / count : 0.0;

        // Three-state hysteresis trend detection
        if (s.Trend == 0)
        {
            // Seed: classify first bar
            if (close >= smaHigh)
            {
                s.Trend = 1;
            }
            else if (close <= smaLow)
            {
                s.Trend = -1;
            }
            else
            {
                s.Trend = 1; // default bullish per Pine reference
            }
        }

        if (close > smaHigh)
        {
            s.Trend = 1;
        }
        else if (close < smaLow)
        {
            s.Trend = -1;
        }
        // else: retain previous trend (hysteresis zone)

        // Select activator: bullish → SMA(Low), bearish → SMA(High)
        double activator = s.Trend == 1 ? smaLow : smaHigh;

        _s = s;

        Last = new TValue(timeTicks, activator);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int period)
    {
        int len = high.Length;

        const int StackAllocThreshold = 256;

        // High circular buffer
        double[]? rentedHigh = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> highBuf = rentedHigh != null
            ? rentedHigh.AsSpan(0, period)
            : stackalloc double[period];

        // Low circular buffer
        double[]? rentedLow = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> lowBuf = rentedLow != null
            ? rentedLow.AsSpan(0, period)
            : stackalloc double[period];

        try
        {
            double highSum = 0;
            double highSumComp = 0;
            double lowSum = 0;
            double lowSumComp = 0;
            double lastValidHigh = 0;
            double lastValidLow = 0;
            double lastValidClose = 0;
            int highIdx = 0;
            int lowIdx = 0;
            int filled = 0;
            int trend = 0;

            // Seed lastValid values
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(high[k]))
                {
                    lastValidHigh = high[k];
                    break;
                }
            }
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(low[k]))
                {
                    lastValidLow = low[k];
                    break;
                }
            }
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(close[k]))
                {
                    lastValidClose = close[k];
                    break;
                }
            }

            for (int i = 0; i < len; i++)
            {
                double h = high[i];
                double l = low[i];
                double c = close[i];

                if (double.IsFinite(h))
                {
                    lastValidHigh = h;
                }
                else
                {
                    h = lastValidHigh;
                }

                if (double.IsFinite(l))
                {
                    lastValidLow = l;
                }
                else
                {
                    l = lastValidLow;
                }

                if (double.IsFinite(c))
                {
                    lastValidClose = c;
                }
                else
                {
                    c = lastValidClose;
                }

                // Kahan-compensated update for high buffer
                {
                    double deltaH = h - (filled >= period ? highBuf[highIdx] : 0);
                    double yH = deltaH - highSumComp;
                    double tH = highSum + yH;
                    highSumComp = (tH - highSum) - yH;
                    highSum = tH;
                }
                highBuf[highIdx] = h;
                highIdx++;
                if (highIdx >= period)
                {
                    highIdx = 0;
                }

                // Kahan-compensated update for low buffer
                {
                    double deltaL = l - (filled >= period ? lowBuf[lowIdx] : 0);
                    double yL = deltaL - lowSumComp;
                    double tL = lowSum + yL;
                    lowSumComp = (tL - lowSum) - yL;
                    lowSum = tL;
                }
                lowBuf[lowIdx] = l;
                lowIdx++;
                if (lowIdx >= period)
                {
                    lowIdx = 0;
                }

                if (filled < period)
                {
                    filled++;
                }

                double smaH = highSum / filled;
                double smaL = lowSum / filled;

                // Hysteresis
                if (trend == 0)
                {
                    if (c >= smaH)
                    {
                        trend = 1;
                    }
                    else if (c <= smaL)
                    {
                        trend = -1;
                    }
                    else
                    {
                        trend = 1; // default bullish per Pine reference
                    }
                }

                if (c > smaH)
                {
                    trend = 1;
                }
                else if (c < smaL)
                {
                    trend = -1;
                }

                output[i] = trend == 1 ? smaL : smaH;
            }
        }
        finally
        {
            if (rentedHigh != null)
            {
                ArrayPool<double>.Shared.Return(rentedHigh);
            }
            if (rentedLow != null)
            {
                ArrayPool<double>.Shared.Return(rentedLow);
            }
        }
    }
}
