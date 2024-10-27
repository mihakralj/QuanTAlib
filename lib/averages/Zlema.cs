using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// ZLEMA: Zero Lag Exponential Moving Average
/// A modified exponential moving average designed to reduce lag by incorporating
/// error correction based on predicted values. It estimates and removes lag by
/// extrapolating the trend using the difference between current and lagged prices.
/// </summary>
/// <remarks>
/// The ZLEMA calculation process:
/// 1. Calculates lag period as (period - 1) / 2
/// 2. Gets error correction term: 2 * price - lag_price
/// 3. Applies EMA to error-corrected price
/// 4. Results in reduced lag compared to standard EMA
///
/// Key characteristics:
/// - Significantly reduced lag compared to EMA
/// - More responsive to price changes
/// - Uses error correction mechanism
/// - Maintains smoothness despite reduced lag
/// - Better trend following capabilities
///
/// Sources:
///     John Ehlers and Ric Way - "Zero Lag (Well, Almost)"
///     Technical Analysis of Stocks and Commodities, 2010
/// </remarks>

public class Zlema : AbstractBase
{
    private readonly CircularBuffer _buffer;
    private readonly int _lag;
    private readonly Ema _ema;
    private double _lastZLEMA, _p_lastZLEMA;

    /// <param name="period">The number of periods used in the ZLEMA calculation.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Zlema(int period)
    {
        if (period < 1)
        {
            throw new System.ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        WarmupPeriod = period;
        _lag = (int)(0.5 * (period - 1));
        _buffer = new CircularBuffer(_lag + 1);
        _ema = new Ema(period, useSma: false);
        Name = $"Zlema({period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in the ZLEMA calculation.</param>
    public Zlema(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
        _ema.Init();
        _lastZLEMA = 0;
        _p_lastZLEMA = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
            _p_lastZLEMA = _lastZLEMA;
        }
        else
        {
            _lastZLEMA = _p_lastZLEMA;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateErrorCorrection()
    {
        double lagValue = _buffer[System.Math.Max(0, _buffer.Count - 1 - _lag)];
        return 2.0 * Input.Value - lagValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateZlema(double errorCorrection)
    {
        var tempValue = new TValue(Input.Time, errorCorrection, Input.IsNew);
        return _ema.Calc(tempValue).Value;
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        // Calculate error correction and apply EMA
        double errorCorrection = CalculateErrorCorrection();
        double zlema = CalculateZlema(errorCorrection);

        _lastZLEMA = zlema;
        IsHot = _index >= WarmupPeriod;

        return zlema;
    }
}
