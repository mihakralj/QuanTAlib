namespace QuanTAlib;

/// <summary>
/// RMA: Relative Moving Average (also known as Wilder's Moving Average)
/// </summary>
/// <remarks>
/// RMA is similar to EMA but uses a different smoothing factor.
///
/// Key characteristics:
/// - Uses no buffer, relying only on the previous RMA value.
/// - The weight of new data points (alpha) is calculated as 1 / period.
/// - Provides a smoother curve compared to SMA and EMA, reacting more slowly to price changes.
///
/// Calculation method:
/// This implementation can use SMA for the first Period bars as a seeding value for RMA when useSma is true.
///
/// Sources:
/// - https://www.tradingview.com/pine-script-reference/v5/#fun_ta{dot}rma
/// - https://www.investopedia.com/terms/w/wilders-smoothing.asp
/// </remarks>
public class Rma : AbstractBase
{
    // inherited _index
    // inherited _value

    /// <summary>
    /// The period for the RMA calculation.
    /// </summary>
    private readonly int _period;

    /// <summary>
    /// Circular buffer for SMA calculation.
    /// </summary>
    private CircularBuffer _sma;

    /// <summary>
    /// The last calculated RMA value.
    /// </summary>
    private double _lastRma, _p_lastRma;

    /// <summary>
    /// Compensator for early RMA values.
    /// </summary>
    private double _e, _p_e;

    /// <summary>
    /// The smoothing factor for RMA calculation.
    /// </summary>
    private readonly double _k;

    /// <summary>
    /// Flags to track initialization status.
    /// </summary>
    private bool _isInit, _p_isInit;

    /// <summary>
    /// Flag to determine whether to use SMA for initial values.
    /// </summary>
    private readonly bool _useSma;

    /// <summary>
    /// Initializes a new instance of the Rma class with a specified period.
    /// </summary>
    /// <param name="period">The period for RMA calculation.</param>
    /// <param name="useSma">Whether to use SMA for initial values. Default is true.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Rma(int period, bool useSma = true)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _period = period;
        _k = 1.0 / _period;  // Wilder's smoothing factor
        _useSma = useSma;
        _sma = new(period);
        Name = "Rma";
        WarmupPeriod = _period * 2;  // RMA typically needs more warmup periods
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Rma class with a specified source and period.
    /// </summary>
    /// <param name="source">The source object for event subscription.</param>
    /// <param name="period">The period for RMA calculation.</param>
    /// <param name="useSma">Whether to use SMA for initial values. Default is true.</param>
    public Rma(object source, int period, bool useSma = true) : this(period, useSma)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Rma instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _e = 1.0;
        _lastRma = 0;
        _isInit = false;
        _p_isInit = false;
        _sma = new(_period);
    }

    /// <summary>
    /// Manages the state of the Rma instance.
    /// </summary>
    /// <param name="isNew">Indicates whether the input is new.</param>
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_lastRma = _lastRma;
            _p_isInit = _isInit;
            _p_e = _e;
            _index++;
        }
        else
        {
            _lastRma = _p_lastRma;
            _isInit = _p_isInit;
            _e = _p_e;
        }
    }

    /// <summary>
    /// Performs the RMA calculation.
    /// </summary>
    /// <returns>The calculated RMA value.</returns>
    protected override double Calculation()
    {
        double result, _rma;
        ManageState(Input.IsNew);

        // when _UseSma == true, use SMA calculation until we have enough data points
        if (!_isInit && _useSma)
        {
            _sma.Add(Input.Value, Input.IsNew);
            _rma = _sma.Average();
            result = _rma;
            if (_index >= _period)
            {
                _isInit = true;
            }
        }
        else
        {
            // compensator for early rma values
            _e = (_e > 1e-10) ? (1 - _k) * _e : 0;

            _rma = _k * Input.Value + (1 - _k) * _lastRma;

            // _useSma decides if we use compensator or not
            result = (_useSma || _e <= double.Epsilon) ? _rma : _rma / (1 - _e);
        }
        _lastRma = _rma;
        IsHot = _index >= WarmupPeriod;
        return result;
    }
}
