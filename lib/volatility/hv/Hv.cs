// Historical Volatility (HV) Indicator
// Close-to-close volatility using standard deviation of log returns

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HV: Historical Volatility (Close-to-Close)
/// Calculates volatility as the standard deviation of log returns over a rolling window.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>Calculate log return: r_t = ln(price_t / price_{t-1})</item>
/// <item>Compute population standard deviation over period</item>
/// <item>If annualize: volatility × √(annualPeriods)</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>Uses only closing prices (simplest volatility measure)</item>
/// <item>Rolling window standard deviation</item>
/// <item>Optional annualization (default 252 trading days)</item>
/// <item>Baseline for comparing other volatility estimators</item>
/// </list>
///
/// <b>Sources:</b>
/// Standard financial literature. Close-to-close volatility is the traditional
/// method taught in finance textbooks.
/// </remarks>
[SkipLocalsInit]
public sealed class Hv : AbstractBase
{
    private readonly int _period;
    private readonly bool _annualize;
    private readonly int _annualPeriods;
    private readonly double _annualFactor;
    private readonly RingBuffer _buffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevPrice,
        double Sum,
        double SumSq,
        double LastValidReturn,
        double LastValue,
        int FillCount
    );
    private State _s;
    private State _ps;

    /// <summary>
    /// Initializes a new instance of the Hv class.
    /// </summary>
    /// <param name="period">The rolling window period (default 20).</param>
    /// <param name="annualize">Whether to annualize the volatility (default true).</param>
    /// <param name="annualPeriods">Number of periods per year (default 252).</param>
    /// <exception cref="ArgumentException">
    /// Thrown when period is less than 2, or annualPeriods is less than 1 when annualizing.
    /// </exception>
    public Hv(int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }
        if (annualize && annualPeriods <= 0)
        {
            throw new ArgumentException("Annual periods must be greater than 0 when annualizing", nameof(annualPeriods));
        }
        _period = period;
        _annualize = annualize;
        _annualPeriods = annualPeriods;
        _annualFactor = annualize ? Math.Sqrt(annualPeriods) : 1.0;
        _buffer = new RingBuffer(period);
        WarmupPeriod = period + 1; // Need period+1 prices to get period returns
        Name = $"Hv({period})";
        _s = new State(double.NaN, 0, 0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Initializes a new instance of the Hv class with a source.
    /// </summary>
    /// <param name="source">The data source for chaining.</param>
    /// <param name="period">The rolling window period (default 20).</param>
    /// <param name="annualize">Whether to annualize the volatility (default true).</param>
    /// <param name="annualPeriods">Number of periods per year (default 252).</param>
    public Hv(ITValuePublisher source, int period = 20, bool annualize = true, int annualPeriods = 252)
        : this(period, annualize, annualPeriods)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _s.FillCount >= _period;

    /// <summary>
    /// The rolling window period.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Whether volatility is annualized.
    /// </summary>
    public bool Annualize => _annualize;

    /// <summary>
    /// Number of periods per year for annualization.
    /// </summary>
    public int AnnualPeriods => _annualPeriods;

    /// <summary>
    /// Updates the indicator with a new price value.
    /// </summary>
    /// <param name="input">The input price value.</param>
    /// <param name="isNew">Whether this is a new bar or an update.</param>
    /// <returns>The calculated volatility value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return UpdateCore(input.Time, input.Value, isNew);
    }

    /// <summary>
    /// Updates the indicator with a new bar (uses Close price).
    /// </summary>
    /// <param name="bar">The input bar.</param>
    /// <param name="isNew">Whether this is a new bar or an update.</param>
    /// <returns>The calculated volatility value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return UpdateCore(bar.Time, bar.Close, isNew);
    }

    /// <summary>
    /// Updates the indicator with a bar series.
    /// </summary>
    /// <param name="source">The source bar series.</param>
    /// <returns>A TSeries containing the volatility values.</returns>
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

        // Extract close prices
        Span<double> closes = len <= 128 ? stackalloc double[len] : new double[len];

        for (int i = 0; i < len; i++)
        {
            closes[i] = source[i].Close;
            tSpan[i] = source[i].Time;
        }

        Batch(closes, vSpan, _period, _annualize, _annualPeriods);

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source[i].Time, source[i].Close), isNew: true);
        }

        return new TSeries(t, v);
    }
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period, _annualize, _annualPeriods);
        source.Times.CopyTo(tSpan);

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(long timeTicks, double price, bool isNew)
    {
        if (isNew)
        {
            _ps = _s;
            _buffer.Snapshot();
        }
        else
        {
            _s = _ps;
            _buffer.Restore();
        }

        var s = _s;

        // Handle non-finite price
        if (!double.IsFinite(price) || price <= 0)
        {
            // Can't compute return, output last value
            Last = new TValue(timeTicks, s.LastValue);
            PubEvent(Last, isNew);
            return Last;
        }

        double volatility;

        // First price - no return yet
        if (double.IsNaN(s.PrevPrice))
        {
            s = s with { PrevPrice = price };
            volatility = 0;
        }
        else
        {
            // Calculate log return
            double logReturn = Math.Log(price / s.PrevPrice);

            if (!double.IsFinite(logReturn))
            {
                logReturn = s.LastValidReturn;
            }
            else
            {
                s = s with { LastValidReturn = logReturn };
            }

            // Always use Add() after Snapshot/Restore pattern
            // When isNew=false, Restore() reverts buffer to pre-Add state,
            // so we need Add() (not UpdateNewest) to put the value back
            _buffer.Add(logReturn);

            // Recalculate sums from buffer - this ensures correctness after corrections
            double sum = 0;
            double sumSq = 0;
            int fillCount = _buffer.Count;

            for (int i = 0; i < fillCount; i++)
            {
                double r = _buffer[i];
                sum += r;
                sumSq += r * r;
            }

            // Calculate population variance: E[X²] - E[X]²
            if (fillCount > 1)
            {
                double mean = sum / fillCount;
                double variance = (sumSq / fillCount) - (mean * mean);
                variance = Math.Max(0.0, variance); // Ensure non-negative
                volatility = Math.Sqrt(variance) * _annualFactor;
            }
            else
            {
                volatility = 0;
            }

            s = s with
            {
                PrevPrice = price,
                Sum = sum,
                SumSq = sumSq,
                FillCount = fillCount
            };
        }

        if (!double.IsFinite(volatility))
        {
            volatility = s.LastValue;
        }
        else
        {
            s = s with { LastValue = volatility };
        }

        _s = s;

        Last = new TValue(timeTicks, volatility);
        PubEvent(Last, isNew);
        return Last;
    }
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }
    public override void Reset()
    {
        _s = new State(double.NaN, 0, 0, 0, 0, 0);
        _ps = _s;
        _buffer.Clear();
        Last = default;
    }

    /// <summary>
    /// Calculates Historical Volatility for a price series (static).
    /// </summary>
    /// <param name="source">The source price series.</param>
    /// <param name="period">The rolling window period.</param>
    /// <param name="annualize">Whether to annualize.</param>
    /// <param name="annualPeriods">Periods per year.</param>
    /// <returns>A TSeries containing the volatility values.</returns>
    public static TSeries Batch(TSeries source, int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }
        if (annualize && annualPeriods <= 0)
        {
            throw new ArgumentException("Annual periods must be greater than 0 when annualizing", nameof(annualPeriods));
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, period, annualize, annualPeriods);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates HV for a bar series (static).
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        var hv = new Hv(period, annualize, annualPeriods);
        return hv.Update(source);
    }

    /// <summary>
    /// Batch calculation using spans.
    /// </summary>
    /// <param name="prices">Price values.</param>
    /// <param name="output">Output volatility values.</param>
    /// <param name="period">The rolling window period.</param>
    /// <param name="annualize">Whether to annualize.</param>
    /// <param name="annualPeriods">Periods per year.</param>
    public static void Batch(
        ReadOnlySpan<double> prices,
        Span<double> output,
        int period = 20,
        bool annualize = true,
        int annualPeriods = 252)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }
        if (annualize && annualPeriods <= 0)
        {
            throw new ArgumentException("Annual periods must be greater than 0 when annualizing", nameof(annualPeriods));
        }
        if (output.Length < prices.Length)
        {
            throw new ArgumentException("Output span must be at least as long as prices span", nameof(output));
        }

        int len = prices.Length;
        if (len == 0)
        {
            return;
        }

        double annualFactor = annualize ? Math.Sqrt(annualPeriods) : 1.0;

        // Use a ring buffer for log returns
        Span<double> buffer = period <= 256 ? stackalloc double[period] : new double[period];
        int head = 0;
        int fillCount = 0;
        double sum = 0;
        double sumSq = 0;
        double prevPrice = double.NaN;
        double lastValidReturn = 0;
        double lastValue = 0;

        for (int i = 0; i < len; i++)
        {
            double price = prices[i];

            // First price - no return
            if (double.IsNaN(prevPrice))
            {
                prevPrice = price;
                output[i] = 0;
                continue;
            }

            // Handle invalid price
            if (!double.IsFinite(price) || price <= 0)
            {
                output[i] = lastValue;
                continue;
            }

            // Calculate log return
            double logReturn = Math.Log(price / prevPrice);
            prevPrice = price;

            if (!double.IsFinite(logReturn))
            {
                logReturn = lastValidReturn;
            }
            else
            {
                lastValidReturn = logReturn;
            }

            // Remove oldest if buffer is full
            if (fillCount == period)
            {
                double oldest = buffer[head];
                sum -= oldest;
                sumSq -= oldest * oldest;
            }
            else
            {
                fillCount++;
            }

            // Add new return
            buffer[head] = logReturn;
            head = (head + 1) % period;
            sum += logReturn;
            sumSq += logReturn * logReturn;

            // Calculate volatility
            double volatility;
            if (fillCount > 1)
            {
                double mean = sum / fillCount;
                double variance = (sumSq / fillCount) - (mean * mean);
                variance = Math.Max(0.0, variance);
                volatility = Math.Sqrt(variance) * annualFactor;
            }
            else
            {
                volatility = 0;
            }

            if (!double.IsFinite(volatility))
            {
                volatility = lastValue;
            }
            else
            {
                lastValue = volatility;
            }

            output[i] = volatility;
        }
    }

    public static (TSeries Results, Hv Indicator) Calculate(TSeries source, int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        var indicator = new Hv(period, annualize, annualPeriods);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

}
