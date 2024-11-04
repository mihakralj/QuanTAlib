using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// WILLR: Williams %R
/// A momentum oscillator that measures the level of the close relative to the
/// highest high for a look-back period. Similar to Stochastic Oscillator but
/// with a reversed scale and no smoothing.
/// </summary>
/// <remarks>
/// The Williams %R calculation process:
/// 1. Find highest high and lowest low over period
/// 2. Calculate where current close is within this range
/// 3. Scale result to -100 to 0 range
///
/// Key characteristics:
/// - Oscillates between -100 and 0
/// - Similar to Stochastic but no smoothing
/// - Traditional overbought level at -20
/// - Traditional oversold level at -80
/// - Leading indicator for market tops/bottoms
///
/// Formula:
/// %R = -100 * (Highest High - Close) / (Highest High - Lowest Low)
///
/// Sources:
///     Larry Williams - "How I Made One Million Dollars Last Year Trading Commodities" (1973)
///     https://www.investopedia.com/terms/w/williamsr.asp
///
/// Note: Default period of 14 is commonly used
/// </remarks>
[SkipLocalsInit]
public sealed class Willr : AbstractBase
{
    private readonly CircularBuffer _highs;
    private readonly CircularBuffer _lows;
    private const int DefaultPeriod = 14;
    private const double ScalingFactor = -100.0;

    /// <param name="period">The lookback period (default 14).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Willr(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _highs = new(period);
        _lows = new(period);
        WarmupPeriod = period;
        Name = $"WILLR({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The lookback period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Willr(object source, int period = DefaultPeriod)
        : this(period)
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

        double highest = _highs.Max();
        double lowest = _lows.Min();
        double range = highest - lowest;

        return range >= double.Epsilon ? ScalingFactor * ((highest - BarInput.Close) / range) : 0;
    }
}
