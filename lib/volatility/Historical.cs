namespace QuanTAlib;

/// <summary>
/// Represents a historical volatility calculator that measures the dispersion of returns
/// for a given security or market index over a specific period.
/// </summary>
/// <remarks>
/// The Historical class calculates volatility based on logarithmic returns. It can provide
/// both annualized and non-annualized volatility measures. The calculation uses a sample
/// standard deviation formula and assumes 252 trading days in a year for annualization.
/// </remarks>
public class Historical : AbstractBase
{
    private readonly int Period;
    private readonly bool IsAnnualized;
    private readonly CircularBuffer _buffer;
    private readonly CircularBuffer _logReturns;
    private double _previousClose;

    /// <summary>
    /// Initializes a new instance of the Historical class with the specified period and annualization flag.
    /// </summary>
    /// <param name="period">The period over which to calculate historical volatility.</param>
    /// <param name="isAnnualized">Whether to annualize the volatility (default is true).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 2.
    /// </exception>
    public Historical(int period, bool isAnnualized = true)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        Period = period;
        IsAnnualized = isAnnualized;
        WarmupPeriod = period + 1;  // We need one extra data point to calculate the first return
        _buffer = new CircularBuffer(period + 1);
        _logReturns = new CircularBuffer(period);
        Name = $"Historical(period={period}, annualized={isAnnualized})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Historical class with the specified source, period, and annualization flag.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate historical volatility.</param>
    /// <param name="isAnnualized">Whether to annualize the volatility (default is true).</param>
    public Historical(object source, int period, bool isAnnualized = true) : this(period, isAnnualized)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Historical instance by clearing buffers and resetting the previous close value.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
        _logReturns.Clear();
        _previousClose = 0;
    }

    /// <summary>
    /// Manages the state of the Historical instance based on whether a new value is being processed.
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
    /// Performs the historical volatility calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated historical volatility value for the current period.
    /// </returns>
    /// <remarks>
    /// This method calculates the volatility using the following steps:
    /// 1. Compute logarithmic returns.
    /// 2. Calculate the sample standard deviation of the log returns.
    /// 3. If annualized, multiply by the square root of 252 (assumed trading days in a year).
    /// The method returns 0 until enough data points are available for the calculation.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer.Add(Input.Value, Input.IsNew);

        double volatility = 0;
        if (_buffer.Count > 1)
        {
            if (_previousClose != 0)
            {
                double logReturn = Math.Log(Input.Value / _previousClose);
                _logReturns.Add(logReturn, Input.IsNew);
            }

            if (_logReturns.Count == Period)
            {
                var returns = _logReturns.GetSpan().ToArray();
                double mean = returns.Average();
                double sumOfSquaredDifferences = returns.Sum(x => Math.Pow(x - mean, 2));

                double variance = sumOfSquaredDifferences / (Period - 1);  // Using sample standard deviation
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
