using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// RAE: Relative Absolute Error
/// A normalized error metric that compares the total absolute error to the total
/// magnitude of actual values. RAE provides a scale-independent measure of error
/// that is robust to the overall magnitude of the data.
/// </summary>
/// <remarks>
/// The RAE calculation process:
/// 1. Calculates sum of absolute errors
/// 2. Calculates sum of absolute actual values
/// 3. Divides total error by total actual magnitude
///
/// Key characteristics:
/// - Scale-independent (normalized by actual values)
/// - Range typically between 0 and 1
/// - Easy to interpret (0 is perfect, 1 means error equals data magnitude)
/// - Robust to data scale changes
/// - Less sensitive to outliers than squared errors
///
/// Formula:
/// RAE = Σ|actual - predicted| / Σ|actual|
///
/// Sources:
///     https://en.wikipedia.org/wiki/Relative_absolute_error
///     https://www.sciencedirect.com/topics/engineering/relative-absolute-error
///
/// Note: Values greater than 1 indicate predictions worse than using zero
/// </remarks>

[SkipLocalsInit]
public sealed class Rae : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the RAE.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rae(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Rae(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the RAE.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rae(object source, int period) : this(period)
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
    private static (double error, double magnitude) CalculateErrorAndMagnitude(double actual, double predicted)
    {
        return (Math.Abs(actual - predicted), Math.Abs(actual));
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

        double rae = 0;
        if (_actualBuffer.Count > 0)
        {
            ReadOnlySpan<double> actualValues = _actualBuffer.GetSpan();
            ReadOnlySpan<double> predictedValues = _predictedBuffer.GetSpan();

            double sumAbsoluteError = 0;
            double sumAbsoluteActual = 0;
            for (int i = 0; i < actualValues.Length; i++)
            {
                var (error, magnitude) = CalculateErrorAndMagnitude(actualValues[i], predictedValues[i]);
                sumAbsoluteError += error;
                sumAbsoluteActual += magnitude;
            }

            rae = sumAbsoluteActual > 0 ? sumAbsoluteError / sumAbsoluteActual : 0;
        }

        IsHot = _index >= WarmupPeriod;
        return rae;
    }
}
