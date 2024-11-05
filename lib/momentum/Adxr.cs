using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// ADXR: Average Directional Movement Index Rating
/// A momentum indicator that measures the strength of a trend by comparing
/// the current ADX value with its value from a specified number of periods ago.
/// </summary>
/// <remarks>
/// The ADXR calculation process:
/// 1. Calculate current ADX
/// 2. Get ADX value from n periods ago
/// 3. Average the two values
///
/// Key characteristics:
/// - Oscillates between 0 and 100
/// - Values above 25 indicate strong trend
/// - Values below 20 indicate weak or no trend
/// - Can be used to confirm trend strength
/// - Helps identify potential trend reversals
///
/// Formula:
/// ADXR = (Current ADX + ADX n periods ago) / 2
///
/// Sources:
///     J. Welles Wilder Jr. - "New Concepts in Technical Trading Systems" (1978)
///     https://www.investopedia.com/terms/a/adxr.asp
/// </remarks>
public sealed class Adxr : AbstractBarBase
{
    private readonly Adx _currentAdx;
    private readonly CircularBuffer _adxHistory;
    private readonly int _period;

    /// <param name="period">The number of periods used in the ADXR calculation (default 14).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Adxr(int period = 14)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        _currentAdx = new(period);
        _adxHistory = new(period);
        _period = period;
        WarmupPeriod = period * 3;  // Need extra periods for ADX calculation and history
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
        {
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Calculate current ADX
        double currentAdx = _currentAdx.Calc(Input);
        _adxHistory.Add(currentAdx, Input.IsNew);

        // Calculate ADXR once we have enough history
        if (_index > _period)
        {
            return (currentAdx + _adxHistory[^_period]) * 0.5;
        }

        return currentAdx;
    }
}
