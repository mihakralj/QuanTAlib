using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// VS: Volatility Stop
/// A technical indicator that uses volatility to determine stop levels,
/// adapting to market conditions for dynamic risk management.
/// </summary>
/// <remarks>
/// The VS calculation process:
/// 1. Calculate Average True Range (ATR)
/// 2. Calculate stop levels:
///    Long Stop = Close - (multiplier * ATR)
///    Short Stop = Close + (multiplier * ATR)
/// 3. Trail stops based on price movement
///
/// Key characteristics:
/// - Adaptive stop levels
/// - Based on ATR volatility
/// - Default period is 14 days
/// - Returns both long and short stops
/// - Trails with price movement
///
/// Formula:
/// ATR = Average(TR, period)
/// Long Stop = Close - (multiplier * ATR)
/// Short Stop = Close + (multiplier * ATR)
///
/// Market Applications:
/// - Stop loss placement
/// - Position management
/// - Risk control
/// - Trend following
/// - Exit strategy
///
/// Sources:
///     Adaptation of Volatility-Based Stops concept
///     https://www.investopedia.com/terms/v/volatility-stop.asp
///
/// Note: Returns two values: long stop and short stop levels
/// </remarks>
[SkipLocalsInit]
public sealed class Vs : AbstractBase
{
    private readonly int _period;
    private readonly double _multiplier;
    private readonly CircularBuffer _tr;
    private double _prevClose;
    private double _longStop;
    private double _shortStop;
    private double _prevLongStop;
    private double _prevShortStop;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vs(int period = 14, double multiplier = 2.0)
    {
        _period = period;
        _multiplier = multiplier;
        WarmupPeriod = period + 1;  // Need one extra period for TR
        Name = $"VS({_period},{_multiplier})";
        _tr = new CircularBuffer(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vs(object source, int period = 14, double multiplier = 2.0) : this(period, multiplier)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _longStop = 0;
        _shortStop = 0;
        _prevLongStop = 0;
        _prevShortStop = 0;
        _tr.Clear();
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

        // Skip first period to establish previous close
        if (_index == 1)
        {
            _prevClose = BarInput.Close;
            _longStop = BarInput.Close;
            _shortStop = BarInput.Close;
            return 0;
        }

        // Calculate True Range
        double tr = Math.Max(BarInput.High - BarInput.Low,
                   Math.Max(Math.Abs(BarInput.High - _prevClose),
                          Math.Abs(BarInput.Low - _prevClose)));

        // Add TR to buffer
        _tr.Add(tr);

        // Store current close for next calculation
        _prevClose = BarInput.Close;

        // Need enough values for ATR calculation
        if (_index <= _period)
        {
            return 0;
        }

        // Calculate ATR
        double atr = _tr.Average();

        // Calculate initial stop levels
        double potentialLongStop = BarInput.Close - (_multiplier * atr);
        double potentialShortStop = BarInput.Close + (_multiplier * atr);

        // Trail stops
        _longStop = BarInput.Close > _prevShortStop ? potentialLongStop : Math.Max(potentialLongStop, _prevLongStop);
        _shortStop = BarInput.Close < _prevLongStop ? potentialShortStop : Math.Min(potentialShortStop, _prevShortStop);

        // Store current stops for next calculation
        _prevLongStop = _longStop;
        _prevShortStop = _shortStop;

        IsHot = _index >= WarmupPeriod;
        return _longStop;  // Return long stop as primary value
    }

    /// <summary>
    /// Gets the long stop level
    /// </summary>
    public double LongStop => _longStop;

    /// <summary>
    /// Gets the short stop level
    /// </summary>
    public double ShortStop => _shortStop;
}
