namespace QuanTAlib;

/// <summary>
/// Represents a mode calculator that determines the most frequent value in a specified period.
/// If multiple values have the same highest frequency, it returns their average.
/// </summary>
/// <remarks>
/// The Mode class uses a circular buffer to store values and calculates the mode
/// efficiently. Before the specified period is reached, it returns the average of
/// the available values as an approximation.
/// </remarks>
public class Mode : AbstractBase {
    private readonly int Period;
    private readonly CircularBuffer _buffer;

    /// <summary>
    /// Initializes a new instance of the Mode class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the mode.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 1.
    /// </exception>
    public Mode(int period) : base() {
        if (period < 1) {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        Period = period;
        WarmupPeriod = period;
        _buffer = new CircularBuffer(period);
        Name = $"Mode(period={period})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Mode class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the mode.</param>
    public Mode(object source, int period) : this(period) {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Manages the state of the Mode instance based on whether a new value is being processed.
    /// </summary>
    /// <param name="isNew">Indicates whether the current input is a new value.</param>
    protected override void ManageState(bool isNew) {
        if (isNew) {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    /// <summary>
    /// Performs the mode calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated mode (most frequent value) for the current period.
    /// If multiple values have the same highest frequency, returns their average.
    /// </returns>
    /// <remarks>
    /// Before the specified period is reached, this method returns the average of
    /// the available values as an approximation of the mode. Once the period is
    /// reached, it calculates the true mode by grouping and counting the values.
    /// </remarks>
    protected override double Calculation() {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        double mode;
        if (_index >= Period) {
            var values = _buffer.GetSpan().ToArray();
            var groupedValues = values.GroupBy(v => v)
                                      .OrderByDescending(g => g.Count())
                                      .ThenBy(g => g.Key)
                                      .ToList();

            int maxCount = groupedValues.First().Count();
            var modes = groupedValues.TakeWhile(g => g.Count() == maxCount)
                                     .Select(g => g.Key)
                                     .ToList();

            mode = modes.Average(); // If there are multiple modes, we return their average
        } else {
            mode = _buffer.Average(); // Use average until we have enough data points
        }

        IsHot = _index >= WarmupPeriod;
        return mode;
    }
}
