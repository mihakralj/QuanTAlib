using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// BBAND: Bollinger Bands®
/// A technical analysis tool that creates a band of three lines:
/// - Middle Band: n-period simple moving average (SMA)
/// - Upper Band: Middle Band + (standard deviation * multiplier)
/// - Lower Band: Middle Band - (standard deviation * multiplier)
/// </summary>
/// <remarks>
/// The Bollinger Bands calculation process:
/// 1. Calculate the middle band (SMA of closing prices)
/// 2. Calculate the standard deviation of prices
/// 3. Upper and lower bands are the middle band +/- standard deviation * multiplier
///
/// Key characteristics:
/// - Adapts to volatility
/// - Default period is 20 days
/// - Default multiplier is 2.0
/// - Returns three bands (upper, middle, lower)
/// - Wider bands indicate higher volatility
/// - Narrower bands indicate lower volatility
///
/// Formula:
/// Middle Band = SMA(Close, period)
/// Standard Deviation = SQRT(SUM((Close - Middle Band)^2) / period)
/// Upper Band = Middle Band + (multiplier * Standard Deviation)
/// Lower Band = Middle Band - (multiplier * Standard Deviation)
///
/// Market Applications:
/// - Volatility measurement
/// - Overbought/oversold identification
/// - Price breakout detection
/// - Trend strength analysis
/// - Dynamic support/resistance levels
///
/// Sources:
///     John Bollinger (1980s)
///     https://www.bollingerbands.com
///
/// Note: Returns three values: upper, middle, and lower bands
/// </remarks>
[SkipLocalsInit]
public sealed class Bband : AbstractBase
{
    private readonly int _period;
    private readonly double _multiplier;
    private readonly CircularBuffer _prices;
    private double _middleBand;
    private double _upperBand;
    private double _lowerBand;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bband(int period = 20, double multiplier = 2.0)
    {
        _period = period;
        _multiplier = multiplier;
        WarmupPeriod = period;
        Name = $"BBAND({_period},{_multiplier})";
        _prices = new CircularBuffer(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bband(object source, int period = 20, double multiplier = 2.0) : this(period, multiplier)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _middleBand = 0;
        _upperBand = 0;
        _lowerBand = 0;
        _prices.Clear();
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

        // Add current price to buffer
        _prices.Add(BarInput.Close);

        // Need enough values for calculation
        if (_index <= _period)
        {
            return 0;
        }

        // Calculate middle band (SMA)
        _middleBand = _prices.Average();

        // Calculate standard deviation
        double sumSquaredDeviations = 0;
        for (int i = 0; i < _period; i++)
        {
            double deviation = _prices[i] - _middleBand;
            sumSquaredDeviations += deviation * deviation;
        }
        double standardDeviation = Math.Sqrt(sumSquaredDeviations / _period);

        // Calculate bands
        double bandWidth = _multiplier * standardDeviation;
        _upperBand = _middleBand + bandWidth;
        _lowerBand = _middleBand - bandWidth;

        IsHot = _index >= WarmupPeriod;
        return _middleBand;  // Return middle band as primary value
    }

    /// <summary>
    /// Gets the upper band value
    /// </summary>
    public double UpperBand => _upperBand;

    /// <summary>
    /// Gets the middle band value (SMA)
    /// </summary>
    public double MiddleBand => _middleBand;

    /// <summary>
    /// Gets the lower band value
    /// </summary>
    public double LowerBand => _lowerBand;
}
