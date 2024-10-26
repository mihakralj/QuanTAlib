namespace QuanTAlib;

/// <summary>
/// Represents an Average True Range (ATR) calculator, a measure of market volatility.
/// </summary>
/// <remarks>
/// The ATR class calculates the average true range using a Relative Moving Average (RMA)
/// of the true range. The true range is the greatest of: current high - current low,
/// absolute value of current high - previous close, or absolute value of current low - previous close.
/// </remarks>
public class Atr : AbstractBase
{
    public double Tr { get; private set; }
    private readonly Rma _ma;
    private double _prevClose, _p_prevClose;

    /// <summary>
    /// Initializes a new instance of the Atr class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the ATR.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 1.
    /// </exception>
    public Atr(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _ma = new(period, useSma: true);
        WarmupPeriod = _ma.WarmupPeriod;
        Name = $"ATR({period})";
    }

    /// <summary>
    /// Initializes a new instance of the Atr class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for bar updates.</param>
    /// <param name="period">The period over which to calculate the ATR.</param>
    public Atr(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    /// <summary>
    /// Initializes the Atr instance by setting up the initial state.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _ma.Init();
        _prevClose = double.NaN;
        Tr = 0;
    }

    /// <summary>
    /// Manages the state of the Atr instance based on whether a new bar is being processed.
    /// </summary>
    /// <param name="isNew">Indicates whether the current input is a new bar.</param>
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_prevClose = _prevClose;
        }
        else
        {
            _prevClose = _p_prevClose;
        }
    }

    /// <summary>
    /// Performs the ATR calculation for the current bar.
    /// </summary>
    /// <returns>
    /// The calculated ATR value for the current bar.
    /// </returns>
    /// <remarks>
    /// This method calculates the true range for the current bar and then uses an RMA
    /// to smooth the true range values. For the first bar, it uses the high-low range
    /// as the true range.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        if (_index == 1)
        {
            Tr = BarInput.High - BarInput.Low;
            _prevClose = BarInput.Close;
        }
        else
        {
            Tr = Math.Max(
                BarInput.High - BarInput.Low,
                Math.Max(
                    Math.Abs(BarInput.High - _prevClose),
                    Math.Abs(BarInput.Low - _prevClose)
                )
            );
        }
        _ma.Calc(new TValue(Input.Time, Tr, BarInput.IsNew));

        IsHot = _ma.IsHot;
        _prevClose = BarInput.Close;
        return _ma.Value;
    }
}
