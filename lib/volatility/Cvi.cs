using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CVI: Chaikin's Volatility Index
/// Measures the rate of change of a moving average of the difference
/// between high and low prices, indicating volatility expansion/contraction.
/// </summary>
/// <remarks>
/// The CVI calculation process:
/// 1. Calculate High-Low difference
/// 2. Take EMA of High-Low difference
/// 3. Calculate ROC of the EMA over specified period
///
/// Key characteristics:
/// - Measures volatility expansion/contraction
/// - Default period is 10 days
/// - Default smoothing period is 10 days
/// - Positive values indicate expanding volatility
/// - Negative values indicate contracting volatility
///
/// Formula:
/// HL = High - Low
/// Smoothed = EMA(HL, smoothPeriod)
/// CVI = ((Smoothed - Smoothed[period]) / Smoothed[period]) * 100
///
/// Market Applications:
/// - Volatility measurement
/// - Trend strength analysis
/// - Market regime identification
/// - Trading range analysis
/// - Breakout confirmation
///
/// Sources:
///     Marc Chaikin
///     https://www.investopedia.com/terms/c/chaikinvolatility.asp
///
/// Note: Returns percentage change in volatility
/// </remarks>
[SkipLocalsInit]
public sealed class Cvi : AbstractBase
{
    private readonly int _period;
    private readonly CircularBuffer _smoothed;
    private readonly double _alpha;
    private double _ema;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cvi(int period = 10, int smoothPeriod = 10)
    {
        _period = period;
        _alpha = 2.0 / (smoothPeriod + 1);
        WarmupPeriod = _period + smoothPeriod;
        Name = $"CVI({_period},{smoothPeriod})";
        _smoothed = new CircularBuffer(_period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cvi(object source, int period = 10, int smoothPeriod = 10) : this(period, smoothPeriod)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _ema = 0;
        _smoothed.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Calculate High-Low difference
        double hl = BarInput.High - BarInput.Low;

        // Calculate EMA of High-Low difference
        if (_index == 1)
        {
            _ema = hl;
        }
        else
        {
            _ema = (_alpha * hl) + ((1 - _alpha) * _ema);
        }

        // Add smoothed value to buffer
        _smoothed.Add(_ema);

        // Need enough values for calculation
        if (_index <= _period)
        {
            return 0;
        }

        // Calculate rate of change
        double roc = ((_ema - _smoothed[_period - 1]) / _smoothed[_period - 1]) * 100;

        IsHot = _index >= WarmupPeriod;
        return roc;
    }
}
