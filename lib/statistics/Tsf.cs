using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// TSF: Time Series Forecast
/// A statistical indicator that provides a linear regression forecast of future values
/// based on historical data. It includes both the forecast value and a confidence interval.
/// </summary>
/// <remarks>
/// The Time Series Forecast calculation process:
/// 1. Calculates linear regression on the input data
/// 2. Extrapolates the regression line to forecast future values
/// 3. Computes confidence intervals based on the standard error of the forecast
///
/// Key characteristics:
/// - Provides point forecast and confidence interval
/// - Based on linear regression principles
/// - Assumes trend continuity
/// - Sensitive to recent data changes
/// - Useful for short-term predictions
///
/// Formula:
/// Forecast = a + b * (n + 1)
/// where:
/// a = y-intercept
/// b = slope
/// n = number of periods
///
/// Confidence Interval = Forecast ± (t * SE)
/// where:
/// t = t-value for desired confidence level
/// SE = Standard Error of the forecast
///
/// Market Applications:
/// - Price target estimation
/// - Trend analysis
/// - Risk assessment
/// - Trading strategy development
/// - Market behavior prediction
///
/// Sources:
///     https://en.wikipedia.org/wiki/Time_series
///     "Forecasting: Principles and Practice" - Rob J Hyndman and George Athanasopoulos
///
/// Note: Assumes linear trend in the data and may not capture non-linear patterns
/// </remarks>
[SkipLocalsInit]
public sealed class Tsf : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _values;
    private const int MinimumPoints = 2;

    /// <summary>
    /// The forecasted value for the next period.
    /// </summary>
    public double Forecast { get; private set; }

    /// <summary>
    /// The lower bound of the confidence interval.
    /// </summary>
    public double LowerBound { get; private set; }

    /// <summary>
    /// The upper bound of the confidence interval.
    /// </summary>
    public double UpperBound { get; private set; }

    /// <param name="period">The number of historical data points to consider for forecasting.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tsf(int period)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2 for time series forecasting.");
        }
        Period = period;
        WarmupPeriod = MinimumPoints;
        _values = new CircularBuffer(period);
        Name = $"TSF(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of historical data points to consider for forecasting.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tsf(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _values.Clear();
        Forecast = 0;
        LowerBound = 0;
        UpperBound = 0;
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
    private static (double slope, double intercept) CalculateLinearRegression(ReadOnlySpan<double> values)
    {
        int n = values.Length;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

        for (int i = 0; i < n; i++)
        {
            double x = i + 1;
            double y = values[i];
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
        }

        double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        double intercept = (sumY - slope * sumX) / n;

        return (slope, intercept);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateStandardError(ReadOnlySpan<double> values, double slope, double intercept)
    {
        int n = values.Length;
        double sumSquaredResiduals = 0;

        for (int i = 0; i < n; i++)
        {
            double x = i + 1;
            double y = values[i];
            double predicted = slope * x + intercept;
            double residual = y - predicted;
            sumSquaredResiduals += residual * residual;
        }

        return Math.Sqrt(sumSquaredResiduals / (n - 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _values.Add(Input.Value, Input.IsNew);

        if (_values.Count >= MinimumPoints)
        {
            ReadOnlySpan<double> values = _values.GetSpan();

            var (slope, intercept) = CalculateLinearRegression(values);

            // Calculate forecast for the next period
            Forecast = slope * (Period + 1) + intercept;

            // Calculate standard error
            double standardError = CalculateStandardError(values, slope, intercept);

            // Calculate confidence interval (using t-distribution with n-2 degrees of freedom)
            double tValue = 1.96; // Approximation for 95% confidence interval
            double marginOfError = tValue * standardError * Math.Sqrt(1 + 1.0 / Period);

            LowerBound = Forecast - marginOfError;
            UpperBound = Forecast + marginOfError;
        }

        IsHot = _values.Count >= Period;
        return Forecast;
    }
}
