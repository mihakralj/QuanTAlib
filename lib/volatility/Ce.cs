using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CE: Chandelier Exit
/// A volatility-based stop-loss indicator that adapts to market conditions,
/// using ATR to set stop levels above/below recent price extremes.
/// </summary>
/// <remarks>
/// The CE calculation process:
/// 1. Calculate highest high and lowest low over the period
/// 2. Calculate ATR over the period
/// 3. Long Exit = Highest High - (ATR * multiplier)
/// 4. Short Exit = Lowest Low + (ATR * multiplier)
///
/// Key characteristics:
/// - Adapts to market volatility
/// - Default period is 22 days
/// - Default multiplier is 3.0
/// - Returns both long and short exit levels
/// - Based on ATR and price extremes
///
/// Formula:
/// ATR = Average(TR, period)
/// Long Exit = Highest High[period] - (multiplier * ATR)
/// Short Exit = Lowest Low[period] + (multiplier * ATR)
///
/// Market Applications:
/// - Stop loss placement
/// - Position management
/// - Trend following
/// - Risk control
/// - Exit strategy
///
/// Sources:
///     Chuck LeBeau
///     https://www.investopedia.com/terms/c/chandelier-exit.asp
///
/// Note: Returns two values: long exit and short exit levels
/// </remarks>

[SkipLocalsInit]
public sealed class Ce : AbstractBase
{
    private readonly int _period;
    private readonly double _multiplier;
    private readonly CircularBuffer _tr;
    private readonly CircularBuffer _highs;
    private readonly CircularBuffer _lows;
    private double _prevClose;
    private double _longExit;
    private double _shortExit;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ce(int period = 22, double multiplier = 3.0)
    {
        _period = period;
        _multiplier = multiplier;
        WarmupPeriod = period + 1;  // Need one extra period for TR
        Name = $"CE({_period},{_multiplier})";
        _tr = new CircularBuffer(period);
        _highs = new CircularBuffer(period);
        _lows = new CircularBuffer(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ce(object source, int period = 22, double multiplier = 3.0) : this(period, multiplier)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _longExit = 0;
        _shortExit = 0;
        _tr.Clear();
        _highs.Clear();
        _lows.Clear();
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
            return 0;
        }

        // Calculate True Range
        double tr = Math.Max(BarInput.High - BarInput.Low,
                   Math.Max(Math.Abs(BarInput.High - _prevClose),
                          Math.Abs(BarInput.Low - _prevClose)));

        // Add values to buffers
        _tr.Add(tr);
        _highs.Add(BarInput.High);
        _lows.Add(BarInput.Low);

        // Store current close for next calculation
        _prevClose = BarInput.Close;

        // Need enough values for calculation
        if (_index <= _period)
        {
            return 0;
        }

        // Calculate ATR
        double atr = _tr.Average();

        // Find highest high and lowest low
        double highestHigh = double.MinValue;
        double lowestLow = double.MaxValue;
        for (int i = 0; i < _period; i++)
        {
            highestHigh = Math.Max(highestHigh, _highs[i]);
            lowestLow = Math.Min(lowestLow, _lows[i]);
        }

        // Calculate exit levels
        _longExit = highestHigh - (_multiplier * atr);
        _shortExit = lowestLow + (_multiplier * atr);

        IsHot = _index >= WarmupPeriod;
        return _longExit;  // Return long exit as primary value
    }

    /// <summary>
    /// Gets the long exit level
    /// </summary>
    public double LongExit => _longExit;

    /// <summary>
    /// Gets the short exit level
    /// </summary>
    public double ShortExit => _shortExit;
}
