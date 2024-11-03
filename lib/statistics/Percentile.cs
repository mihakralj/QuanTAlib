using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// Percentile: Distribution Position Measure
/// A statistical measure that indicates the value below which a given percentage
/// of observations falls. Percentiles provide insights into data distribution
/// and are particularly useful for risk assessment and outlier detection.
/// </summary>
/// <remarks>
/// The Percentile calculation process:
/// 1. Sorts values in ascending order
/// 2. Calculates position based on percentile
/// 3. Interpolates between adjacent values
/// 4. Uses mean until period filled
///
/// Key characteristics:
/// - Range specific value identification
/// - Linear interpolation for precision
/// - Distribution independent
/// - Robust to outliers
/// - Useful for risk metrics
///
/// Formula:
/// position = (percentile/100) * (n-1)
/// value = v[floor(pos)] + (v[ceil(pos)] - v[floor(pos)]) * (pos - floor(pos))
/// where n = number of observations, v = sorted values
///
/// Market Applications:
/// - Value at Risk (VaR) calculation
/// - Risk management metrics
/// - Performance analysis
/// - Volatility assessment
/// - Outlier detection
///
/// Sources:
///     https://en.wikipedia.org/wiki/Percentile
///     "Risk Management in Trading" - Davis Edwards
///
/// Note: Particularly useful for risk metrics like VaR
/// </remarks>
[SkipLocalsInit]
public sealed class Percentile : AbstractBase
{
    private readonly int Period;
    private readonly double Percent;
    private readonly CircularBuffer _buffer;
    private const double Epsilon = 1e-10;
    private const int MinimumPoints = 2;

    /// <param name="period">The number of points to consider for percentile calculation.</param>
    /// <param name="percent">The percentile to calculate (0-100).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when period is less than 2 or percent is not between 0 and 100.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Percentile(int period, double percent)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2 for percentile calculation.");
        }
        if (percent < 0 || percent > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percent),
                "Percent must be between 0 and 100.");
        }
        Period = period;
        Percent = percent;
        WarmupPeriod = MinimumPoints; // Minimum number of points needed for percentile calculation
        _buffer = new CircularBuffer(period);
        Name = $"Percentile(period={period}, percent={percent})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for percentile calculation.</param>
    /// <param name="percent">The percentile to calculate (0-100).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Percentile(object source, int period, double percent) : this(period, percent)
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
    private static void QuickSort(Span<double> arr, int left, int right)
    {
        if (left < right)
        {
            int pivotIndex = Partition(arr, left, right);
            QuickSort(arr, left, pivotIndex - 1);
            QuickSort(arr, pivotIndex + 1, right);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Partition(Span<double> arr, int left, int right)
    {
        double pivot = arr[right];
        int i = left - 1;

        for (int j = left; j < right; j++)
        {
            if (arr[j] <= pivot)
            {
                i++;
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
        }

        (arr[i + 1], arr[right]) = (arr[right], arr[i + 1]);
        return i + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double CalculatePercentile(Span<double> sortedValues)
    {
        double position = (Percent / 100.0) * (sortedValues.Length - 1);
        int lowerIndex = (int)Math.Floor(position);
        int upperIndex = (int)Math.Ceiling(position);

        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        // Linear interpolation between adjacent values
        double lowerValue = sortedValues[lowerIndex];
        double upperValue = sortedValues[upperIndex];
        double fraction = position - lowerIndex;
        return lowerValue + ((upperValue - lowerValue) * fraction);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        double result;
        if (_buffer.Count >= Period)
        {
            // Create a temporary buffer on the stack and sort values
            Span<double> values = stackalloc double[Period];
            _buffer.GetSpan().CopyTo(values);
            QuickSort(values, 0, values.Length - 1);

            result = CalculatePercentile(values);
        }
        else
        {
            // Use average until we have enough data points
            result = _buffer.Average();
        }

        IsHot = _buffer.Count >= Period;
        return result;
    }
}
