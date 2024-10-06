namespace QuanTAlib;

/// <summary>
/// Represents a realized volatility calculator that measures the actual price fluctuations
/// observed in the market over a specific period.
/// </summary>
/// <remarks>
/// The Realized class calculates volatility based on logarithmic returns. It can provide
/// both annualized and non-annualized volatility measures. The calculation uses a rolling
/// sum of squared returns for efficiency and assumes 252 trading days in a year for annualization.
/// </remarks>
public class Realized : AbstractBase
{
    private readonly int Period;
    private readonly bool IsAnnualized;
    private readonly CircularBuffer _returns;
    private double _previousClose;
    private double _sumSquaredReturns;

    /// <summary>
    /// Initializes a new instance of the Realized class with the specified period and annualization flag.
    /// </summary>
    /// <param name="period">The period over which to calculate realized volatility.</param>
    /// <param name="isAnnualized">Whether to annualize the volatility (default is true).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 2.
    /// </exception>
    public Realized(int period, bool isAnnualized = true) : base()
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        Period = period;
        IsAnnualized = isAnnualized;
        WarmupPeriod = period + 1;  // We need one extra data point to calculate the first return
        _returns = new CircularBuffer(period);
        Name = $"Realized(period={period}, annualized={isAnnualized})";
        Init();
    }

    /// <summary>
    /// Initializes the Realized instance by clearing buffers and resetting calculation variables.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _returns.Clear();
        _previousClose = 0;
        _sumSquaredReturns = 0;
    }

    /// <summary>
    /// Manages the state of the Realized instance based on whether a new value is being processed.
    /// </summary>
    /// <param name="isNew">Indicates whether the current input is a new value.</param>
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    /// <summary>
    /// Performs the realized volatility calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated realized volatility value for the current period.
    /// </returns>
    /// <remarks>
    /// This method calculates the volatility using the following steps:
    /// 1. Compute logarithmic returns.
    /// 2. Maintain a rolling sum of squared returns.
    /// 3. Calculate the variance using the sum of squared returns.
    /// 4. Take the square root of the variance to get volatility.
    /// 5. If annualized, multiply by the square root of 252 (assumed trading days in a year).
    /// The method returns 0 until enough data points are available for the calculation.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double volatility = 0;
        if (_previousClose != 0)
        {
            double logReturn = Math.Log(Input.Value / _previousClose);

            if (_returns.Count == Period)
            {
                // Remove the oldest squared return from the sum
                _sumSquaredReturns -= Math.Pow(_returns[0], 2);
            }

            _returns.Add(logReturn, Input.IsNew);
            _sumSquaredReturns += Math.Pow(logReturn, 2);

            if (_returns.Count == Period)
            {
                double variance = _sumSquaredReturns / Period;
                volatility = Math.Sqrt(variance);

                if (IsAnnualized)
                {
                    // Assuming 252 trading days in a year. Adjust as needed.
                    volatility *= Math.Sqrt(252);
                }
            }
        }

        _previousClose = Input.Value;
        IsHot = _index >= WarmupPeriod;
        return volatility;
    }
}