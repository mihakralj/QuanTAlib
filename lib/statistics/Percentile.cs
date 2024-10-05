namespace QuanTAlib;

/// <summary>
/// Represents a percentile calculator that determines the value at a specified percentile
/// in a given period of data points.
/// </summary>
/// <remarks>
/// The Percentile class uses a circular buffer to store values and calculates the
/// percentile efficiently. It uses linear interpolation when the percentile falls
/// between two data points. Before the specified period is reached, it returns the
/// average of the available values as an approximation.
/// </remarks>
public class Percentile : AbstractBase {
    private readonly int Period;
    private readonly double Percent;
    private readonly CircularBuffer _buffer;

    /// <summary>
    /// Initializes a new instance of the Percentile class with the specified period and percentile.
    /// </summary>
    /// <param name="period">The period over which to calculate the percentile.</param>
    /// <param name="percent">The percentile to calculate (between 0 and 100).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 2 or percent is not between 0 and 100.
    /// </exception>
    public Percentile(int period, double percent) : base() {
        if (period < 2) {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2 for percentile calculation.");
        }
        if (percent < 0 || percent > 100) {
            throw new ArgumentOutOfRangeException(nameof(percent), "Percent must be between 0 and 100.");
        }
        Period = period;
        Percent = percent;
        WarmupPeriod = 2;
        _buffer = new CircularBuffer(period);
        Name = $"Percentile(period={period}, percent={percent})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Percentile class with the specified source, period, and percentile.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the percentile.</param>
    /// <param name="percent">The percentile to calculate (between 0 and 100).</param>
    public Percentile(object source, int period, double percent) : this(period, percent) {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Percentile instance by clearing the buffer.
    /// </summary>
    public override void Init() {
        base.Init();
        _buffer.Clear();
    }

    /// <summary>
    /// Manages the state of the Percentile instance based on whether a new value is being processed.
    /// </summary>
    /// <param name="isNew">Indicates whether the current input is a new value.</param>
    protected override void ManageState(bool isNew) {
        if (isNew) {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    /// <summary>
    /// Performs the percentile calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated percentile value for the current period.
    /// </returns>
    /// <remarks>
    /// This method uses linear interpolation when the percentile falls between two data points.
    /// Before the specified period is reached, it returns the average of the available values
    /// as an approximation. Once the period is reached, it calculates the true percentile by
    /// sorting the values and interpolating as necessary.
    /// </remarks>
    protected override double Calculation() {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        double result;
        if (_buffer.Count >= Period) {
            var values = _buffer.GetSpan().ToArray();
            Array.Sort(values);

            double position = (Percent / 100.0) * (values.Length - 1);
            int lowerIndex = (int)Math.Floor(position);
            int upperIndex = (int)Math.Ceiling(position);

            if (lowerIndex == upperIndex) {
                result = values[lowerIndex];
            } else {
                // Interpolate between the two nearest values
                double lowerValue = values[lowerIndex];
                double upperValue = values[upperIndex];
                double fraction = position - lowerIndex;
                result = lowerValue + (upperValue - lowerValue) * fraction;
            }
        } else {
            // Use average for insufficient data, like the Median class
            result = _buffer.Average();
        }

        IsHot = _buffer.Count >= Period;
        return result;
    }
}