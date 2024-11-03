using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// T3: Tillson T3 Moving Average
/// A sophisticated moving average developed by Tim Tillson that applies six EMAs
/// in sequence with optimized coefficients. The T3 provides excellent smoothing
/// while maintaining responsiveness and minimal lag.
/// </summary>
/// <remarks>
/// The T3 calculation process:
/// 1. Applies six EMAs in sequence
/// 2. Uses volume factor to determine optimal coefficients
/// 3. Combines EMAs using specific formula: c1*EMA6 + c2*EMA5 + c3*EMA4 + c4*EMA3
/// 4. Coefficients are based on the volume factor parameter
///
/// Key characteristics:
/// - Excellent smoothing with minimal lag
/// - Adjustable via volume factor parameter
/// - No overshooting like triple EMA
/// - Better noise reduction than traditional EMAs
/// - Maintains responsiveness to significant moves
///
/// Sources:
///     Tim Tillson - "Better Moving Averages"
///     TASC Magazine, 1998
/// </remarks>
public class T3 : AbstractBase
{
    private readonly int _period;
    private readonly bool _useSma;
    private readonly double _k;
    private readonly double _c1, _c2, _c3, _c4;
    private readonly CircularBuffer _buffer1, _buffer2, _buffer3, _buffer4, _buffer5, _buffer6;

    private double _lastEma1, _lastEma2, _lastEma3, _lastEma4, _lastEma5, _lastEma6;
    private double _p_lastEma1, _p_lastEma2, _p_lastEma3, _p_lastEma4, _p_lastEma5, _p_lastEma6;

    /// <param name="period">The number of periods used in each EMA calculation.</param>
    /// <param name="vfactor">Volume factor controlling smoothing (default 0.7).</param>
    /// <param name="useSma">Whether to use SMA for initial values (default true).</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public T3(int period, double vfactor = 0.7, bool useSma = true)
    {
        if (period < 1)
        {
            throw new System.ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _useSma = useSma;
        WarmupPeriod = period;

        _k = 2.0 / (_period + 1);

        // Precalculate coefficients
        double v2 = vfactor * vfactor;
        double v3 = v2 * vfactor;
        _c1 = -v3;
        _c2 = 3.0 * (v2 + v3);
        _c3 = -3.0 * ((2.0 * v2) + vfactor + v3);
        _c4 = 1.0 + (3.0 * vfactor) + v3 + (3.0 * v2);

        _buffer1 = new(period);
        _buffer2 = new(period);
        _buffer3 = new(period);
        _buffer4 = new(period);
        _buffer5 = new(period);
        _buffer6 = new(period);

        Name = $"T3({_period}, {vfactor})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in each EMA calculation.</param>
    /// <param name="vfactor">Volume factor controlling smoothing (default 0.7).</param>
    /// <param name="useSma">Whether to use SMA for initial values (default true).</param>
    public T3(object source, int period, double vfactor = 0.7, bool useSma = true) : this(period, vfactor, useSma)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        _lastEma1 = _lastEma2 = _lastEma3 = _lastEma4 = _lastEma5 = _lastEma6 = 0;
        _buffer1.Clear();
        _buffer2.Clear();
        _buffer3.Clear();
        _buffer4.Clear();
        _buffer5.Clear();
        _buffer6.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
            _p_lastEma1 = _lastEma1;
            _p_lastEma2 = _lastEma2;
            _p_lastEma3 = _lastEma3;
            _p_lastEma4 = _lastEma4;
            _p_lastEma5 = _lastEma5;
            _p_lastEma6 = _lastEma6;
        }
        else
        {
            _lastEma1 = _p_lastEma1;
            _lastEma2 = _p_lastEma2;
            _lastEma3 = _p_lastEma3;
            _lastEma4 = _p_lastEma4;
            _lastEma5 = _p_lastEma5;
            _lastEma6 = _p_lastEma6;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateEma(double input, double lastEma)
    {
        return (_k * (input - lastEma)) + lastEma;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateT3(double ema3, double ema4, double ema5, double ema6)
    {
        return (_c1 * ema6) + (_c2 * ema5) + (_c3 * ema4) + (_c4 * ema3);
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double ema1, ema2, ema3, ema4, ema5, ema6;

        if (_index == 1)
        {
            ema1 = ema2 = ema3 = ema4 = ema5 = ema6 = Input.Value;
        }
        else if (_index <= _period && _useSma)
        {
            _buffer1.Add(Input.Value, Input.IsNew);
            ema1 = _buffer1.Average();
            _buffer2.Add(ema1, Input.IsNew);
            ema2 = _buffer2.Average();
            _buffer3.Add(ema2, Input.IsNew);
            ema3 = _buffer3.Average();
            _buffer4.Add(ema3, Input.IsNew);
            ema4 = _buffer4.Average();
            _buffer5.Add(ema4, Input.IsNew);
            ema5 = _buffer5.Average();
            _buffer6.Add(ema5, Input.IsNew);
            ema6 = _buffer6.Average();
        }
        else
        {
            ema1 = CalculateEma(Input.Value, _lastEma1);
            ema2 = CalculateEma(ema1, _lastEma2);
            ema3 = CalculateEma(ema2, _lastEma3);
            ema4 = CalculateEma(ema3, _lastEma4);
            ema5 = CalculateEma(ema4, _lastEma5);
            ema6 = CalculateEma(ema5, _lastEma6);
        }

        _lastEma1 = ema1;
        _lastEma2 = ema2;
        _lastEma3 = ema3;
        _lastEma4 = ema4;
        _lastEma5 = ema5;
        _lastEma6 = ema6;

        IsHot = _index >= WarmupPeriod;
        return CalculateT3(ema3, ema4, ema5, ema6);
    }
}
