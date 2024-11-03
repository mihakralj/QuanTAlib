using System.Runtime.CompilerServices;
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
    private readonly int _period;
    private readonly double _k;  // Wilder's smoothing factor
    private readonly double _oneMinusK;  // 1 - k
    private readonly double _epsilon = 1e-10;
    private readonly bool _useSma;
    private CircularBuffer _sma;

    private double _lastRma, _p_lastRma;
    private double _e, _p_e;
    private bool _isInit, _p_isInit;

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
            throw new System.ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _period = period;
        _k = 1.0 / period;
        _oneMinusK = 1.0 - _k;
        _useSma = useSma;
        _sma = new(period);
        Name = "Rma";
        WarmupPeriod = period * 2;  // RMA typically needs more warmup periods
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _e = 1.0;
        _lastRma = 0;
        _isInit = false;
        _p_isInit = false;
        _sma = new(_period);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateRma(double input)
    {
        return (_k * input) + (_oneMinusK * _lastRma);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CompensateRma(double rma)
    {
        _e = (_e > _epsilon) ? _oneMinusK * _e : 0;
        return (_useSma || _e <= double.Epsilon) ? rma : rma / (1.0 - _e);
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double result;
        if (!_isInit && _useSma)
        {
            _sma.Add(Input.Value, Input.IsNew);
            _lastRma = _sma.Average();
            result = _lastRma;

            if (_index >= _period)
            {
                _isInit = true;
            }
        }
        else
        {
            _lastRma = CalculateRma(Input.Value);
            result = CompensateRma(_lastRma);
        }

        IsHot = _index >= WarmupPeriod;
        return result;
    }
}
