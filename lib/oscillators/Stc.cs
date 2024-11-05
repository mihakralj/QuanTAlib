using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// STC: Schaff Trend Cycle
/// A trend-following indicator that combines MACD and stochastic concepts
/// to create a smoother, more responsive indicator with less noise.
/// </summary>
/// <remarks>
/// The STC calculation process:
/// 1. Calculate MACD-style momentum using EMAs
/// 2. Apply double stochastic formula to smooth the momentum
/// 3. Scale result to oscillator range
///
/// Key characteristics:
/// - Oscillates between 0 and 100
/// - Combines trend and momentum
/// - Double smoothing reduces noise
/// - Traditional overbought level at 75
/// - Traditional oversold level at 25
///
/// Formula:
/// Momentum = EMA1(Close) - EMA2(Close)
/// First Stochastic:
/// %K1 = 100 * (Momentum - Lowest Low) / (Highest High - Lowest Low)
/// %D1 = EMA(%K1)
/// Second Stochastic:
/// %K2 = 100 * (%D1 - Lowest %D1) / (Highest %D1 - Lowest %D1)
/// STC = EMA(%K2)
///
/// Sources:
///     Doug Schaff - "The Schaff Trend Cycle" (1999)
///     https://www.tradingview.com/script/o6tSS6Hn-Schaff-Trend-Cycle/
///
/// Note: Default periods (23,10,3) were recommended by Schaff
/// </remarks>
[SkipLocalsInit]
public sealed class Stc : AbstractBase
{
    private readonly Ema _fastEma;
    private readonly Ema _slowEma;
    private readonly CircularBuffer _macdValues;
    private readonly CircularBuffer _k1Values;
    private readonly CircularBuffer _d1Values;
    private readonly Ema _d1Ema;
    private readonly Ema _stcEma;
    private const int DefaultCyclePeriod = 10;
    private const int DefaultFastPeriod = 23;
    private const int DefaultSlowPeriod = 50;
    private const int DefaultD1Period = 3;
    private const int DefaultStcPeriod = 3;
    private const double ScalingFactor = 100.0;

    /// <param name="cyclePeriod">The lookback period for highs/lows (default 10).</param>
    /// <param name="fastPeriod">Fast EMA period (default 23).</param>
    /// <param name="slowPeriod">Slow EMA period (default 50).</param>
    /// <param name="d1Period">First %D smoothing period (default 3).</param>
    /// <param name="stcPeriod">Final STC smoothing period (default 3).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stc(int cyclePeriod = DefaultCyclePeriod, int fastPeriod = DefaultFastPeriod,
               int slowPeriod = DefaultSlowPeriod, int d1Period = DefaultD1Period,
               int stcPeriod = DefaultStcPeriod)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cyclePeriod, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(fastPeriod, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(slowPeriod, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(d1Period, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(stcPeriod, 1);

        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(fastPeriod), "Fast period must be less than slow period");
        }

        _fastEma = new(fastPeriod);
        _slowEma = new(slowPeriod);
        _macdValues = new(cyclePeriod);
        _k1Values = new(cyclePeriod);
        _d1Values = new(cyclePeriod);
        _d1Ema = new(d1Period);
        _stcEma = new(stcPeriod);

        WarmupPeriod = slowPeriod + cyclePeriod + Math.Max(d1Period, stcPeriod);
        Name = $"STC({cyclePeriod},{fastPeriod},{slowPeriod},{d1Period},{stcPeriod})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="cyclePeriod">The lookback period for highs/lows.</param>
    /// <param name="fastPeriod">Fast EMA period.</param>
    /// <param name="slowPeriod">Slow EMA period.</param>
    /// <param name="d1Period">First %D smoothing period.</param>
    /// <param name="stcPeriod">Final STC smoothing period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stc(object source, int cyclePeriod = DefaultCyclePeriod, int fastPeriod = DefaultFastPeriod,
               int slowPeriod = DefaultSlowPeriod, int d1Period = DefaultD1Period,
               int stcPeriod = DefaultStcPeriod)
        : this(cyclePeriod, fastPeriod, slowPeriod, d1Period, stcPeriod)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew) _index++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateStochastic(double value, double highest, double lowest)
    {
        double range = highest - lowest;
        return range >= double.Epsilon ? ((value - lowest) / range) * ScalingFactor : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Calculate MACD-style momentum
        double fastEma = _fastEma.Calc(Input);
        double slowEma = _slowEma.Calc(Input);
        double macd = fastEma - slowEma;

        if (Input.IsNew)
            _macdValues.Add(macd);

        // First stochastic
        double k1 = CalculateStochastic(macd, _macdValues.Max(), _macdValues.Min());
        if (Input.IsNew)
            _k1Values.Add(k1);

        double d1 = _d1Ema.Calc(new TValue(Input.Time, k1, Input.IsNew));
        if (Input.IsNew)
            _d1Values.Add(d1);

        // Second stochastic
        double k2 = CalculateStochastic(d1, _d1Values.Max(), _d1Values.Min());

        // Final smoothing
        return _stcEma.Calc(new TValue(Input.Time, k2, Input.IsNew));
    }
}
