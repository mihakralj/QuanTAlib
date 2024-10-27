using System;
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

public class Rv : AbstractBase
{
    private readonly int Period;
    private readonly bool IsAnnualized;
    private readonly CircularBuffer _returns;
    private double _previousClose;
    private double _sumSquaredReturns;

    /// <param name="period">The number of periods for volatility calculation.</param>
    /// <param name="isAnnualized">Whether to annualize the result (default true).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    public Rv(int period, bool isAnnualized = true)
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
    public Rv(object source, int period, bool isAnnualized = true) : this(period, isAnnualized)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _returns.Clear();
        _previousClose = 0;
        _sumSquaredReturns = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double volatility = 0;
        if (_previousClose != 0)
        {
            // Calculate log return
            double logReturn = Math.Log(Input.Value / _previousClose);

            if (_returns.Count == Period)
            {
                // Maintain rolling sum by removing oldest squared return
                _sumSquaredReturns -= Math.Pow(_returns[0], 2);
            }

            // Add new return and update sum
            _returns.Add(logReturn, Input.IsNew);
            _sumSquaredReturns += Math.Pow(logReturn, 2);

            if (_returns.Count == Period)
            {
                // Calculate realized volatility
                double variance = _sumSquaredReturns / Period;
                volatility = Math.Sqrt(variance);

                if (IsAnnualized)
                {
                    volatility *= Math.Sqrt(252); // Annualize using trading days
                }
            }
        }

        _previousClose = Input.Value;
        IsHot = _index >= WarmupPeriod;
        return volatility;
    }
}
