using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// JVOLTY: Jurik Volatility
/// An advanced volatility measure developed by Mark Jurik that combines adaptive
/// bands with JMA smoothing. JVOLTY provides a sophisticated approach to measuring
/// market volatility with reduced noise and better responsiveness.
/// </summary>
/// <remarks>
/// The JVOLTY calculation process:
/// 1. Calculates adaptive price bands
/// 2. Measures volatility from band distances
/// 3. Applies volatility normalization
/// 4. Uses JMA-style smoothing
/// 5. Provides multiple outputs
///
/// Key characteristics:
/// - Adaptive measurement
/// - Noise reduction
/// - Multiple timeframe analysis
/// - Price band integration
/// - Volatility normalization
///
/// Formula:
/// volty = max(|price - upperBand|, |price - lowerBand|)
/// bands = adaptive calculation using Jurik's methods
/// final = JMA smoothing of normalized volatility
///
/// Market Applications:
/// - Dynamic position sizing
/// - Adaptive stop placement
/// - Volatility breakout systems
/// - Risk management
/// - Market regime detection
///
/// Sources:
///     Mark Jurik Research
///     https://www.jurikresearch.com/
///
/// Note: Proprietary enhancement of volatility measurement
/// </remarks>

[SkipLocalsInit]
public sealed class Jvolty : AbstractBase
{
    private readonly int _period;
    private readonly double _phase;
    private readonly CircularBuffer _vsumBuff;
    private readonly CircularBuffer _avoltyBuff;
    private readonly double _beta;
    private const double Epsilon = 1e-10;
    private const int DefaultPhase = 0;
    private const int VsumBufferSize = 10;
    private const int AvoltyBufferSize = 65;

    private double _len1;
    private double _pow1;
    private double _upperBand, _lowerBand, _p_upperBand, _p_lowerBand;
    private double _prevMa1, _prevDet0, _prevDet1, _prevJma, _p_prevMa1, _p_prevDet0, _p_prevDet1, _p_prevJma;
    private double _vSum, _p_vSum;

    public double UpperBand { get; private set; }
    public double LowerBand { get; private set; }
    public double Volty { get; private set; }
    public double VSum { get; private set; }
    public double Jma { get; private set; }
    public double AvgVolty { get; private set; }

    /// <param name="period">The number of periods for volatility calculation.</param>
    /// <param name="phase">Phase parameter for JMA smoothing (default 0).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Jvolty(int period, int phase = DefaultPhase)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 1.");
        }
        _period = period;
        _phase = Math.Clamp((phase * 0.01) + 1.5, 0.5, 2.5);

        _vsumBuff = new CircularBuffer(VsumBufferSize);
        _avoltyBuff = new CircularBuffer(AvoltyBufferSize);
        _beta = 0.45 * (period - 1) / (0.45 * (period - 1) + 2);

        WarmupPeriod = period * 2;
        Name = $"JVOLTY({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for volatility calculation.</param>
    /// <param name="phase">Phase parameter for JMA smoothing (default 0).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Jvolty(object source, int period, int phase = DefaultPhase) : this(period, phase)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _upperBand = _lowerBand = 0.0;
        _p_upperBand = _p_lowerBand = 0.0;
        _len1 = Math.Max((Math.Log(Math.Sqrt(_period - 1)) / Math.Log(2.0)) + 2.0, 0);
        _pow1 = Math.Max(_len1 - 2.0, 0.5);
        _avoltyBuff.Clear();
        _vsumBuff.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_upperBand = _upperBand;
            _p_lowerBand = _lowerBand;
            _p_vSum = _vSum;
            _p_prevMa1 = _prevMa1;
            _p_prevDet0 = _prevDet0;
            _p_prevDet1 = _prevDet1;
            _p_prevJma = _prevJma;
        }
        else
        {
            _upperBand = _p_upperBand;
            _lowerBand = _p_lowerBand;
            _vSum = _p_vSum;
            _prevMa1 = _p_prevMa1;
            _prevDet0 = _p_prevDet0;
            _prevDet1 = _p_prevDet1;
            _prevJma = _p_prevJma;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateVolatility(double price, double upperBand, double lowerBand)
    {
        double del1 = price - upperBand;
        double del2 = price - lowerBand;
        return Math.Max(Math.Abs(del1), Math.Abs(del2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double CalculateNormalizedVolatility(double volty, double avgVolty)
    {
        double rvolty = (avgVolty > Epsilon) ? volty / avgVolty : 1;
        return Math.Min(Math.Max(rvolty, 1.0), Math.Pow(_len1, 1.0 / _pow1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double CalculateJma(double price, double alpha, double ma1)
    {
        double det0 = (price - ma1) * (1 - _beta) + _beta * _prevDet0;
        _prevDet0 = det0;
        double ma2 = ma1 + _phase * det0;

        double det1 = ((ma2 - _prevJma) * (1 - alpha) * (1 - alpha)) + (alpha * alpha * _prevDet1);
        _prevDet1 = det1;
        double jma = _prevJma + det1;
        _prevJma = jma;

        return jma;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double price = Input.Value;
        if (_index == 1)
        {
            _upperBand = _lowerBand = price;
        }

        // Calculate volatility from band distances
        double volty = CalculateVolatility(price, _upperBand, _lowerBand);

        // Calculate moving averages of volatility
        _vsumBuff.Add(volty, Input.IsNew);
        _vSum += (_vsumBuff[^1] - _vsumBuff[0]) / VsumBufferSize;
        _avoltyBuff.Add(_vSum, Input.IsNew);
        double avgvolty = _avoltyBuff.Average();

        // Normalize and adjust volatility
        double rvolty = CalculateNormalizedVolatility(volty, avgvolty);
        double pow2 = Math.Pow(rvolty, _pow1);
        double Kv = Math.Pow(_beta, Math.Sqrt(pow2));

        // Update adaptive bands
        double del1 = price - _upperBand;
        double del2 = price - _lowerBand;
        _upperBand = (del1 >= 0) ? price : price - (Kv * del1);
        _lowerBand = (del2 <= 0) ? price : price - (Kv * del2);

        // Apply JMA smoothing
        double alpha = Math.Pow(_beta, pow2);
        double ma1 = (1 - alpha) * price + alpha * _prevMa1;
        _prevMa1 = ma1;

        double jma = CalculateJma(price, alpha, ma1);

        // Update public properties
        UpperBand = _upperBand;
        LowerBand = _lowerBand;
        Volty = volty;
        VSum = _vSum;
        AvgVolty = avgvolty;
        Jma = jma;

        IsHot = _index >= WarmupPeriod;
        return volty;
    }
}
