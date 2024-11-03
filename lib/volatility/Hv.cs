using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// HV: Historical Volatility
/// A statistical measure that calculates the dispersion of returns over time,
/// providing insights into past price variability. Historical volatility is
/// fundamental to options pricing and risk assessment.
/// </summary>
/// <remarks>
/// The HV calculation process:
/// 1. Computes daily log returns
/// 2. Calculates standard deviation
/// 3. Annualizes if specified
/// 4. Uses sample variance formula
///
/// Key characteristics:
/// - Backward-looking measure
/// - Log-return based
/// - Optional annualization
/// - Sample-based calculation
/// - Trading-day adjusted
///
/// Formula:
/// HV = √[(Σ(ln(P[t]/P[t-1]) - μ)²)/(n-1)] * √252
/// where:
/// P = price
/// μ = mean of log returns
/// n = number of observations
/// 252 = trading days per year
///
/// Market Applications:
/// - Options pricing
/// - Risk assessment
/// - Trading ranges
/// - Portfolio management
/// - Volatility trading
///
/// Sources:
///     Black-Scholes Option Pricing Model
///     https://en.wikipedia.org/wiki/Volatility_(finance)
///
/// Note: Assumes 252 trading days for annualization
/// </remarks>
[SkipLocalsInit]
public sealed class Hv : AbstractBase
{
    private readonly int Period;
    private readonly bool IsAnnualized;
    private readonly CircularBuffer _buffer;
    private readonly CircularBuffer _logReturns;
    private double _previousClose;
    private const int TradingDaysPerYear = 252;
    private const double Epsilon = 1e-10;

    /// <param name="period">The number of periods for volatility calculation.</param>
    /// <param name="isAnnualized">Whether to annualize the result (default true).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hv(int period, bool isAnnualized = true)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2.");
        }
        Period = period;
        IsAnnualized = isAnnualized;
        WarmupPeriod = period + 1;  // Need extra point for first return
        _buffer = new CircularBuffer(period + 1);
        _logReturns = new CircularBuffer(period);
        Name = $"Historical(period={period}, annualized={isAnnualized})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for volatility calculation.</param>
    /// <param name="isAnnualized">Whether to annualize the result (default true).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hv(object source, int period, bool isAnnualized = true) : this(period, isAnnualized)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
        _logReturns.Clear();
        _previousClose = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateLogReturn(double currentPrice, double previousPrice)
    {
        return previousPrice > Epsilon ? Math.Log(currentPrice / previousPrice) : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateMean(ReadOnlySpan<double> values)
    {
        double sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }
        return sum / values.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateVariance(ReadOnlySpan<double> values, double mean, int degreesOfFreedom)
    {
        double sumSquaredDiff = 0;
        for (int i = 0; i < values.Length; i++)
        {
            double diff = values[i] - mean;
            sumSquaredDiff += diff * diff;
        }
        return sumSquaredDiff / degreesOfFreedom;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        double volatility = 0;
        if (_buffer.Count > 1)
        {
            // Calculate log return if we have previous close
            if (_previousClose > Epsilon)
            {
                double logReturn = CalculateLogReturn(Input.Value, _previousClose);
                _logReturns.Add(logReturn, Input.IsNew);
            }

            // Calculate volatility when we have enough returns
            if (_logReturns.Count == Period)
            {
                ReadOnlySpan<double> returns = _logReturns.GetSpan();
                double mean = CalculateMean(returns);
                double variance = CalculateVariance(returns, mean, Period - 1);
                volatility = Math.Sqrt(variance);

                if (IsAnnualized)
                {
                    volatility *= Math.Sqrt(TradingDaysPerYear);
                }
            }
        }

        _previousClose = Input.Value;
        IsHot = _index >= WarmupPeriod;
        return volatility;
    }
}
