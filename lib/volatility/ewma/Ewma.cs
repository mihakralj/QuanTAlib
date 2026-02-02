using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// EWMA: Exponentially Weighted Moving Average Volatility
/// </summary>
/// <remarks>
/// EWMA Volatility calculates volatility using an exponentially weighted moving average
/// of squared log returns. This approach gives more weight to recent observations while
/// still considering historical data, making it responsive to market changes.
///
/// Formula:
/// <c>r_t = ln(Close_t / Close_{t-1})</c>
/// <c>RMA_t = (RMA_{t-1} × (period - 1) + r²_t) / period</c>
/// <c>BiasCorrection = 1 - (1 - 1/period)^n</c>
/// <c>CorrectedVariance = RMA_t / BiasCorrection</c>
/// <c>EWMA = √(CorrectedVariance × AnnualPeriods)</c>
///
/// Key properties:
/// - Uses RMA (Running Moving Average) for exponential smoothing
/// - Includes bias correction for accurate early estimates
/// - Can be annualized or returned as periodic volatility
/// - More responsive than simple moving average approaches
/// </remarks>
[SkipLocalsInit]
public sealed class Ewma : AbstractBase
{
    private readonly int _period;
    private readonly bool _annualize;
    private readonly int _annualPeriods;
    private readonly double _decay;

    private const double MinPrice = 1e-10;
    private const double Epsilon = 1e-10;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double RawRmaSqRet,
        double BiasE,
        double PrevClose,
        double LastValid,
        int Count);
    private State _s;
    private State _ps;

    /// <summary>
    /// Creates EWMA Volatility indicator with specified parameters.
    /// </summary>
    /// <param name="period">The period for EWMA calculation (must be > 0)</param>
    /// <param name="annualize">Whether to annualize the volatility output (default: true)</param>
    /// <param name="annualPeriods">Number of periods in a year for annualization (default: 252 for daily data)</param>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
    public Ewma(int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (annualize && annualPeriods <= 0)
        {
            throw new ArgumentException("Annual periods must be greater than 0 when annualizing", nameof(annualPeriods));
        }

        _period = period;
        _annualize = annualize;
        _annualPeriods = annualPeriods;
        _decay = 1.0 - (1.0 / period);
        Name = annualize ? $"Ewma({period},{annualPeriods})" : $"Ewma({period})";
        WarmupPeriod = period;
        _s = new State(0.0, 1.0, double.NaN, 0.0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Creates EWMA Volatility indicator with specified source and parameters.
    /// </summary>
    public Ewma(ITValuePublisher source, int period = 20, bool annualize = true, int annualPeriods = 252)
        : this(period, annualize, annualPeriods)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has completed the warmup period.
    /// </summary>
    public override bool IsHot => _s.Count >= _period;

    /// <summary>
    /// Period for EWMA calculation.
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

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double close = input.Value;

        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        // Sanitize input - use state's LastValid for consistency
        double lastValid = double.IsFinite(s.LastValid) && s.LastValid > 0 ? s.LastValid : 1.0;
        if (!double.IsFinite(close) || close <= 0)
        {
            close = lastValid;
        }
        else if (isNew)
        {
            s.LastValid = close;
        }

        double safeClose = Math.Max(close, MinPrice);
        double safePrevClose = double.IsFinite(s.PrevClose) && s.PrevClose > 0 ? s.PrevClose : safeClose;

        // Calculate log return
        double logReturn = 0.0;
        if (safeClose > 0.0 && safePrevClose > 0.0)
        {
            logReturn = Math.Log(safeClose / safePrevClose);
        }

        double squaredReturn = logReturn * logReturn;

        // RMA calculation: raw_rma_sq_ret = (raw_rma_sq_ret * (period - 1) + squaredReturn) / period
        double rawRmaSqRet;
        double biasE;

        if (s.Count == 0)
        {
            // First value: initialize with squared return
            rawRmaSqRet = squaredReturn;
            biasE = _decay;
        }
        else
        {
            // RMA update: (prev * (period - 1) + current) / period
            rawRmaSqRet = Math.FusedMultiplyAdd(s.RawRmaSqRet, _period - 1, squaredReturn) / _period;
            // Update bias correction factor: e = (1 - alpha) * e_prev
            biasE = _decay * s.BiasE;
        }

        // Bias correction: corrected = raw / (1 - e)
        double biasCorrection = 1.0 - biasE;
        double correctedRmaSqRet = biasCorrection > Epsilon ? rawRmaSqRet / biasCorrection : rawRmaSqRet;

        // Ensure non-negative variance
        double currentEwmaSqReturns = Math.Max(correctedRmaSqRet, 0.0);

        // Calculate volatility
        double volatility = Math.Sqrt(currentEwmaSqReturns);

        // Annualize if requested
        double result = _annualize ? volatility * Math.Sqrt(_annualPeriods) : volatility;

        if (isNew)
        {
            s.RawRmaSqRet = rawRmaSqRet;
            s.BiasE = biasE;
            s.PrevClose = safeClose;
            s.Count++;
            _s = s;
        }

        if (!double.IsFinite(result))
        {
            result = 0.0;
        }

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
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

        Batch(source.Values, vSpan, _period, _annualize, _annualPeriods);
        source.Times.CopyTo(tSpan);

        // Update internal state to match final position
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
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
    public override void Reset()
    {
        _s = new State(0.0, 1.0, double.NaN, 0.0, 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Calculates EWMA Volatility for entire series.
    /// </summary>
    public static TSeries Calculate(TSeries source, int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
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
    /// Batch EWMA Volatility calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (annualize && annualPeriods <= 0)
        {
            throw new ArgumentException("Annual periods must be greater than 0 when annualizing", nameof(annualPeriods));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double alpha = 1.0 / period;
        double decay = 1.0 - alpha;
        double annualFactor = annualize ? Math.Sqrt(annualPeriods) : 1.0;

        double rawRmaSqRet = 0.0;
        double biasE = 1.0;
        double prevClose = double.NaN;
        double lastValidClose = 1.0;

        for (int i = 0; i < len; i++)
        {
            double close = source[i];

            // Sanitize input
            if (!double.IsFinite(close) || close <= 0)
            {
                close = lastValidClose;
            }
            else
            {
                lastValidClose = close;
            }

            double safeClose = Math.Max(close, MinPrice);
            double safePrevClose = double.IsFinite(prevClose) && prevClose > 0 ? prevClose : safeClose;

            // Calculate log return
            double logReturn = 0.0;
            if (safeClose > 0.0 && safePrevClose > 0.0)
            {
                logReturn = Math.Log(safeClose / safePrevClose);
            }

            double squaredReturn = logReturn * logReturn;

            // RMA calculation
            if (i == 0)
            {
                rawRmaSqRet = squaredReturn;
                biasE = decay;
            }
            else
            {
                rawRmaSqRet = Math.FusedMultiplyAdd(rawRmaSqRet, period - 1, squaredReturn) / period;
                biasE = decay * biasE;
            }

            // Bias correction
            double biasCorrection = 1.0 - biasE;
            double correctedRmaSqRet = biasCorrection > Epsilon ? rawRmaSqRet / biasCorrection : rawRmaSqRet;

            // Calculate volatility
            double currentEwmaSqReturns = Math.Max(correctedRmaSqRet, 0.0);
            double volatility = Math.Sqrt(currentEwmaSqReturns);
            double result = volatility * annualFactor;

            prevClose = safeClose;

            output[i] = double.IsFinite(result) ? result : 0.0;
        }
    }
}