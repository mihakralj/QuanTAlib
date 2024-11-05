using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// THEIL: Theil's U Statistics (U1, U2)
/// A statistical measure that quantifies the accuracy of forecasts compared to actual values
/// and naive forecasts.
/// </summary>
/// <remarks>
/// The Theil's U calculation process:
/// 1. Calculate U1 statistic (relative accuracy)
/// 2. Calculate U2 statistic (comparison with naive forecast)
///
/// Key characteristics:
/// - U1 ranges from 0 to 1, with 0 indicating perfect forecast
/// - U2 &lt; 1: forecast better than naive forecast
/// - U2 = 1: forecast equal to naive forecast
/// - U2 &gt; 1: forecast worse than naive forecast
///
/// Formula:
/// U1 = √[Σ(Ft - At)² / Σ(At)²]
/// U2 = √[Σ(Ft - At)² / Σ(At - At-1)²]
/// where:
/// Ft = forecasted value
/// At = actual value
/// At-1 = previous actual value
///
/// Market Applications:
/// - Evaluating forecast accuracy
/// - Comparing forecasting models
/// - Assessing forecasting methods
/// - Model selection
/// - Performance analysis
///
/// Sources:
///     https://en.wikipedia.org/wiki/Theil%27s_U
///     "Forecasting: Principles and Practice" - Rob J Hyndman
///
/// Note: Should be used alongside other accuracy measures
/// </remarks>
[SkipLocalsInit]
public sealed class Theil : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _actual;
    private readonly CircularBuffer _forecast;
    private const int MinimumPoints = 2;

    /// <summary>
    /// Gets the U2 statistic comparing forecast with naive forecast
    /// </summary>
    public double U2 { get; private set; }

    /// <param name="period">The number of points to consider for Theil's U calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Theil(int period)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2 for Theil's U calculation.");
        }
        Period = period;
        WarmupPeriod = MinimumPoints;
        _actual = new CircularBuffer(period);
        _forecast = new CircularBuffer(period);
        Name = $"Theil(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for Theil's U calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Theil(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _actual.Clear();
        _forecast.Clear();
        U2 = 0;
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
    private static double CalculateSquaredSum(ReadOnlySpan<double> values)
    {
        double sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i] * values[i];
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateSquaredErrorSum(ReadOnlySpan<double> forecast, ReadOnlySpan<double> actual)
    {
        double sum = 0;
        for (int i = 0; i < forecast.Length; i++)
        {
            double error = forecast[i] - actual[i];
            sum += error * error;
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateNaiveSquaredErrorSum(ReadOnlySpan<double> actual)
    {
        double sum = 0;
        for (int i = 1; i < actual.Length; i++)
        {
            double error = actual[i] - actual[i - 1];
            sum += error * error;
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _actual.Add(Input.Value, Input.IsNew);
        _forecast.Add(Input2.Value, Input.IsNew);

        double u1 = 0;
        if (_actual.Count >= MinimumPoints && _forecast.Count >= MinimumPoints)
        {
            ReadOnlySpan<double> actualValues = _actual.GetSpan();
            ReadOnlySpan<double> forecastValues = _forecast.GetSpan();

            double squaredErrorSum = CalculateSquaredErrorSum(forecastValues, actualValues);
            double squaredActualSum = CalculateSquaredSum(actualValues);
            double naiveSquaredErrorSum = CalculateNaiveSquaredErrorSum(actualValues);

            if (squaredActualSum > double.Epsilon)
            {
                u1 = Math.Sqrt(squaredErrorSum / squaredActualSum);
            }

            if (naiveSquaredErrorSum > double.Epsilon)
            {
                U2 = Math.Sqrt(squaredErrorSum / naiveSquaredErrorSum);
            }
        }

        IsHot = _actual.Count >= Period && _forecast.Count >= Period;
        return u1;
    }
}
