// Realized Volatility (RV) Indicator
// Sum of squared log returns, then sqrt, smoothed with SMA

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RV: Realized Volatility
/// Calculates volatility as the square root of realized variance (sum of squared log returns),
/// smoothed with a Simple Moving Average.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>Calculate log return: r_t = ln(price_t / price_{t-1})</item>
/// <item>Compute realized variance: RV_t = Σ(r_i²) for returns in window</item>
/// <item>Take square root: volatility_t = √(RV_t)</item>
/// <item>Smooth with SMA over smoothing period</item>
/// <item>If annualize: volatility × √(annualPeriods)</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>Based on sum of squared returns (not variance-adjusted)</item>
/// <item>More responsive to recent volatility bursts</item>
/// <item>SMA smoothing reduces noise</item>
/// <item>Standard measure in academic finance and risk management</item>
/// </list>
///
/// <b>Sources:</b>
/// Andersen, T.G., Bollerslev, T. (1998). "Answering the Skeptics: Yes, Standard
/// Volatility Models Do Provide Accurate Forecasts". International Economic Review.
/// </remarks>
[SkipLocalsInit]
public sealed class Rv : AbstractBase
{
    private readonly int _period;
    private readonly int _smoothingPeriod;
    private readonly bool _annualize;
    private readonly int _annualPeriods;
    private readonly double _annualFactor;
    private readonly RingBuffer _returnBuffer;
    private readonly RingBuffer _volatilityBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevPrice,
        double LastValidReturn,
        double LastValue,
        int ReturnCount
    );
    private State _s;
    private State _ps;

    /// <summary>
    /// Initializes a new instance of the Rv class.
    /// </summary>
    /// <param name="period">The window for calculating realized variance (default 5).</param>
    /// <param name="smoothingPeriod">The SMA smoothing period (default 20).</param>
    /// <param name="annualize">Whether to annualize the volatility (default true).</param>
    /// <param name="annualPeriods">Number of periods per year (default 252).</param>
    /// <exception cref="ArgumentException">
    /// Thrown when period or smoothingPeriod is less than 1, or annualPeriods is less than 1 when annualizing.
    /// </exception>
    public Rv(int period = 5, int smoothingPeriod = 20, bool annualize = true, int annualPeriods = 252)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1", nameof(period));
        }
        if (smoothingPeriod < 1)
        {
            throw new ArgumentException("Smoothing period must be at least 1", nameof(smoothingPeriod));
        }
        if (annualize && annualPeriods <= 0)
        {
            throw new ArgumentException("Annual periods must be greater than 0 when annualizing", nameof(annualPeriods));
        }
        _period = period;
        _smoothingPeriod = smoothingPeriod;
        _annualize = annualize;
        _annualPeriods = annualPeriods;
        _annualFactor = annualize ? Math.Sqrt(annualPeriods) : 1.0;
        _returnBuffer = new RingBuffer(period);
        _volatilityBuffer = new RingBuffer(smoothingPeriod);
        WarmupPeriod = period + smoothingPeriod; // Need returns + smoothing
        Name = $"Rv({period},{smoothingPeriod})";
        _s = new State(double.NaN, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Initializes a new instance of the Rv class with a source.
    /// </summary>
    /// <param name="source">The data source for chaining.</param>
    /// <param name="period">The window for calculating realized variance (default 5).</param>
    /// <param name="smoothingPeriod">The SMA smoothing period (default 20).</param>
    /// <param name="annualize">Whether to annualize the volatility (default true).</param>
    /// <param name="annualPeriods">Number of periods per year (default 252).</param>
    public Rv(ITValuePublisher source, int period = 5, int smoothingPeriod = 20, bool annualize = true, int annualPeriods = 252)
        : this(period, smoothingPeriod, annualize, annualPeriods)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _volatilityBuffer.Count >= _smoothingPeriod;

    /// <summary>
    /// The window for calculating realized variance.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// The SMA smoothing period.
    /// </summary>
    public int SmoothingPeriod => _smoothingPeriod;

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

        Batch(closes, vSpan, _period, _smoothingPeriod, _annualize, _annualPeriods);

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source[i].Time, source[i].Close), isNew: true);
        }

        return new TSeries(t, v);
    }

    /// <inheritdoc/>
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period, _smoothingPeriod, _annualize, _annualPeriods);
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
            _returnBuffer.Snapshot();
            _volatilityBuffer.Snapshot();
        }
        else
        {
            _s = _ps;
            _returnBuffer.Restore();
            _volatilityBuffer.Restore();
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

        double result;

        // First price - no return yet
        if (double.IsNaN(s.PrevPrice))
        {
            s = s with { PrevPrice = price };
            result = 0;
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

            // Add squared return to buffer
            double squaredReturn = logReturn * logReturn;
            _returnBuffer.Add(squaredReturn);

            // Calculate realized variance (sum of squared returns)
            double sumSquaredReturns = 0;
            for (int i = 0; i < _returnBuffer.Count; i++)
            {
                sumSquaredReturns += _returnBuffer[i];
            }

            // Raw volatility = sqrt(realized variance)
            double rawVolatility = Math.Sqrt(sumSquaredReturns);

            // Add to smoothing buffer
            _volatilityBuffer.Add(rawVolatility);

            // Calculate SMA of volatilities
            double sumVol = 0;
            for (int i = 0; i < _volatilityBuffer.Count; i++)
            {
                sumVol += _volatilityBuffer[i];
            }
            double smoothedVolatility = sumVol / _volatilityBuffer.Count;

            // Apply annualization
            result = smoothedVolatility * _annualFactor;

            s = s with
            {
                PrevPrice = price,
                ReturnCount = s.ReturnCount + 1
            };
        }

        if (!double.IsFinite(result))
        {
            result = s.LastValue;
        }
        else
        {
            s = s with { LastValue = result };
        }

        _s = s;

        Last = new TValue(timeTicks, result);
        PubEvent(Last, isNew);
        return Last;
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
    public override void Reset()
    {
        _s = new State(double.NaN, 0, 0, 0);
        _ps = _s;
        _returnBuffer.Clear();
        _volatilityBuffer.Clear();
        Last = default;
    }

    /// <summary>
    /// Calculates Realized Volatility for a price series (static).
    /// </summary>
    /// <param name="source">The source price series.</param>
    /// <param name="period">The window for realized variance.</param>
    /// <param name="smoothingPeriod">The SMA smoothing period.</param>
    /// <param name="annualize">Whether to annualize.</param>
    /// <param name="annualPeriods">Periods per year.</param>
    /// <returns>A TSeries containing the volatility values.</returns>
    public static TSeries Calculate(TSeries source, int period = 5, int smoothingPeriod = 20, bool annualize = true, int annualPeriods = 252)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1", nameof(period));
        }
        if (smoothingPeriod < 1)
        {
            throw new ArgumentException("Smoothing period must be at least 1", nameof(smoothingPeriod));
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

        Batch(source.Values, vSpan, period, smoothingPeriod, annualize, annualPeriods);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates RV for a bar series (static).
    /// </summary>
    public static TSeries Calculate(TBarSeries source, int period = 5, int smoothingPeriod = 20, bool annualize = true, int annualPeriods = 252)
    {
        var rv = new Rv(period, smoothingPeriod, annualize, annualPeriods);
        return rv.Update(source);
    }

    /// <summary>
    /// Batch calculation using spans.
    /// </summary>
    /// <param name="prices">Price values.</param>
    /// <param name="output">Output volatility values.</param>
    /// <param name="period">The window for realized variance.</param>
    /// <param name="smoothingPeriod">The SMA smoothing period.</param>
    /// <param name="annualize">Whether to annualize.</param>
    /// <param name="annualPeriods">Periods per year.</param>
    public static void Batch(
        ReadOnlySpan<double> prices,
        Span<double> output,
        int period = 5,
        int smoothingPeriod = 20,
        bool annualize = true,
        int annualPeriods = 252)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1", nameof(period));
        }
        if (smoothingPeriod < 1)
        {
            throw new ArgumentException("Smoothing period must be at least 1", nameof(smoothingPeriod));
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

        // Ring buffers for squared returns and raw volatilities
        Span<double> returnBuffer = period <= 128 ? stackalloc double[period] : new double[period];
        Span<double> volBuffer = smoothingPeriod <= 128 ? stackalloc double[smoothingPeriod] : new double[smoothingPeriod];

        int returnHead = 0;
        int returnCount = 0;
        int volHead = 0;
        int volCount = 0;

        double prevPrice = double.NaN;
        double lastValidReturn = 0;
        double lastValue = 0;
        double sumSquaredReturns = 0;
        double sumVol = 0;

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

            double squaredReturn = logReturn * logReturn;

            // Update return buffer
            if (returnCount == period)
            {
                sumSquaredReturns -= returnBuffer[returnHead];
            }
            else
            {
                returnCount++;
            }
            returnBuffer[returnHead] = squaredReturn;
            returnHead = (returnHead + 1) % period;
            sumSquaredReturns += squaredReturn;

            // Raw volatility
            double rawVolatility = Math.Sqrt(sumSquaredReturns);

            // Update volatility buffer for SMA
            if (volCount == smoothingPeriod)
            {
                sumVol -= volBuffer[volHead];
            }
            else
            {
                volCount++;
            }
            volBuffer[volHead] = rawVolatility;
            volHead = (volHead + 1) % smoothingPeriod;
            sumVol += rawVolatility;

            // Smoothed volatility
            double smoothedVolatility = sumVol / volCount;
            double result = smoothedVolatility * annualFactor;

            if (!double.IsFinite(result))
            {
                result = lastValue;
            }
            else
            {
                lastValue = result;
            }

            output[i] = result;
        }
    }
}