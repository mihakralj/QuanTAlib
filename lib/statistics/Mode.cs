using System;
using System.Linq;
namespace QuanTAlib;

/// <summary>
/// MODE: Most Frequent Value Measure
/// A statistical measure that identifies the most frequently occurring value(s)
/// in a dataset. When multiple values share the highest frequency, it returns
/// their average to provide a representative central value.
/// </summary>
/// <remarks>
/// The Mode calculation process:
/// 1. Groups values by frequency
/// 2. Identifies highest frequency group(s)
/// 3. Averages multiple modes if present
/// 4. Uses mean until period filled
///
/// Key characteristics:
/// - Identifies most common values
/// - Handles multiple modes
/// - Robust to distribution shape
/// - Useful for discrete data
/// - Returns actual data points
///
/// Formula:
/// mode = value with highest frequency count
/// if multiple modes: average of mode values
///
/// Market Applications:
/// - Identify common price levels
/// - Detect support/resistance zones
/// - Analyze volume clusters
/// - Find price congestion areas
/// - Pattern recognition
///
/// Sources:
///     https://en.wikipedia.org/wiki/Mode_(statistics)
///     "Statistical Analysis in Financial Markets"
///
/// Note: Particularly useful for price level analysis
/// </remarks>

public class Mode : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;

    /// <param name="period">The number of points to consider for mode calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Mode(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        Period = period;
        WarmupPeriod = period;
        _buffer = new CircularBuffer(period);
        Name = $"Mode(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for mode calculation.</param>
    public Mode(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _buffer.Clear();
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        double mode;
        if (_index >= Period)
        {
            // Group values by frequency and order by count
            var values = _buffer.GetSpan().ToArray();
            var groupedValues = values.GroupBy(v => v)
                                    .OrderByDescending(g => g.Count())
                                    .ThenBy(g => g.Key)
                                    .ToList();

            // Find all values with highest frequency
            int maxCount = groupedValues.First().Count();
            var modes = groupedValues.TakeWhile(g => g.Count() == maxCount)
                                   .Select(g => g.Key)
                                   .ToList();

            // Average multiple modes if present
            mode = modes.Average();
        }
        else
        {
            // Use average until we have enough data points
            mode = _buffer.Average();
        }

        IsHot = _index >= WarmupPeriod;
        return mode;
    }
}
