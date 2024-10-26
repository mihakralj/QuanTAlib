namespace QuanTAlib;

/// <summary>
/// Represents a Z-score calculator that measures how many standard deviations
/// an element is from the mean of a set of values.
/// </summary>
/// <remarks>
/// The Zscore class calculates the Z-score (also known as standard score) for
/// the most recent value in a given period. It uses a circular buffer to
/// efficiently manage the data points within the specified period.
///
/// In financial analysis, Z-score is important for:
/// - Identifying outliers or unusual price movements.
/// - Normalizing data across different scales or time periods.
/// - Assessing the relative position of a value within its historical distribution.
/// - Supporting trading strategies based on mean reversion or momentum.
/// </remarks>
public class Zscore : AbstractBase
{
    /// <summary>
    /// The number of data points to consider for the Z-score calculation.
    /// </summary>
    private readonly int Period;

    /// <summary>
    /// Circular buffer to store the most recent data points.
    /// </summary>
    private readonly CircularBuffer _buffer;

    /// <summary>
    /// Initializes a new instance of the Zscore class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the Z-score.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 2.
    /// </exception>
    public Zscore(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2 for Z-score calculation.");
        }
        Period = period;
        WarmupPeriod = 2;
        _buffer = new CircularBuffer(period);
        Name = $"ZScore(period={period})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Zscore class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the Z-score.</param>
    public Zscore(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Zscore instance by clearing the buffer.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
    }

    /// <summary>
    /// Manages the state of the Zscore instance based on whether a new value is being processed.
    /// </summary>
    /// <param name="isNew">Indicates whether the current input is a new value.</param>
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    /// <summary>
    /// Performs the Z-score calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated Z-score value for the most recent input in the current period.
    /// </returns>
    /// <remarks>
    /// This method calculates the Z-score using the formula:
    /// Z = (x - μ) / σ
    /// where x is the input value, μ is the mean of the period, and σ is the sample standard deviation.
    /// If there are fewer than 2 data points or if the standard deviation is 0, the method returns 0.
    ///
    /// Interpretation of results:
    /// - A Z-score of 0 indicates that the data point is exactly on the mean.
    /// - A positive Z-score indicates the data point is above the mean.
    /// - A negative Z-score indicates the data point is below the mean.
    /// - The magnitude of the Z-score represents how many standard deviations away from the mean the data point is.
    /// - In a normal distribution, about 68% of the values have a Z-score between -1 and 1,
    ///   95% between -2 and 2, and 99.7% between -3 and 3.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer.Add(Input.Value, Input.IsNew);

        double zScore = 0;
        if (_buffer.Count >= 2)
        {  // We need at least 2 data points for Z-score
            var values = _buffer.GetSpan().ToArray();
            double mean = values.Average();
            double n = values.Length;

            double sumSquaredDeviations = values.Sum(x => Math.Pow(x - mean, 2));
            double standardDeviation = Math.Sqrt(sumSquaredDeviations / (n - 1));  // Sample standard deviation

            if (standardDeviation != 0)
            {  // Avoid division by zero
                zScore = (Input.Value - mean) / standardDeviation;
            }
        }

        IsHot = _buffer.Count >= Period;
        return zScore;
    }
}
