using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// STOCH: Stochastic Oscillator
/// A momentum indicator that shows the location of the close relative to
/// high-low range over a period. Consists of %K (fast) and %D (slow) lines.
/// </summary>
/// <remarks>
/// The Stochastic calculation process:
/// 1. Calculate %K (raw stochastic):
///    - Find highest high and lowest low over period
///    - Calculate where current close is within this range
/// 2. Smooth %K with SMA to get Fast %K
/// 3. Smooth Fast %K with SMA to get %D (signal line)
///
/// Key characteristics:
/// - Oscillates between 0 and 100
/// - Traditional overbought level at 80
/// - Traditional oversold level at 20
/// - %K/%D crossovers signal momentum shifts
/// - Divergence with price shows potential reversals
///
/// Formula:
/// Raw %K = 100 * (Close - Lowest Low) / (Highest High - Lowest Low)
/// Fast %K = SMA(Raw %K, smoothK)
/// %D = SMA(Fast %K, smoothD)
///
/// Sources:
///     George Lane - "Lane's Stochastics" (1950s)
///     https://www.investopedia.com/terms/s/stochasticoscillator.asp
///
/// Note: Default periods (14,3,3) are commonly used values
/// </remarks>
[SkipLocalsInit]
public sealed class Stoch : AbstractBase
{
    private readonly CircularBuffer _highs;
    private readonly CircularBuffer _lows;
    private readonly Sma _fastK;
    private readonly Sma _slowD;
    private readonly CircularBuffer _rawK;
    private const int DefaultPeriod = 14;
    private const int DefaultSmoothK = 3;
    private const int DefaultSmoothD = 3;
    private const double ScalingFactor = 100.0;

    /// <param name="period">The lookback period (default 14).</param>
    /// <param name="smoothK">%K smoothing period (default 3).</param>
    /// <param name="smoothD">%D smoothing period (default 3).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stoch(int period = DefaultPeriod, int smoothK = DefaultSmoothK, int smoothD = DefaultSmoothD)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(smoothK, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(smoothD, 1);

        _highs = new(period);
        _lows = new(period);
        _rawK = new(smoothK);
        _fastK = new(smoothK);
        _slowD = new(smoothD);
        WarmupPeriod = period + Math.Max(smoothK, smoothD);
        Name = $"STOCH({period},{smoothK},{smoothD})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The lookback period.</param>
    /// <param name="smoothK">%K smoothing period.</param>
    /// <param name="smoothD">%D smoothing period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stoch(object source, int period = DefaultPeriod, int smoothK = DefaultSmoothK, int smoothD = DefaultSmoothD)
        : this(period, smoothK, smoothD)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _highs.Add(BarInput.High);
            _lows.Add(BarInput.Low);
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Calculate raw %K
        double highest = _highs.Max();
        double lowest = _lows.Min();
        double range = highest - lowest;
        double rawK = range >= double.Epsilon ? ((BarInput.Close - lowest) / range) * ScalingFactor : 0;

        if (BarInput.IsNew)
            _rawK.Add(rawK);

        // Calculate Fast %K (first smoothing)
        double fastK = _fastK.Calc(new TValue(BarInput.Time, rawK, BarInput.IsNew));

        // Calculate %D (second smoothing)
        return _slowD.Calc(new TValue(BarInput.Time, fastK, BarInput.IsNew));
    }

    /// <summary>
    /// Gets the %K line value (Fast Stochastic)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double K() => _fastK.Value;

    /// <summary>
    /// Gets the %D line value (Slow Stochastic)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double D() => Value;
}
