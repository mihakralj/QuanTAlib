using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// FCB: Fractal Chaos Bands
/// Adaptive price bands based on fractal geometry concepts,
/// identifying potential support and resistance levels.
/// </summary>
/// <remarks>
/// The FCB calculation process:
/// 1. Identify fractal highs and lows over the period
/// 2. Calculate high and low bands using fractal points
/// 3. Smooth bands using exponential moving average
///
/// Key characteristics:
/// - Adapts to market structure
/// - Default period is 20 days
/// - Default smoothing factor is 0.5
/// - Returns upper and lower bands
/// - Based on fractal geometry concepts
///
/// Formula:
/// Fractal High = High[t] where High[t] > High[t±1,2]
/// Fractal Low = Low[t] where Low[t] < Low[t±1,2]
/// Upper Band = EMA(Fractal Highs, smoothing)
/// Lower Band = EMA(Fractal Lows, smoothing)
///
/// Market Applications:
/// - Support/resistance identification
/// - Trend analysis
/// - Volatility measurement
/// - Breakout detection
/// - Trading range analysis
///
/// Sources:
///     Bill Williams' Chaos Theory
///     Trading Chaos (2nd Edition) by Bill Williams
///
/// Note: Returns three values: upper, middle, and lower bands
/// </remarks>
[SkipLocalsInit]
public sealed class Fcb : AbstractBase
{
    private readonly double _smoothing;
    private readonly CircularBuffer _highs;
    private readonly CircularBuffer _lows;
    private double _upperBand;
    private double _middleBand;
    private double _lowerBand;
    private double _upperEma;
    private double _lowerEma;
    private readonly double _alpha;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Fcb(int period = 20, double smoothing = 0.5)
    {
        _smoothing = smoothing;
        _alpha = 2.0 / (period + 1);
        WarmupPeriod = period + 4;  // Need extra periods for fractal identification
        Name = $"FCB({period},{_smoothing})";
        _highs = new CircularBuffer(period);
        _lows = new CircularBuffer(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Fcb(object source, int period = 20, double smoothing = 0.5) : this(period, smoothing)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _upperBand = 0;
        _middleBand = 0;
        _lowerBand = 0;
        _upperEma = 0;
        _lowerEma = 0;
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

        // Add current high/low to buffers
        _highs.Add(BarInput.High);
        _lows.Add(BarInput.Low);

        // Need enough values for calculation
        if (_index <= 4)
        {
            return 0;
        }

        // Check for fractal patterns
        bool isFractalHigh = false;
        bool isFractalLow = false;

        // Fractal high: current high is higher than 2 bars before and after
        isFractalHigh = _highs[2] > _highs[0] && _highs[2] > _highs[1] &&
                       _highs[2] > _highs[3] && _highs[2] > _highs[4];

        // Fractal low: current low is lower than 2 bars before and after
        isFractalLow = _lows[2] < _lows[0] && _lows[2] < _lows[1] &&
                      _lows[2] < _lows[3] && _lows[2] < _lows[4];


        // Update EMAs with fractal points
        if (isFractalHigh)
        {
            _upperEma = (_alpha * _highs[2]) + ((1 - _alpha) * _upperEma);
        }
        if (isFractalLow)
        {
            _lowerEma = (_alpha * _lows[2]) + ((1 - _alpha) * _lowerEma);
        }

        // Apply smoothing to bands
        _upperBand = (_smoothing * _upperEma) + ((1 - _smoothing) * BarInput.High);
        _lowerBand = (_smoothing * _lowerEma) + ((1 - _smoothing) * BarInput.Low);
        _middleBand = (_upperBand + _lowerBand) / 2;

        IsHot = _index >= WarmupPeriod;
        return _middleBand;  // Return middle band as primary value
    }

    /// <summary>
    /// Gets the upper band value
    /// </summary>
    public double UpperBand => _upperBand;

    /// <summary>
    /// Gets the middle band value
    /// </summary>
    public double MiddleBand => _middleBand;

    /// <summary>
    /// Gets the lower band value
    /// </summary>
    public double LowerBand => _lowerBand;
}
