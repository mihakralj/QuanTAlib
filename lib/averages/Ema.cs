namespace QuanTAlib;

/// <summary>
/// EMA: Exponential Moving Average
/// EMA needs very short history buffer and calculates the EMA value using just the
/// previous EMA value. The weight of the new datapoint (alpha) is alpha = 2 / (period + 1)
/// </summary>
/// <remarks>
/// Key characteristics:
/// - Uses no buffer, relying only on the previous EMA value.
/// - The weight of new data points is calculated as alpha = 2 / (period + 1).
/// - Provides a balance between responsiveness and smoothing. No overshooting. Significant lag
///
/// Calculation method:
/// This implementation can use SMA for the first Period bars as a seeding value for EMA when useSma is true.
///
/// Sources:
/// - https://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:moving_averages
/// - https://www.investopedia.com/ask/answers/122314/what-exponential-moving-average-ema-formula-and-how-ema-calculated.asp
/// - https://blog.fugue88.ws/archives/2017-01/The-correct-way-to-start-an-Exponential-Moving-Average-EMA
/// </remarks>

public class Ema : AbstractBase
{
    // inherited _index
    // inherited _value
    private readonly int _period;
    private CircularBuffer _sma;
    private double _lastEma, _p_lastEma;
    private double _k, _e, _p_e;
    private bool _isInit, _p_isInit, _useSma;

    public Ema(int period, bool useSma = true) : base()
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _period = period;
        _k = 2.0 / (_period + 1);
        _useSma = useSma;
        _sma = new(period);
        Name = "Ema";
        WarmupPeriod = (int)Math.Ceiling(Math.Log(0.05) / Math.Log(1 - _k)); //95th percentile
        Init();
    }

    public Ema(double alpha) : base()
    {
        _k = alpha;
        _useSma = false;
        _sma = new(1);
        _period = 1;
        WarmupPeriod = (int)Math.Ceiling(Math.Log(0.05) / Math.Log(1 - _k)); //95th percentile
        Init();
    }

    public Ema(object source, int period, bool useSma = true) : this(period, useSma)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }
    //inhereted public void Sub(object source, in ValueEventArgs args)

    public override void Init()
    {
        base.Init();
        _e = 1.0;
        _lastEma = 0;
        _isInit = false;
        _p_isInit = false;
        _sma = new(_period);
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_lastEma = _lastEma;
            _p_isInit = _isInit;
            _p_e = _e;
            _index++;
        }
        else
        {
            _lastEma = _p_lastEma;
            _isInit = _p_isInit;
            _e = _p_e;
        }
    }

    /// <summary>
    /// Core EMA calculation
    /// </summary>
    protected override double Calculation()
    {
        double result, _ema;
        ManageState(Input.IsNew);

        // when _UseSma == true, use SMA calculation until we have enough data points
        if (!_isInit && _useSma)
        {
            _sma.Add(Input.Value, Input.IsNew);
            _ema = _sma.Average();
            result = _ema;
            if (_index >= _period)
            {
                _isInit = true;
            }
        }
        else
        {
            // compensator for early ema values
            _e = (_e > 1e-10) ? (1 - _k) * _e : 0;

            _ema = _k * (Input.Value - _lastEma) + _lastEma;

            // _useSma decides if we use compensator or not
            result = (_useSma || _e == 0) ? _ema : _ema / (1 - _e);
        }
        _lastEma = _ema;
        IsHot = _index >= WarmupPeriod;
        return result;
    }
}
