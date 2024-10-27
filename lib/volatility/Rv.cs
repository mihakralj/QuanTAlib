using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// RV: Realized Volatility
/// A precise volatility measure that captures actual observed price fluctuations
/// using high-frequency returns. RV provides a more accurate assessment of true
/// market volatility compared to traditional estimators.
/// </summary>
/// <remarks>
/// The RV calculation process:
/// 1. Computes log returns
/// 2. Squares each return
/// 3. Maintains rolling sum
/// 4. Takes square root of average
/// 5. Optionally annualizes
///
/// Key characteristics:
/// - Model-free measurement
/// - High-frequency capable
/// - Rolling calculation
/// - Memory efficient
/// - Optional annualization
///
/// Formula:
/// RV = √(Σ(ln(P[t]/P[t-1]))²/n) * √252
/// where:
/// P = price
/// n = number of observations
/// 252 = trading days per year
///
/// Market Applications:
/// - High-frequency trading
/// - Options pricing
/// - Risk forecasting
/// - Market microstructure
/// - Volatility trading
///
/// Sources:
///     Andersen, Bollerslev - "Answering the Skeptics"
///     https://en.wikipedia.org/wiki/Realized_volatility
///
/// Note: Efficient implementation using rolling sums
/// </remarks>

[SkipLocalsInit]
public sealed class Rv : AbstractBase
{
    private readonly int Period;
    private readonly bool IsAnnualized;
    private readonly CircularBuffer _returns;
    private double _previousClose;
    private double _sumSquaredReturns;
    private const int TradingDaysPerYear = 252;
    private const double Epsilon = 1e-10;
    private const bool DefaultIsAnnualized = true;

    /// <param name="period">The number of periods for volatility calculation.</param>
    /// <param name="isAnnualized">Whether to annualize the result (default true).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rv(int period, bool isAnnualized = DefaultIsAnnualized)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2.");
        }
        Period = period;
        IsAnnualized = isAnnualized;
        WarmupPeriod = period + 1;  // Need extra point for first return
        _returns = new CircularBuffer(period);
        Name = $"Realized(period={period}, annualized={isAnnualized})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for volatility calculation.</param>
    /// <param name="isAnnualized">Whether to annualize the result (default true).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rv(object source, int period, bool isAnnualized = DefaultIsAnnualized) : this(period, isAnnualized)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _returns.Clear();
        _previousClose = 0;
        _sumSquaredReturns = 0;
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
    private static double CalculateVolatility(double sumSquaredReturns, int period, bool isAnnualized)
    {
        double variance = sumSquaredReturns / period;
        double volatility = Math.Sqrt(variance);
        return isAnnualized ? volatility * Math.Sqrt(TradingDaysPerYear) : volatility;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double volatility = 0;
        if (_previousClose > Epsilon)
        {
            // Calculate log return
            double logReturn = CalculateLogReturn(Input.Value, _previousClose);

            if (_returns.Count == Period)
            {
                // Maintain rolling sum by removing oldest squared return
                double oldReturn = _returns[0];
                _sumSquaredReturns -= oldReturn * oldReturn;
            }

            // Add new return and update sum
            _returns.Add(logReturn, Input.IsNew);
            _sumSquaredReturns += logReturn * logReturn;

            if (_returns.Count == Period)
            {
                // Calculate realized volatility
                volatility = CalculateVolatility(_sumSquaredReturns, Period, IsAnnualized);
            }
        }

        _previousClose = Input.Value;
        IsHot = _index >= WarmupPeriod;
        return volatility;
    }
}
