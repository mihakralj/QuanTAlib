namespace QuanTAlib;

/// <summary>
/// Calculates excess kurtosis using the Sheskin Algorithm.
/// Measures the "tailedness" of the probability distribution of a real-valued random variable.
/// </summary>
public class Kurtosis : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;

    /// <summary>
    /// Initializes a new instance of the Kurtosis class.
    /// </summary>
    /// <param name="period">The number of data points to consider for calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the period is less than 4.
    /// </exception>
    public Kurtosis(int period) : base()
    {
        if (period < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 4 for kurtosis calculation.");
        }
        Period = period;
        WarmupPeriod = Period - 1;
        _buffer = new CircularBuffer(period);
        Name = $"Kurtosis(period={period})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Kurtosis class with a data source.
    /// </summary>
    /// <param name="source">The source object that publishes data.</param>
    /// <param name="period">The number of data points to consider.</param>
    public Kurtosis(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Resets the Kurtosis indicator to its initial state.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
    }

    /// <summary>
    /// Manages the state of the indicator.
    /// </summary>
    /// <param name="isNew">Indicates if the current data point is new.</param>
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    /// <summary>
    /// Performs the kurtosis calculation.
    /// </summary>
    /// <returns>
    /// The calculated excess kurtosis. Positive for heavy-tailed distributions,
    /// negative for light-tailed distributions.
    /// </returns>
    /// <remarks>
    /// Uses the Sheskin Algorithm for kurtosis calculation.
    /// Requires at least 4 data points for a valid calculation.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer.Add(Input.Value, Input.IsNew);

        double kurtosis = 0;
        if (_buffer.Count > 3)
        {
            var values = _buffer.GetSpan().ToArray();
            double mean = values.Average();
            double n = values.Length;

            double s2 = 0;
            double s4 = 0;

            for (int i = 0; i < values.Length; i++)
            {
                double diff = values[i] - mean;
                s2 += diff * diff;
                s4 += diff * diff * diff * diff;
            }

            double variance = s2 / (n - 1);

            // Sheskin Algorithm
            kurtosis = (n * (n + 1) * s4) / (variance * variance * (n - 3) * (n - 1) * (n - 2))
                       - (3 * (n - 1) * (n - 1) / ((n - 2) * (n - 3)));
        }

        IsHot = _buffer.Count >= Period;
        return kurtosis;
    }
}
