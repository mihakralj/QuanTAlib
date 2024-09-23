namespace QuanTAlib;

/// <summary>
/// DEMA: Double Exponential Moving Average
/// DEMA reduces the lag of a traditional EMA by applying a second EMA over EMA.
/// It responds more quickly to price changes than a standard EMA while maintaining
/// smoothness, at the cost of overshooting the signal line.
/// </summary>
/// <remarks>
/// Smoothness:     ★★★☆☆ (3/5)
/// Sensitivity:    ★★★★☆ (4/5)
/// Overshooting:   ★★★☆☆ (3/5)
/// Lag:            ★★★★☆ (4/5)
///
/// Sources:
///    https://www.investopedia.com/terms/d/double-exponential-moving-average.asp
///    https://www.tradingview.com/support/solutions/43000502589-double-exponential-moving-average-dema/
///
/// Validation:
///    Skender.Stock.Indicators
/// </remarks>
public class Dema : AbstractBase
{
    // inherited _index
    // inherited _value
    private readonly int _period;
    private double _lastEma1, _p_lastEma1;
    private double _lastEma2, _p_lastEma2;
    private double _k, _e, _p_e;

    public Dema(int period) : base()
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _period = period;
        Name = "Dema";
        double percentile = 0.85; //targeting 85th percentile of correctness of converging EMA
        WarmupPeriod = (int)Math.Ceiling(-period * Math.Log(1 - percentile));
        Init();
    }

    public Dema(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }
    //inhereted public void Sub(object source, in ValueEventArgs args)

    public override void Init()
    {
        base.Init();
        _k = 2.0 / (_period + 1);
        _e = 1.0;
        _lastEma1 = 0;
        _lastEma2 = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_lastEma1 = _lastEma1;
            _p_lastEma2 = _lastEma2;
            _p_e = _e;
            _index++;
        }
        else
        {
            _lastEma1 = _p_lastEma1;
            _lastEma2 = _p_lastEma2;
            _e = _p_e;
        }
    }

    /// <summary>
    /// Core DEMA calculation
    /// </summary>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double result, _ema1, _ema2;

        // dynamic k when within period; (index is zero-based, therefore +2)
        //double _dk = (_index + 1 >= _period) ? _k : 2.0 / (_index + 2);
        // compensator for early ema values
        _e = (_e > 1e-10) ? (1 - _k) * _e : 0;
        double _invE = (_e > 1e-10) ? 1 / (1 - _e) : 1;

        // Calculate EMA1
        _ema1 = _k * (Input.Value - _lastEma1) + _lastEma1;

        // Calculate EMA2 using compensatedEma1
        _ema2 = _k * (_ema1 * _invE - _lastEma2) + _lastEma2;

        // Calculate DEMA
        double _dema = 2 * _ema1 * _invE - (_ema2 * _invE);

        result = _dema;
        _lastEma1 = _ema1;
        _lastEma2 = _ema2;

        IsHot = _index >= WarmupPeriod;
        return result;
    }
}
