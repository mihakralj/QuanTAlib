using System;
namespace QuanTAlib;

/// <summary>
/// TEMA: Triple Exponential Moving Average
/// A sophisticated moving average that applies three EMAs in sequence with a specific
/// combination formula to reduce lag while maintaining smoothness. The formula
/// 3*EMA1 - 3*EMA2 + EMA3 helps eliminate lag in trending markets.
/// </summary>
/// <remarks>
/// The TEMA calculation process:
/// 1. Calculates first EMA of the price
/// 2. Calculates second EMA of the first EMA
/// 3. Calculates third EMA of the second EMA
/// 4. Combines using formula: 3*EMA1 - 3*EMA2 + EMA3
///
/// Key characteristics:
/// - Significantly reduced lag compared to single EMA
/// - Better response to trends than standard EMAs
/// - Maintains smoothness despite reduced lag
/// - More responsive than double EMA (DEMA)
/// - Uses compensator for early values
///
/// Sources:
///     Patrick Mulloy - "Smoothing Data with Faster Moving Averages"
///     Technical Analysis of Stocks and Commodities, 1994
/// </remarks>

public class Tema : AbstractBase
{
    private readonly int _period;
    private double _lastEma1, _p_lastEma1;
    private double _lastEma2, _p_lastEma2;
    private double _lastEma3, _p_lastEma3;
    private double _k, _e, _p_e;

    /// <param name="period">The number of periods used in each EMA calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Tema(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _period = period;
        Name = "Tema";
        double percentile = 0.85; //targeting 85th percentile of correctness of converging EMA
        WarmupPeriod = (int)Math.Ceiling(-period * Math.Log(1 - percentile));
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in each EMA calculation.</param>
    public Tema(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _k = 2.0 / (_period + 1);
        _e = 1.0;
        _lastEma1 = _lastEma2 = _lastEma3 = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_lastEma1 = _lastEma1;
            _p_lastEma2 = _lastEma2;
            _p_lastEma3 = _lastEma3;
            _p_e = _e;
            _index++;
        }
        else
        {
            _lastEma1 = _p_lastEma1;
            _lastEma2 = _p_lastEma2;
            _lastEma3 = _p_lastEma3;
            _e = _p_e;
        }
    }

    protected override double Calculation()
    {
        double result, _ema1, _ema2, _ema3;
        ManageState(Input.IsNew);

        _e = (_e > 1e-10) ? (1 - _k) * _e : 0;
        double _invE = (_e > 1e-10) ? 1 / (1 - _e) : 1;

        _ema1 = _k * (Input.Value - _lastEma1) + _lastEma1;
        _ema2 = _k * (_ema1 * _invE - _lastEma2) + _lastEma2;
        _ema3 = _k * (_ema2 * _invE - _lastEma3) + _lastEma3;

        double _tema = 3 * _ema1 * _invE - 3 * _ema2 * _invE + _ema3 * _invE;

        result = _tema;
        _lastEma1 = _ema1;
        _lastEma2 = _ema2;
        _lastEma3 = _ema3;

        IsHot = _index >= WarmupPeriod;
        return result;
    }
}
