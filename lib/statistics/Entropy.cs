namespace QuanTAlib;

/// <summary>
/// Measures the unpredictability of data using Shannon's Entropy.
/// Provides insights into the randomness or information content of the time series.
/// </summary>
public class Entropy : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;

    /// <summary>
    /// Initializes a new instance of the Entropy class.
    /// </summary>
    /// <param name="period">The number of data points to consider for calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the period is less than 2.
    /// </exception>
    public Entropy(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2 for entropy calculation.");
        }
        Period = period;
        WarmupPeriod = 2;
        _buffer = new CircularBuffer(period);
        Name = $"Entropy(period={period})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Entropy class with a data source.
    /// </summary>
    /// <param name="source">The source object that publishes data.</param>
    /// <param name="period">The number of data points to consider.</param>
    public Entropy(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Resets the Entropy indicator to its initial state.
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
    /// Performs the entropy calculation.
    /// </summary>
    /// <returns>
    /// The calculated entropy value, normalized between 0 and 1.
    /// 1 indicates maximum randomness, 0 indicates perfect predictability.
    /// </returns>
    /// <remarks>
    /// Uses Shannon's Entropy formula and normalizes the result based on the
    /// number of unique values in the current period.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer.Add(Input.Value, Input.IsNew);

        double entropy = 0;
        if (_index > 1)  // We need at least two data points for entropy calculation
        {
            var values = _buffer.GetSpan().ToArray();
            int n = values.Length;

            // Calculate probabilities
            var groupedValues = values.GroupBy(x => x).Select(g => new { Value = g.Key, Count = g.Count() });

            // Use the actual count of values for probability calculation
            foreach (var group in groupedValues)
            {
                double probability = (double)group.Count / n;
                entropy -= probability * Math.Log2(probability);
            }

            // Normalize the entropy based on the current number of unique values
            int uniqueValueCount = groupedValues.Count();
            double maxEntropy = Math.Log2(uniqueValueCount);

            entropy = entropy == 0 ? 1 : entropy / maxEntropy;
        }
        else
        {
            entropy = 1; // Default to maximum entropy when insufficient data
        }

        IsHot = _buffer.Count >= Period;
        return entropy;
    }
}