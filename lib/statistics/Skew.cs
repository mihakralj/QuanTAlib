namespace QuanTAlib;

/// <summary>
/// Represents a skewness calculator that measures the asymmetry of the probability
/// distribution of a real-valued random variable about its mean.
/// </summary>
/// <remarks>
/// The Skew class uses a circular buffer to store values and calculates the skewness
/// efficiently. It uses the adjusted Fisher-Pearson standardized moment coefficient
/// for sample skewness calculation. A minimum of 3 data points is required for the
/// calculation.
///
/// In financial analysis, skewness is important for:
/// - Assessing the asymmetry of returns distribution.
/// - Evaluating the risk of extreme events in either direction.
/// - Complementing other risk measures like standard deviation.
/// - Informing investment decisions and risk management strategies.
///
/// Positive skewness indicates a longer tail on the right side of the distribution,
/// while negative skewness indicates a longer tail on the left side.
/// </remarks>
public class Skew : AbstractBase
{
    /// <summary>
    /// The number of data points to consider for the skewness calculation.
    /// </summary>
    private readonly int Period;
    private readonly CircularBuffer _buffer;

    /// <summary>
    /// Initializes a new instance of the Skew class with the specified period.
    /// </summary>
    /// <param name="period">The period over which to calculate the skewness.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 3.
    /// </exception>
    public Skew(int period)
    {
        if (period < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 3 for skewness calculation.");
        }
        Period = period;
        WarmupPeriod = 3;
        _buffer = new CircularBuffer(period);
        Name = $"Skew(period={period})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Skew class with the specified source and period.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the skewness.</param>
    public Skew(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Skew instance by clearing the buffer.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
    }

    /// <summary>
    /// Manages the state of the Skew instance based on whether a new value is being processed.
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
    /// Performs the skewness calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated skewness value for the current period.
    /// </returns>
    /// <remarks>
    /// This method uses the adjusted Fisher-Pearson standardized moment coefficient
    /// to calculate the sample skewness. It requires at least 3 data points for the
    /// calculation. If there are fewer than 3 data points, or if the standard
    /// deviation is zero, the method returns 0.
    ///
    /// Interpretation of results:
    /// - Positive values indicate right-skewed distribution (longer tail on the right side).
    /// - Negative values indicate left-skewed distribution (longer tail on the left side).
    /// - Values close to 0 suggest a relatively symmetric distribution.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer.Add(Input.Value, Input.IsNew);

        double skew = 0;
        if (_buffer.Count >= 3)
        {  // We need at least 3 data points for skewness
            var values = _buffer.GetSpan().ToArray();
            double mean = values.Average();
            double n = values.Length;

            double sumCubedDeviations = 0;
            double sumSquaredDeviations = 0;

            foreach (var value in values)
            {
                double deviation = value - mean;
                sumCubedDeviations += Math.Pow(deviation, 3);
                sumSquaredDeviations += Math.Pow(deviation, 2);
            }

            // Calculate sample skewness using the adjusted Fisher-Pearson standardized moment coefficient
            double m3 = sumCubedDeviations / n;
            double m2 = sumSquaredDeviations / n;
            double s3 = Math.Pow(m2, 1.5);

            if (s3 != 0)
            {  // Avoid division by zero
                skew = (Math.Sqrt(n * (n - 1)) / (n - 2)) * (m3 / s3);
            }
        }

        IsHot = _buffer.Count >= Period;
        return skew;
    }
}
