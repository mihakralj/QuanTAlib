namespace QuanTAlib;

/// <summary>
/// Represents a standard deviation calculator that measures the amount of variation or
/// dispersion of a set of values.
/// </summary>
/// <remarks>
/// The Stddev class calculates either the population standard deviation or the sample
/// standard deviation based on the isPopulation parameter. It uses a circular buffer
/// to efficiently manage the data points within the specified period.
/// </remarks>
public class Stddev : AbstractBase
{
    private readonly int Period;
    private readonly bool IsPopulation;
    private readonly CircularBuffer _buffer;

    /// <summary>
    /// Initializes a new instance of the Stddev class with the specified period and
    /// population flag.
    /// </summary>
    /// <param name="period">The period over which to calculate the standard deviation.</param>
    /// <param name="isPopulation">
    /// A flag indicating whether to calculate population (true) or sample (false) standard deviation.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 2.
    /// </exception>
    public Stddev(int period, bool isPopulation = false) : base()
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        Period = period;
        IsPopulation = isPopulation;
        WarmupPeriod = 0;
        _buffer = new CircularBuffer(period);
        Name = $"Stddev(period={period}, population={isPopulation})";
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the Stddev class with the specified source, period,
    /// and population flag.
    /// </summary>
    /// <param name="source">The source object to subscribe to for value updates.</param>
    /// <param name="period">The period over which to calculate the standard deviation.</param>
    /// <param name="isPopulation">
    /// A flag indicating whether to calculate population (true) or sample (false) standard deviation.
    /// </param>
    public Stddev(object source, int period, bool isPopulation = false) : this(period, isPopulation)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Initializes the Stddev instance by clearing the buffer.
    /// </summary>
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
    }

    /// <summary>
    /// Manages the state of the Stddev instance based on whether a new value is being processed.
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
    /// Performs the standard deviation calculation for the current period.
    /// </summary>
    /// <returns>
    /// The calculated standard deviation value for the current period.
    /// </returns>
    /// <remarks>
    /// This method calculates the standard deviation using the formula:
    /// sqrt(sum((x - mean)^2) / n) for population, or
    /// sqrt(sum((x - mean)^2) / (n - 1)) for sample,
    /// where x is each value, mean is the average of all values, and n is the number of values.
    /// If there's only one value in the buffer, the method returns 0.
    /// </remarks>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer.Add(Input.Value, Input.IsNew);

        double stddev = 0;
        if (_buffer.Count > 1)
        {
            var values = _buffer.GetSpan().ToArray();
            double mean = values.Average();
            double sumOfSquaredDifferences = values.Sum(x => Math.Pow(x - mean, 2));

            double divisor = IsPopulation ? _buffer.Count : _buffer.Count - 1;
            double variance = sumOfSquaredDifferences / divisor;
            stddev = Math.Sqrt(variance);
        }

        IsHot = true; // StdDev calc is valid from bar 1
        return stddev;
    }
}
