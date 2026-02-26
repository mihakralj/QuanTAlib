using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ETHERM: Elder's Thermometer
/// Measures bar-to-bar range extension to quantify market volatility.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>highDiff = |High - prevHigh|, lowDiff = |prevLow - Low|</item>
/// <item>Inside bar (High &lt; prevHigh AND Low &gt; prevLow) → Temperature = 0</item>
/// <item>Otherwise Temperature = max(highDiff, lowDiff)</item>
/// <item>Signal = EMA(Temperature, period) with bias compensation</item>
/// </list>
///
/// <b>Sources:</b>
/// Dr. Alexander Elder (2002). "Come Into My Trading Room" p.162
/// </remarks>
/// <seealso href="Etherm.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Etherm : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevHigh,
        double PrevLow,
        double Ema,
        double E,
        double LastValidHigh,
        double LastValidLow,
        double LastValidTemp,
        int Count
    )
    {
        public bool IsCompensated => E <= 1e-10;
    }

    private State _s;
    private State _ps;
    private readonly double _alpha;
    private readonly double _decay;

    /// <summary>
    /// Creates ETHERM with specified EMA smoothing period.
    /// </summary>
    /// <param name="period">EMA period for signal line (must be &gt; 0, default 22)</param>
    public Etherm(int period = 22)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _alpha = 2.0 / (period + 1);
        _decay = 1.0 - _alpha;
        Name = $"Etherm({period})";
        WarmupPeriod = period;
        _s = new State(double.NaN, double.NaN, 0, 1.0, 0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Creates ETHERM with specified source and period.
    /// </summary>
    public Etherm(ITValuePublisher source, int period = 22) : this(period)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// IsHot when bias compensator E &lt;= 0.05 (95% coverage).
    /// </summary>
    public override bool IsHot => _s.E <= 0.05;

    /// <summary>
    /// The current EMA signal line value.
    /// </summary>
    public double Signal { get; private set; }

    /// <summary>
    /// Updates the indicator with a TBar input (preferred method).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return UpdateCore(bar.Time, bar.High, bar.Low, isNew);
    }

    /// <summary>
    /// Updates the indicator with a TValue input.
    /// Treats the value as H=L (degenerate case, zero temperature).
    /// Prefer Update(TBar) for standard OHLC data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return UpdateCore(input.Time, input.Value, input.Value, isNew);
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

        // Stream each bar to build state
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
        // TSeries has no OHLC — treat values as H=L (degenerate case)
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
        _s = new State(double.NaN, double.NaN, 0, 1.0, 0, 0, 0, 0);
        _ps = _s;
        Signal = 0;
        Last = default;
    }

    /// <summary>
    /// Calculates ETHERM for the entire bar series using a new instance.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period = 22)
    {
        var etherm = new Etherm(period);
        return etherm.Update(source);
    }

    /// <summary>
    /// Span-based batch calculation for high and low price arrays.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="output">Output thermometer temperature values.</param>
    /// <param name="period">EMA smoothing period (used for signal, output is raw temp).</param>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        Span<double> output,
        int period = 22)
    {
        int len = high.Length;
        if (low.Length != len)
        {
            throw new ArgumentException("High and low spans must have the same length", nameof(low));
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

        double lastValidHigh = 0;
        double lastValidLow = 0;
        double lastValidTemp = 0;

        for (int i = 0; i < len; i++)
        {
            double h = high[i];
            double l = low[i];

            // Handle non-finite values
            if (!double.IsFinite(h))
            {
                h = lastValidHigh;
            }
            else
            {
                lastValidHigh = h;
            }

            if (!double.IsFinite(l))
            {
                l = lastValidLow;
            }
            else
            {
                lastValidLow = l;
            }

            double temp;
            if (i == 0)
            {
                // First bar: no previous bar, temp = 0
                temp = 0;
            }
            else
            {
                double prevH = high[i - 1];
                double prevL = low[i - 1];
                if (!double.IsFinite(prevH))
                {
                    prevH = lastValidHigh;
                }
                if (!double.IsFinite(prevL))
                {
                    prevL = lastValidLow;
                }

                double highDiff = Math.Abs(h - prevH);
                double lowDiff = Math.Abs(prevL - l);
                bool isInsideBar = h < prevH && l > prevL;
                temp = isInsideBar ? 0 : Math.Max(highDiff, lowDiff);
            }

            if (!double.IsFinite(temp) || temp < 0)
            {
                temp = lastValidTemp;
            }
            else
            {
                lastValidTemp = temp;
            }

            output[i] = temp;
        }
    }

    /// <summary>
    /// Calculates ETHERM and returns both results and the indicator instance.
    /// </summary>
    public static (TSeries Results, Etherm Indicator) Calculate(TBarSeries source, int period = 22)
    {
        var indicator = new Etherm(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    // ---- Private implementation ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(long timeTicks, double high, double low, bool isNew)
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

        // Handle non-finite values — use last valid
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

        // Calculate thermometer temperature
        double temp;
        if (s.Count == 0 || !double.IsFinite(s.PrevHigh))
        {
            // First bar: no previous bar to compare, temperature = 0
            temp = 0;
        }
        else
        {
            double highDiff = Math.Abs(high - s.PrevHigh);
            double lowDiff = Math.Abs(s.PrevLow - low);
            bool isInsideBar = high < s.PrevHigh && low > s.PrevLow;
            temp = isInsideBar ? 0 : Math.Max(highDiff, lowDiff);
        }

        // NaN/Infinity safety on computed temp
        if (!double.IsFinite(temp) || temp < 0)
        {
            temp = s.LastValidTemp;
        }
        else
        {
            s.LastValidTemp = temp;
        }

        // EMA smoothing with bias compensation (FMA pattern)
        // ema = ema * decay + alpha * temp
        s.Ema = Math.FusedMultiplyAdd(s.Ema, _decay, _alpha * temp);
        s.E *= _decay;

        double signal = s.IsCompensated ? s.Ema : s.Ema / (1.0 - s.E);

        // Update previous bar state
        s.PrevHigh = high;
        s.PrevLow = low;
        if (isNew)
        {
            s.Count++;
        }

        _s = s;

        Signal = signal;
        Last = new TValue(timeTicks, temp);
        PubEvent(Last, isNew);
        return Last;
    }
}
