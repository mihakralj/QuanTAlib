using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// ATRS: ATR Trailing Stop
/// A volatility-based trailing stop indicator that uses ATR to dynamically adjust
/// stop levels. It helps maintain position while allowing for normal market
/// fluctuations.
/// </summary>
/// <remarks>
/// The ATRS calculation process:
/// 1. Calculate ATR
/// 2. Multiply ATR by factor
/// 3. Apply trailing logic based on trend
/// 4. Update stop levels
///
/// Key characteristics:
/// - Dynamic stop levels
/// - Trend-following
/// - Volatility-based
/// - Position protection
/// - Risk management
///
/// Formula:
/// Long Stop = High - (ATR * Factor)
/// Short Stop = Low + (ATR * Factor)
/// where Factor is multiplier for ATR (default 2.0)
///
/// Market Applications:
/// - Stop loss placement
/// - Position management
/// - Trend following
/// - Risk control
/// - Exit strategy
///
/// Note: Returns stop level based on current trend
/// </remarks>

[SkipLocalsInit]
public sealed class Atrs : AbstractBase
{
    private readonly Atr _atr;
    private double _prevStop;
    private double _p_prevStop;
    private bool _isLong;
    private bool _p_isLong;
    private const int DefaultPeriod = 14;
    private const double DefaultFactor = 2.0;

    /// <summary>
    /// Gets the current trend direction (true for long, false for short)
    /// </summary>
    public bool IsLong => _isLong;

    /// <param name="period">The number of periods for ATR calculation (default 14).</param>
    /// <param name="factor">The multiplier for ATR (default 2.0).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1 or factor is less than or equal to 0.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Atrs(int period = DefaultPeriod, double factor = DefaultFactor)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (factor <= 0)
            throw new ArgumentOutOfRangeException(nameof(factor));

        _atr = new(period);
        Factor = factor;
        WarmupPeriod = period;
        Name = $"ATRS({period},{factor:F1})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for ATR calculation.</param>
    /// <param name="factor">The multiplier for ATR.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Atrs(object source, int period = DefaultPeriod, double factor = DefaultFactor) : this(period, factor)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    /// <summary>
    /// Gets or sets the ATR multiplier factor
    /// </summary>
    public double Factor { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _atr.Init();
        _prevStop = double.NaN;
        _isLong = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_prevStop = _prevStop;
            _p_isLong = _isLong;
        }
        else
        {
            _prevStop = _p_prevStop;
            _isLong = _p_isLong;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Calculate ATR
        double atr = _atr.Calc(BarInput);
        double atrBand = atr * Factor;

        if (_index == 1 || double.IsNaN(_prevStop))
        {
            // Initialize stop level
            _isLong = BarInput.Close > BarInput.Open;
            _prevStop = _isLong ? BarInput.Low - atrBand : BarInput.High + atrBand;
            return _prevStop;
        }

        // Update stop level based on trend
        if (_isLong)
        {
            double newStop = BarInput.High - atrBand;
            if (BarInput.Close < _prevStop)
            {
                _isLong = false;
                _prevStop = BarInput.High + atrBand;
            }
            else if (newStop > _prevStop)
            {
                _prevStop = newStop;
            }
        }
        else
        {
            double newStop = BarInput.Low + atrBand;
            if (BarInput.Close > _prevStop)
            {
                _isLong = true;
                _prevStop = BarInput.Low - atrBand;
            }
            else if (newStop < _prevStop)
            {
                _prevStop = newStop;
            }
        }

        return _prevStop;
    }
}
