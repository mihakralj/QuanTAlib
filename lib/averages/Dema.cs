using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// DEMA: Double Exponential Moving Average
/// DEMA reduces the lag of a traditional EMA by applying a second EMA over EMA.
/// It responds more quickly to price changes than a standard EMA while maintaining
/// smoothness, at the cost of overshooting the signal line.
/// </summary>
/// <remarks>
/// Sources:
///    https://en.wikipedia.org/wiki/Double_exponential_moving_average
///    https://www.investopedia.com/terms/d/double-exponential-moving-average.asp
///    https://www.tradingview.com/support/solutions/43000502589-double-exponential-moving-average-dema/
///
/// Validation:
///    Skender.Stock.Indicators
/// </remarks>
public class Dema : AbstractBase
{
    private readonly double _k;
    private readonly double _epsilon = 1e-10;
    private double _lastEma1, _p_lastEma1;
    private double _lastEma2, _p_lastEma2;
    private double _e, _p_e;

    public Dema(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _k = 2.0 / (period + 1);
        Name = "Dema";
        double percentile = 0.85; //targeting 85th percentile of correctness of converging EMA
        WarmupPeriod = (int)System.Math.Ceiling(-period * System.Math.Log(1 - percentile));
        Init();
    }

    public Dema(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _e = 1.0;
        _lastEma1 = 0;
        _lastEma2 = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateEma(double input, double lastEma)
    {
        return (_k * (input - lastEma)) + lastEma;
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Compensator for early EMA values
        _e = (_e > _epsilon) ? (1 - _k) * _e : 0;
        double invE = (_e > _epsilon) ? 1 / (1 - _e) : 1;

        // Calculate EMAs
        double ema1 = CalculateEma(Input.Value, _lastEma1);
        double compensatedEma1 = ema1 * invE;
        double ema2 = CalculateEma(compensatedEma1, _lastEma2);

        // Store values for next iteration
        _lastEma1 = ema1;
        _lastEma2 = ema2;

        // Calculate final DEMA
        double result = (2 * compensatedEma1) - (ema2 * invE);

        IsHot = _index >= WarmupPeriod;
        return result;
    }
}
