using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CV: Conditional Volatility (GARCH(1,1))
/// </summary>
/// <remarks>
/// Conditional Volatility calculates GARCH(1,1) volatility, which models time-varying
/// volatility as a function of past squared returns and past variance. This captures
/// volatility clustering - the tendency for high volatility periods to be followed
/// by high volatility and low volatility periods to be followed by low volatility.
///
/// Formula:
/// <c>r_t = ln(Close_t / Close_{t-1})</c>
/// <c>σ²_t = ω + α × r²_{t-1} + β × σ²_{t-1}</c>
/// <c>CV = √(252 × σ²_t) × 100</c>
///
/// where:
/// - ω = (1 - α - β) × long-run variance (estimated during warmup)
/// - α = weight on previous squared return (innovation coefficient)
/// - β = weight on previous variance (persistence coefficient)
/// - α + β must be less than 1 for stationarity
///
/// Key properties:
/// - Models volatility clustering (heteroskedasticity)
/// - Mean-reverting to long-run variance
/// - Annualized and expressed as percentage
/// </remarks>
[SkipLocalsInit]
public sealed class Cv : AbstractBase
{
    private readonly int _period;
    private readonly double _alpha;
    private readonly double _beta;

    private const double DaysInYear = 252.0;
    private const double MinPrice = 1e-10;
    private const double DefaultVariance = 0.0001;
    private const double MinVariance = 1e-10;
    private const double MaxLogReturn = 0.2;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Omega,
        double LongRunVar,
        double PrevVariance,
        double PrevSquaredReturn,
        double PrevClose,
        double LastValid,
        int Count);
    private State _s;
    private State _ps;

    /// <summary>
    /// Creates CV with specified parameters.
    /// </summary>
    /// <param name="period">Initial period for long-run variance estimation (must be > 0)</param>
    /// <param name="alpha">Weight on previous squared return (0 &lt; alpha &lt; 1)</param>
    /// <param name="beta">Weight on previous variance (0 &lt; beta &lt; 1)</param>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
    public Cv(int period = 20, double alpha = 0.2, double beta = 0.7)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (alpha <= 0.0 || alpha >= 1.0)
        {
            throw new ArgumentException("Alpha must be between 0 and 1 (exclusive)", nameof(alpha));
        }
        if (beta <= 0.0 || beta >= 1.0)
        {
            throw new ArgumentException("Beta must be between 0 and 1 (exclusive)", nameof(beta));
        }
        if (alpha + beta >= 1.0)
        {
            throw new ArgumentException("Alpha + Beta must be less than 1 for stationarity", nameof(alpha));
        }

        _period = period;
        _alpha = alpha;
        _beta = beta;
        Name = $"Cv({period},{alpha:F2},{beta:F2})";
        WarmupPeriod = period + 1;
        _s = new State(0.0, 0.0, 0.0, 0.0, double.NaN, 0.0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Creates CV with specified source and parameters.
    /// </summary>
    public Cv(ITValuePublisher source, int period = 20, double alpha = 0.2, double beta = 0.7) : this(period, alpha, beta)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has completed the initial variance estimation period.
    /// </summary>
    public override bool IsHot => _s.Count >= _period;

    /// <summary>
    /// Period for initial variance estimation.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Alpha coefficient (innovation weight).
    /// </summary>
    public double Alpha => _alpha;

    /// <summary>
    /// Beta coefficient (persistence weight).
    /// </summary>
    public double Beta => _beta;
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

        // Clamp extreme returns
        if (Math.Abs(logReturn) > MaxLogReturn)
        {
            logReturn = Math.Sign(logReturn) * MaxLogReturn;
        }

        double squaredReturn = logReturn * logReturn;
        double variance;

        // Warmup phase: estimate long-run variance from squared returns
        if (s.Count < _period)
        {
            // Running mean of squared returns - use immutable calculation
            double newLongRunVar = Math.FusedMultiplyAdd(s.LongRunVar, s.Count, squaredReturn) / (s.Count + 1);
            variance = newLongRunVar;

            if (isNew)
            {
                s.LongRunVar = newLongRunVar;
                s.PrevVariance = newLongRunVar;
                s.PrevSquaredReturn = squaredReturn;
                s.PrevClose = safeClose;
                s.Count++;
            }
        }
        else
        {
            // Calculate omega based on stored LongRunVar (compute locally, don't store during !isNew)
            double omega = s.Omega;
            if (Math.Abs(omega) <= 0)
            {
                omega = (1.0 - _alpha - _beta) * s.LongRunVar;
            }

            // GARCH(1,1) variance update
            // For isNew=true: use PREVIOUS squared return (standard lagged GARCH)
            // For isNew=false: use CURRENT squared return (bar correction scenario)
            // σ²_t = ω + α × r² + β × σ²_{t-1}
            double r2ForVariance = isNew ? s.PrevSquaredReturn : squaredReturn;
            variance = Math.FusedMultiplyAdd(_alpha, r2ForVariance, Math.FusedMultiplyAdd(_beta, s.PrevVariance, omega));

            // For near-zero long-run variance (constant prices), allow variance to be exactly 0
            // Use tolerance check instead of exact equality due to floating-point precision
            double r2ForZeroCheck = isNew ? s.PrevSquaredReturn : squaredReturn;
            if (s.LongRunVar < 1e-15 && r2ForZeroCheck < 1e-15)
            {
                variance = 0.0;
            }
            else
            {
                variance = Math.Max(variance, MinVariance);
            }

            if (isNew)
            {
                // Only store omega on first GARCH calculation
                if (Math.Abs(s.Omega) <= 0)
                {
                    s.Omega = omega;
                }
                s.PrevVariance = variance;
                s.PrevSquaredReturn = squaredReturn;
                s.PrevClose = safeClose;
                s.Count++;
            }
        }

        // Only persist state changes if isNew
        if (isNew)
        {
            _s = s;
        }

        // Calculate annualized volatility as percentage
        double result = Math.Sqrt(DaysInYear * variance) * 100.0;

        if (!double.IsFinite(result))
        {
            result = 0.0;
        }

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
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

        Batch(source.Values, vSpan, _period, _alpha, _beta);
        source.Times.CopyTo(tSpan);

        // Update internal state to match final position
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
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
        _s = new State(0.0, 0.0, 0.0, 0.0, double.NaN, 0.0, 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Calculates CV for entire series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 20, double alpha = 0.2, double beta = 0.7)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (alpha <= 0.0 || alpha >= 1.0)
        {
            throw new ArgumentException("Alpha must be between 0 and 1 (exclusive)", nameof(alpha));
        }
        if (beta <= 0.0 || beta >= 1.0)
        {
            throw new ArgumentException("Beta must be between 0 and 1 (exclusive)", nameof(beta));
        }
        if (alpha + beta >= 1.0)
        {
            throw new ArgumentException("Alpha + Beta must be less than 1 for stationarity", nameof(alpha));
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, period, alpha, beta);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch CV calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 20, double alpha = 0.2, double beta = 0.7)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (alpha <= 0.0 || alpha >= 1.0)
        {
            throw new ArgumentException("Alpha must be between 0 and 1 (exclusive)", nameof(alpha));
        }
        if (beta <= 0.0 || beta >= 1.0)
        {
            throw new ArgumentException("Beta must be between 0 and 1 (exclusive)", nameof(beta));
        }
        if (alpha + beta >= 1.0)
        {
            throw new ArgumentException("Alpha + Beta must be less than 1 for stationarity", nameof(alpha));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double omega = 0.0;
        double longRunVar = 0.0;
        double prevVariance = DefaultVariance;
        double prevClose = double.NaN;
        double lastValidClose = 1.0;

        double prevSquaredReturn = 0.0;

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

            // Clamp extreme returns
            if (Math.Abs(logReturn) > MaxLogReturn)
            {
                logReturn = Math.Sign(logReturn) * MaxLogReturn;
            }

            double squaredReturn = logReturn * logReturn;

            // Warmup phase
            if (i < period)
            {
                longRunVar = Math.FusedMultiplyAdd(longRunVar, i, squaredReturn) / (i + 1);
                prevVariance = longRunVar;
            }
            else
            {
                // Calculate omega at the end of warmup
                if (i == period && Math.Abs(omega) <= 0)
                {
                    omega = (1.0 - alpha - beta) * longRunVar;
                }

                // GARCH(1,1) variance update using PREVIOUS squared return (lagged)
                double variance = Math.FusedMultiplyAdd(alpha, prevSquaredReturn, Math.FusedMultiplyAdd(beta, prevVariance, omega));
                
                // For zero long-run variance (constant prices), allow variance to be exactly 0
                if (Math.Abs(longRunVar) <= 0 && Math.Abs(prevSquaredReturn) <= 0)
                {
                    variance = 0.0;
                }
                else
                {
                    variance = Math.Max(variance, MinVariance);
                }
                prevVariance = variance;
            }

            prevSquaredReturn = squaredReturn;
            prevClose = safeClose;

            // Calculate annualized volatility as percentage
            double result = Math.Sqrt(DaysInYear * prevVariance) * 100.0;
            output[i] = double.IsFinite(result) ? result : 0.0;
        }
    }

    public static (TSeries Results, Cv Indicator) Calculate(TSeries source, int period = 20, double alpha = 0.2, double beta = 0.7)
    {
        var indicator = new Cv(period, alpha, beta);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}