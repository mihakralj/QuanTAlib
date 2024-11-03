using System.Runtime.CompilerServices;
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
    private readonly double _k;
    private readonly double _oneMinusK;
    private readonly double _epsilon = 1e-10;
    private double _lastEma1, _p_lastEma1;
    private double _lastEma2, _p_lastEma2;
    private double _lastEma3, _p_lastEma3;
    private double _e, _p_e;

    /// <param name="period">The number of periods used in each EMA calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Tema(int period)
    {
        if (period < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _period = period;
        _k = 2.0 / (_period + 1);
        _oneMinusK = 1.0 - _k;
        Name = "Tema";
        double percentile = 0.85; //targeting 85th percentile of correctness of converging EMA
        WarmupPeriod = (int)System.Math.Ceiling(-period * System.Math.Log(1 - percentile));
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in each EMA calculation.</param>
    public Tema(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _e = 1.0;
        _lastEma1 = _lastEma2 = _lastEma3 = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateEma(double input, double lastEma, double invE)
    {
        return (_k * ((input * invE) - lastEma)) + lastEma;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double UpdateCompensator()
    {
        _e = (_e > _epsilon) ? _oneMinusK * _e : 0;
        return (_e > _epsilon) ? 1.0 / (1.0 - _e) : 1.0;
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double invE = UpdateCompensator();

        // Calculate EMAs with compensation
        double ema1 = CalculateEma(Input.Value, _lastEma1, 1.0);  // First EMA doesn't need compensation
        double ema2 = CalculateEma(ema1, _lastEma2, invE);
        double ema3 = CalculateEma(ema2, _lastEma3, invE);

        // Store values for next iteration
        _lastEma1 = ema1;
        _lastEma2 = ema2;
        _lastEma3 = ema3;

        // Calculate final TEMA with compensation
        double result = ((3.0 * ema1) - (3.0 * ema2) + ema3) * invE;

        IsHot = _index >= WarmupPeriod;
        return result;
    }
}
