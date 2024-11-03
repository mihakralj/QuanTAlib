using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// RMSE: Root Mean Square Error
/// A widely used error metric that measures the square root of the average squared
/// differences between predicted and actual values. RMSE provides error measurements
/// in the same units as the original data.
/// </summary>
/// <remarks>
/// The RMSE calculation process:
/// 1. Calculates error (actual - predicted) for each point
/// 2. Squares each error value
/// 3. Averages the squared errors
/// 4. Takes the square root of the average
///
/// Key characteristics:
/// - Same units as input data (unlike MSE)
/// - Penalizes large errors more than small ones
/// - Always non-negative
/// - More interpretable than MSE
/// - Commonly used in regression problems
///
/// Formula:
/// RMSE = √((1/n) * Σ(actual - predicted)²)
///
/// Sources:
///     https://en.wikipedia.org/wiki/Root-mean-square_deviation
///     https://www.statisticshowto.com/probability-and-statistics/regression-analysis/rmse-root-mean-square-error/
///
/// Note: Square root of MSE, making it more interpretable in original units
/// </remarks>
[SkipLocalsInit]
public sealed class Rmse : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the RMSE.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rmse(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Rmse(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the RMSE.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rmse(object source, int period) : this(period)
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
    private static double CalculateSquaredError(double actual, double predicted)
    {
        double error = actual - predicted;
        return error * error;
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

        double rmse = 0;
        if (_actualBuffer.Count > 0)
        {
            ReadOnlySpan<double> actualValues = _actualBuffer.GetSpan();
            ReadOnlySpan<double> predictedValues = _predictedBuffer.GetSpan();

            double sumSquaredError = 0;
            for (int i = 0; i < actualValues.Length; i++)
            {
                sumSquaredError += CalculateSquaredError(actualValues[i], predictedValues[i]);
            }

            rmse = Math.Sqrt(sumSquaredError / actualValues.Length);
        }

        IsHot = _index >= WarmupPeriod;
        return rmse;
    }
}
