using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// SMA: Simple Moving Average
/// The most basic form of moving average, calculating the arithmetic mean over a
/// specified period. Each data point in the period has equal weight in the
/// calculation.
/// </summary>
/// <remarks>
/// The SMA calculation process:
/// 1. Maintains a circular buffer of the last 'period' values
/// 2. Maintains a running sum for O(1) calculation
/// 3. Updates: sum = sum - oldest + newest
/// 4. Returns sum / count for the average
///
/// Key characteristics:
/// - Equal weight for all values in the period
/// - O(1) time complexity using running sum
/// - Simple and straightforward calculation
/// - Significant lag due to equal weighting
/// - Smooth output with good noise reduction
/// - Most basic form of trend following
///
/// Sources:
///     https://www.investopedia.com/terms/s/sma.asp
///     https://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:moving_averages
/// </remarks>
[SkipLocalsInit]
public sealed class Sma : AbstractBase
{
    private readonly CircularBuffer _buffer;
    private double _sum, _p_sum;
    private double _lastValue, _p_lastValue;

    /// <param name="period">The number of data points used in the SMA calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sma(int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        _buffer = new CircularBuffer(period);
        Name = $"Sma({period})";
        WarmupPeriod = period;
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of data points used in the SMA calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_sum = _sum;
            _p_lastValue = _lastValue;
        }
        else
        {
            _sum = _p_sum;
            _lastValue = _p_lastValue;
        }
    }

    /// <summary>
    /// Performs the core SMA calculation using O(1) running sum algorithm.
    /// </summary>
    /// <returns>The calculated SMA value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double oldValue;
        if (Input.IsNew)
        {
            oldValue = _buffer.Count == _buffer.Capacity ? _buffer.Oldest() : 0.0;
            _lastValue = Input.Value;
        }
        else
        {
            oldValue = _lastValue;
        }

        _sum = _sum - oldValue + Input.Value;
        _buffer.Add(Input.Value, Input.IsNew);

        IsHot = _index >= WarmupPeriod;
        return _sum / _buffer.Count;
    }
}
