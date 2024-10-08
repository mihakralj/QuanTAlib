namespace QuanTAlib;

/// <summary>
/// Represents a Relative Volatility Index (RVI) calculator, which measures the direction
/// of volatility in relation to price movements.
/// </summary>
/// <remarks>
/// The RVI was introduced by Donald Dorsey in the 1993 issue of Technical Analysis
/// of Stocks &amp; Commodities Magazine. It focuses on the direction of price movements
/// in relation to volatility. The indicator uses standard deviation calculations
/// to determine whether volatility is increasing more in up moves or down moves.
///
/// This implementation uses a combination of Standard Deviation and Simple Moving Average
/// calculations to compute the RVI.
/// </remarks>
public class Rvi : AbstractBase
{
    private readonly Stddev _upStdDev, _downStdDev;
    private readonly Sma _upSma, _downSma;
    private double _previousClose;

    /// <summary>
    /// Initializes a new instance of the Rvi class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the RVI.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 2.
    /// </exception>
    public Rvi(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        int Period = period;
        WarmupPeriod = period;
        Name = $"RVI(period={period})";
        _upStdDev = new Stddev(Period);
        _downStdDev = new Stddev(Period);
        _upSma = new(Period);
        _downSma = new(Period);
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Rvi class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the RVI.</param>
    public Rvi(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Rvi instance by setting up the initial state.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _previousClose = 0;
    }

    /// <summary>
    /// Manages the state of the Rvi instance based on whether a new value is being processed.
    /// </summary>
    /// <param name="isNew">Indicates whether the current input is a new value.</param>
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Value;
            _index++;
        }
    }

    /// <summary>
    /// Performs the RVI calculation for the current input.
    /// </summary>
    /// <returns>
    /// The calculated RVI value for the current input.
    /// </returns>
    /// <remarks>
    /// This method calculates the RVI using the following steps:
    /// 1. Calculate the change in price from the previous close.
    /// 2. Determine the up move and down move based on the change.
    /// 3. Calculate standard deviations of up and down moves.
    /// 4. Apply a simple moving average to the standard deviations.
    /// 5. Compute the RVI as a percentage of up volatility to total volatility.
    /// The method returns 0 if the sum of up and down volatility is zero.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double close = Input.Value;
        double change = close - _previousClose;

        double upMove = Math.Max(change, 0);
        double downMove = Math.Max(-change, 0);

        _upSma.Calc(_upStdDev.Calc(new TValue(Input.Time, upMove, Input.IsNew)));
        _downSma.Calc(_downStdDev.Calc(new TValue(Input.Time, downMove, Input.IsNew)));

        double rvi;
        rvi = (_upSma.Value + _downSma.Value != 0) ? 100 * _upSma.Value / (_upSma.Value + _downSma.Value) : 0;

        _previousClose = close;
        IsHot = _index >= WarmupPeriod;
        return rvi;
    }
}
