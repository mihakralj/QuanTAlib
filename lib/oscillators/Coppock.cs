using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// COPPOCK: Coppock Curve
/// A long-term momentum oscillator used to identify major bottoms in the market.
/// It is calculated using a weighted moving average of two different Rate of Change calculations.
/// </summary>
/// <remarks>
/// The Coppock Curve calculation process:
/// 1. Calculate 14-period Rate of Change (ROC)
/// 2. Calculate 11-period Rate of Change (ROC)
/// 3. Sum the two ROC values
/// 4. Apply 10-period Weighted Moving Average (WMA) to the sum
///
/// Key characteristics:
/// - Long-term momentum indicator
/// - Primarily used for monthly data
/// - Buy signals when curve turns up from below zero
/// - Rarely used for sell signals
/// - Designed to identify major bottoms in stock market indices
///
/// Formula:
/// COPPOCK = WMA(10) of (ROC(14) + ROC(11))
/// where:
/// ROC(n) = ((Price - Price[n]) / Price[n]) * 100
/// WMA is weighted moving average
///
/// Sources:
///     Edwin Coppock - Barron's Magazine (October 1962)
///     https://www.investopedia.com/terms/c/coppockcurve.asp
///
/// Note: Originally designed for monthly data with parameters (14,11,10),
/// but can be adapted for other timeframes
/// </remarks>
[SkipLocalsInit]
public sealed class Coppock : AbstractBase
{
    private readonly CircularBuffer _values;
    private readonly Wma _wma;
    private readonly int _roc1Period;
    private readonly int _roc2Period;
    private const int DefaultRoc1Period = 14;
    private const int DefaultRoc2Period = 11;
    private const int DefaultWmaPeriod = 10;

    /// <param name="roc1Period">The first ROC period (default 14).</param>
    /// <param name="roc2Period">The second ROC period (default 11).</param>
    /// <param name="wmaPeriod">The WMA smoothing period (default 10).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Coppock(int roc1Period = DefaultRoc1Period, int roc2Period = DefaultRoc2Period, int wmaPeriod = DefaultWmaPeriod)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(roc1Period, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(roc2Period, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(wmaPeriod, 1);

        _roc1Period = roc1Period;
        _roc2Period = roc2Period;
        int maxPeriod = Math.Max(roc1Period, roc2Period);
        _values = new(maxPeriod + 1);
        _wma = new(wmaPeriod);
        WarmupPeriod = maxPeriod + wmaPeriod;
        Name = $"COPPOCK({roc1Period},{roc2Period},{wmaPeriod})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="roc1Period">The first ROC period.</param>
    /// <param name="roc2Period">The second ROC period.</param>
    /// <param name="wmaPeriod">The WMA smoothing period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Coppock(object source, int roc1Period = DefaultRoc1Period, int roc2Period = DefaultRoc2Period, int wmaPeriod = DefaultWmaPeriod)
        : this(roc1Period, roc2Period, wmaPeriod)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _values.Add(Input.Value);
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double CalculateRoc(int period)
    {
        if (_index <= period) return 0;
        double currentValue = _values[0];
        double oldValue = _values[period];
        return ((currentValue - oldValue) / oldValue) * 100.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Calculate ROC values and their sum
        double roc1 = CalculateRoc(_roc1Period);
        double roc2 = CalculateRoc(_roc2Period);
        double rocSum = roc1 + roc2;

        // Not enough data for WMA calculation
        if (_index <= Math.Max(_roc1Period, _roc2Period))
            return 0;

        // Calculate WMA of ROC sums
        return _wma.Calc(new TValue(Input.Time, rocSum, Input.IsNew));
    }
}
