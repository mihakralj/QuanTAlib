using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// ADXR: Average Directional Movement Index Rating
/// A momentum indicator that measures trend strength by comparing the current ADX
/// value with a historical ADX value. ADXR helps identify potential trend
/// reversals earlier than standard ADX.
/// </summary>
/// <remarks>
/// The ADXR calculation process:
/// 1. Calculate current period ADX
/// 2. Calculate historical period ADX (shifted back by period)
/// 3. Average the current and historical ADX values
///
/// Key characteristics:
/// - Oscillates between 0 and 100
/// - Values above 25 indicate strong trend
/// - Values below 20 indicate weak or no trend
/// - Faster at identifying trend changes than ADX
/// - Does not indicate trend direction, only strength
///
/// Formula:
/// ADXR = (Current ADX + Historical ADX) / 2
/// where:
/// Historical ADX = ADX value from 'period' bars ago
///
/// Sources:
///     J. Welles Wilder Jr. - "New Concepts in Technical Trading Systems" (1978)
///     https://www.investopedia.com/terms/a/adxr.asp
///
/// Note: Default period of 14 was recommended by Wilder
/// </remarks>

[SkipLocalsInit]
public sealed class Adxr : AbstractBarBase
{
    private readonly Adx _currentAdx;
    private readonly CircularBuffer _historicalAdx;
    private const int DefaultPeriod = 14;

    /// <param name="period">The number of periods used in the ADXR calculation (default 14).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Adxr(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        _currentAdx = new(period);
        _historicalAdx = new(period);
        _index = 0;
        WarmupPeriod = period * 3;  // Need extra periods for historical ADX
        Name = $"ADXR({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in the ADXR calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Adxr(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
            _index++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Calculate current ADX
        double currentAdx = _currentAdx.Value;
        _currentAdx.Calc(Input);

        // Store ADX value in historical buffer
        _historicalAdx.Add(currentAdx, Input.IsNew);

        // Calculate ADXR once we have enough historical data
        if (_index > _historicalAdx.Capacity)
            return (currentAdx + _historicalAdx.Oldest()) / 2.0;

        return currentAdx;
    }
}
