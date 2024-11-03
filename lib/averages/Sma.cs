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
/// 1. Maintains a buffer of the last 'period' values
/// 2. Calculates arithmetic mean of all values in the buffer
/// 3. Updates buffer with new values in FIFO manner
///
/// Key characteristics:
/// - Equal weight for all values in the period
/// - Simple and straightforward calculation
/// - Significant lag due to equal weighting
/// - Smooth output with good noise reduction
/// - Most basic form of trend following
///
/// Sources:
///     https://www.investopedia.com/terms/s/sma.asp
///     https://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:moving_averages
/// </remarks>
public class Sma : AbstractBase
{
    private readonly CircularBuffer _buffer;

    /// <param name="period">The number of data points used in the SMA calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Sma(int period)
    {
        if (period < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _buffer = new CircularBuffer(period);
        Name = "Sma";
        WarmupPeriod = period;
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of data points used in the SMA calculation.</param>
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
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    /// <summary>
    /// Performs the core SMA calculation using the circular buffer's average.
    /// </summary>
    /// <returns>The calculated SMA value.</returns>
    protected override double Calculation()
    {
        ManageState(IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        IsHot = _index >= WarmupPeriod;
        return _buffer.Average();
    }
}
