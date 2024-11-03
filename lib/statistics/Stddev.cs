using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// STDDEV: Standard Deviation Volatility Measure
/// A statistical measure that quantifies the amount of variation or dispersion
/// in a dataset. Standard deviation is widely used in finance as a measure of
/// volatility and risk assessment.
/// </summary>
/// <remarks>
/// The StdDev calculation process:
/// 1. Calculates mean of the data
/// 2. Computes squared deviations from mean
/// 3. Averages squared deviations
/// 4. Takes square root of average
///
/// Key characteristics:
/// - Measures data dispersion
/// - Same units as input data
/// - Sensitive to outliers
/// - Population or sample versions
/// - Key volatility indicator
///
/// Formula:
/// Population: σ = √(Σ(x - μ)² / N)
/// Sample: s = √(Σ(x - x̄)² / (n-1))
/// where:
/// x = values
/// μ, x̄ = mean
/// N, n = count
///
/// Market Applications:
/// - Volatility measurement
/// - Risk assessment
/// - Bollinger Bands
/// - Option pricing
/// - Portfolio management
///
/// Sources:
///     https://en.wikipedia.org/wiki/Standard_deviation
///     "Options, Futures, and Other Derivatives" - John C. Hull
///
/// Note: Foundation for many volatility-based indicators
/// </remarks>
[SkipLocalsInit]
public sealed class Stddev : AbstractBase
{
    private readonly bool IsPopulation;
    private readonly CircularBuffer _buffer;
    private const double Epsilon = 1e-10;
    private const int MinimumPoints = 2;

    /// <param name="period">The number of points to consider for standard deviation calculation.</param>
    /// <param name="isPopulation">True for population stddev, false for sample stddev (default).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stddev(int period, bool isPopulation = false)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2.");
        }
        IsPopulation = isPopulation;
        WarmupPeriod = 0;
        _buffer = new CircularBuffer(period);
        Name = $"Stddev(period={period}, population={isPopulation})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for standard deviation calculation.</param>
    /// <param name="isPopulation">True for population stddev, false for sample stddev (default).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stddev(object source, int period, bool isPopulation = false) : this(period, isPopulation)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateMean(ReadOnlySpan<double> values)
    {
        double sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }
        return sum / values.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateSumSquaredDeviations(ReadOnlySpan<double> values, double mean)
    {
        double sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            double diff = values[i] - mean;
            sum += diff * diff;
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        double stddev = 0;
        if (_buffer.Count > 1)
        {
            ReadOnlySpan<double> values = _buffer.GetSpan();
            double mean = CalculateMean(values);
            double sumOfSquaredDifferences = CalculateSumSquaredDeviations(values, mean);

            // Use appropriate divisor based on population/sample calculation
            double divisor = IsPopulation ? _buffer.Count : _buffer.Count - 1;
            double variance = sumOfSquaredDifferences / divisor;
            stddev = Math.Sqrt(variance);
        }

        IsHot = true; // StdDev calc is valid from bar 1
        return stddev;
    }
}
