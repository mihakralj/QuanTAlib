using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// MAPE: Mean Absolute Percentage Error
/// A percentage-based error metric that measures the average absolute percentage
/// difference between predicted and actual values. MAPE expresses accuracy as a
/// percentage, making it scale-independent and easy to interpret.
/// </summary>
/// <remarks>
/// The MAPE calculation process:
/// 1. Calculates absolute percentage error for each point
/// 2. Sums all absolute percentage errors
/// 3. Divides by the number of observations
///
/// Key characteristics:
/// - Scale-independent (percentage-based)
/// - Easy to interpret (0-100% range)
/// - Useful for comparing different scales
/// - Cannot handle zero actual values
/// - Asymmetric (treats over/under predictions differently)
///
/// Formula:
/// MAPE = (1/n) * Σ|((actual - predicted) / actual)| * 100%
///
/// Sources:
///     https://en.wikipedia.org/wiki/Mean_absolute_percentage_error
///     https://www.statisticshowto.com/mean-absolute-percentage-error-mape/
///
/// Note: Also known as MAPD (Mean Absolute Percentage Deviation) in some contexts
/// </remarks>
[SkipLocalsInit]
public sealed class Mape : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the MAPE.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mape(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Mape(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the MAPE.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mape(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _actualBuffer.Clear();
        _predictedBuffer.Clear();
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
    private static double CalculatePercentageError(double actual, double predicted)
    {
        return actual >= double.Epsilon ? Math.Abs((actual - predicted) / actual) : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        // If no predicted value provided, use mean of actual values
        double predicted = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _predictedBuffer.Add(predicted, Input.IsNew);

        double mape = 0;
        if (_actualBuffer.Count > 0)
        {
            ReadOnlySpan<double> actualValues = _actualBuffer.GetSpan();
            ReadOnlySpan<double> predictedValues = _predictedBuffer.GetSpan();

            double sumAbsolutePercentageError = 0;
            for (int i = 0; i < actualValues.Length; i++)
            {
                sumAbsolutePercentageError += CalculatePercentageError(actualValues[i], predictedValues[i]);
            }

            mape = sumAbsolutePercentageError / actualValues.Length;
        }

        IsHot = _index >= WarmupPeriod;
        return mape;
    }
}
