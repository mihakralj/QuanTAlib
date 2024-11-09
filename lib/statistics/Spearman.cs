using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// SPEARMAN: Spearman's Rank Correlation Coefficient
/// A nonparametric measure of rank correlation that assesses the monotonic relationship
/// between two variables. Unlike Pearson correlation, Spearman correlation evaluates
/// the relationship based on ranked values rather than raw data.
/// </summary>
/// <remarks>
/// The Spearman calculation process:
/// 1. Ranks both sets of values
/// 2. Calculates correlation between ranks
/// 3. Handles ties by averaging ranks
///
/// Key characteristics:
/// - Resistant to outliers
/// - Detects monotonic relationships
/// - Range: -1 to +1
/// - Distribution-free measure
/// - Handles non-linear relationships
///
/// Formula:
/// ρ = Cov(rank(X), rank(Y)) / (σrank(X) * σrank(Y))
/// where:
/// X, Y = variables
/// rank() = ranking function
/// Cov = covariance
/// σ = standard deviation
///
/// Market Applications:
/// - Technical analysis
/// - Risk assessment
/// - Market correlation studies
/// - Trend analysis
/// - Pattern recognition
///
/// Sources:
///     https://en.wikipedia.org/wiki/Spearman%27s_rank_correlation_coefficient
///     "Nonparametric Statistics for Non-Statisticians" - Gregory W. Corder
///
/// Note: More robust to outliers than Pearson correlation
/// </remarks>
[SkipLocalsInit]
public sealed class Spearman : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _xValues;
    private readonly CircularBuffer _yValues;
    private readonly CircularBuffer _xRanks;
    private readonly CircularBuffer _yRanks;
    private const double Epsilon = 1e-10;
    private const int MinimumPoints = 2;

    /// <param name="period">The number of points to consider for Spearman correlation calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Spearman(int period)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2 for Spearman correlation calculation.");
        }
        Period = period;
        WarmupPeriod = MinimumPoints;
        _xValues = new CircularBuffer(period);
        _yValues = new CircularBuffer(period);
        _xRanks = new CircularBuffer(period);
        _yRanks = new CircularBuffer(period);
        Name = $"Spearman(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for Spearman correlation calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Spearman(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _xValues.Clear();
        _yValues.Clear();
        _xRanks.Clear();
        _yRanks.Clear();
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
    private static double[] CalculateRanks(ReadOnlySpan<double> values)
    {
        int n = values.Length;
        var pairs = new (double value, int index)[n];
        for (int i = 0; i < n; i++)
        {
            pairs[i] = double.IsNaN(values[i]) ? (double.NaN, i) : (values[i], i);
        }

        // Sort non-NaN values
        var validPairs = pairs.Where(p => !double.IsNaN(p.value)).OrderBy(p => p.value).ToArray();
        var ranks = new double[n];
        Array.Fill(ranks, double.NaN);

        for (int i = 0; i < validPairs.Length;)
        {
            int j = i;
            // Find ties
            while (j < validPairs.Length - 1 && Math.Abs(validPairs[j].value - validPairs[j + 1].value) < Epsilon)
            {
                j++;
            }

            // Average rank for ties
            double rank = (i + j) / 2.0 + 1;
            for (int k = i; k <= j; k++)
            {
                ranks[validPairs[k].index] = rank;
            }
            i = j + 1;
        }

        return ranks;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateCovariance(CircularBuffer xBuffer, CircularBuffer yBuffer, double xMean, double yMean)
    {
        var xSpan = xBuffer.GetSpan();
        var ySpan = yBuffer.GetSpan();
        double covariance = 0;
        int count = 0;

        for (int i = 0; i < xSpan.Length; i++)
        {
            if (!double.IsNaN(xSpan[i]) && !double.IsNaN(ySpan[i]))
            {
                covariance += (xSpan[i] - xMean) * (ySpan[i] - yMean);
                count++;
            }
        }

        return count > 0 ? covariance / count : double.NaN;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateStandardDeviation(CircularBuffer buffer, double mean)
    {
        var span = buffer.GetSpan();
        double sumSquaredDeviations = 0;
        int count = 0;

        for (int i = 0; i < span.Length; i++)
        {
            if (!double.IsNaN(span[i]))
            {
                double deviation = span[i] - mean;
                sumSquaredDeviations += deviation * deviation;
                count++;
            }
        }

        return count > 0 ? Math.Sqrt(sumSquaredDeviations / count) : double.NaN;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _xValues.Add(Input.Value, Input.IsNew);
        _yValues.Add(Input2.Value, Input.IsNew);

        double correlation = 0;
        if (_xValues.Count >= MinimumPoints && _yValues.Count >= MinimumPoints)
        {
            // Convert values to ranks
            var xRanks = CalculateRanks(_xValues.GetSpan());
            var yRanks = CalculateRanks(_yValues.GetSpan());

            // Store ranks in buffers for statistical calculations
            _xRanks.Clear();
            _yRanks.Clear();
            for (int i = 0; i < xRanks.Length; i++)
            {
                _xRanks.Add(xRanks[i], true);
                _yRanks.Add(yRanks[i], true);
            }

            // Use CircularBuffer's optimized Average() method
            double xMean = _xRanks.Average();
            double yMean = _yRanks.Average();

            if (!double.IsNaN(xMean) && !double.IsNaN(yMean))
            {
                double covariance = CalculateCovariance(_xRanks, _yRanks, xMean, yMean);
                double xStdDev = CalculateStandardDeviation(_xRanks, xMean);
                double yStdDev = CalculateStandardDeviation(_yRanks, yMean);

                if (!double.IsNaN(covariance) && xStdDev > Epsilon && yStdDev > Epsilon)
                {
                    correlation = covariance / (xStdDev * yStdDev);
                }
            }
        }

        IsHot = _xValues.Count >= Period && _yValues.Count >= Period;
        return correlation;
    }
}
