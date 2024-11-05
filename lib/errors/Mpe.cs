using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// MPE: Mean Percentage Error
/// A percentage-based error metric that measures the average percentage difference
/// between actual and predicted values. Like ME, it allows positive and negative
/// errors to cancel out, but expresses the bias in percentage terms.
/// </summary>
/// <remarks>
/// The MPE calculation process:
/// 1. Calculates percentage error for each point
/// 2. Sums all percentage errors (allowing cancellation)
/// 3. Divides by the number of observations
///
/// Key characteristics:
/// - Scale-independent (percentage-based)
/// - Can detect systematic bias
/// - Positive MPE indicates underprediction
/// - Negative MPE indicates overprediction
/// - Cannot handle zero actual values
/// - Errors can cancel out
///
/// Formula:
/// MPE = (1/n) * Σ((actual - predicted) / actual) * 100%
///
/// Sources:
///     https://en.wikipedia.org/wiki/Mean_percentage_error
///     https://www.statisticshowto.com/mean-percentage-error/
///
/// Note: Similar to MAPE but allows error cancellation
/// </remarks>
[SkipLocalsInit]
public sealed class Mpe : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the MPE.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mpe(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Mpe(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the MPE.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mpe(object source, int period) : this(period)
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
        return actual >= double.Epsilon ? (actual - predicted) / actual : 0;
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

        double mpe = 0;
        if (_actualBuffer.Count > 0)
        {
            ReadOnlySpan<double> actualValues = _actualBuffer.GetSpan();
            ReadOnlySpan<double> predictedValues = _predictedBuffer.GetSpan();

            double sumPercentageError = 0;
            for (int i = 0; i < actualValues.Length; i++)
            {
                sumPercentageError += CalculatePercentageError(actualValues[i], predictedValues[i]);
            }

            mpe = sumPercentageError / actualValues.Length;
        }

        IsHot = _index >= WarmupPeriod;
        return mpe;
    }
}
