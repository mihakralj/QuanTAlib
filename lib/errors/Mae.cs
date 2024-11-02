using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// MAE: Mean Absolute Error
/// A straightforward error metric that measures the average magnitude of errors
/// between predicted and actual values, without considering their direction.
/// MAE treats all individual differences equally in the average.
/// </summary>
/// <remarks>
/// The MAE calculation process:
/// 1. Calculates absolute difference between each actual and predicted value
/// 2. Sums all absolute differences
/// 3. Divides by the number of observations
///
/// Key characteristics:
/// - Linear scale (all differences weighted equally)
/// - Robust to outliers compared to MSE
/// - Easy to interpret (same units as data)
/// - Constant gradient for optimization
/// - Less sensitive to large errors than MSE
///
/// Formula:
/// MAE = (1/n) * Σ|actual - predicted|
///
/// Sources:
///     https://en.wikipedia.org/wiki/Mean_absolute_error
///     https://www.statisticshowto.com/absolute-error/
/// </remarks>

[SkipLocalsInit]
public sealed class Mae : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the MAE.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mae(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Mae(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the MAE.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mae(object source, int period) : this(period)
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
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double actual = Input.Value;
        _actualBuffer.Add(actual, Input.IsNew);

        // If no predicted value provided, use mean of actual values
        double predicted = double.IsNaN(Input2.Value) ? _actualBuffer.Average() : Input2.Value;
        _predictedBuffer.Add(predicted, Input.IsNew);

        double mae = 0;
        if (_actualBuffer.Count > 0)
        {
            ReadOnlySpan<double> actualValues = _actualBuffer.GetSpan();
            ReadOnlySpan<double> predictedValues = _predictedBuffer.GetSpan();

            double sumAbsoluteError = 0;
            for (int i = 0; i < actualValues.Length; i++)
            {
                sumAbsoluteError += Math.Abs(actualValues[i] - predictedValues[i]);
            }

            mae = sumAbsoluteError / actualValues.Length;
        }

        IsHot = _index >= WarmupPeriod;
        return mae;
    }
}
