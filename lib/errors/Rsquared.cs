using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// R-squared: Coefficient of Determination
/// A statistical measure that represents the proportion of variance in the dependent
/// variable that is predictable from the independent variable. R-squared provides
/// a measure of how well the predictions approximate the actual data.
/// </summary>
/// <remarks>
/// The R-squared calculation process:
/// 1. Calculates total sum of squares (variance from mean)
/// 2. Calculates residual sum of squares (prediction errors)
/// 3. Computes 1 - (residual SS / total SS)
///
/// Key characteristics:
/// - Range is typically 0 to 1
/// - 1 indicates perfect prediction
/// - 0 indicates prediction no better than mean
/// - Scale-independent
/// - Widely used in regression analysis
///
/// Formula:
/// R² = 1 - (Σ(actual - predicted)² / Σ(actual - mean(actual))²)
///
/// Sources:
///     https://en.wikipedia.org/wiki/Coefficient_of_determination
///     https://www.statisticshowto.com/probability-and-statistics/coefficient-of-determination-r-squared/
///
/// Note: Can be negative if predictions are worse than using the mean
/// </remarks>

[SkipLocalsInit]
public sealed class Rsquared : AbstractBase
{
    private readonly CircularBuffer _actualBuffer;
    private readonly CircularBuffer _predictedBuffer;

    /// <param name="period">The number of points over which to calculate the R-squared value.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rsquared(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        WarmupPeriod = period;
        _actualBuffer = new CircularBuffer(period);
        _predictedBuffer = new CircularBuffer(period);
        Name = $"Rsquared(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points over which to calculate the R-squared value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rsquared(object source, int period) : this(period)
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
    private static (double squaredResidual, double squaredTotal) CalculateSquaredErrors(double actual, double predicted, double meanActual)
    {
        double deviation = actual - meanActual;
        double error = actual - predicted;
        return (error * error, deviation * deviation);
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

        double rsquared = 0;
        if (_actualBuffer.Count > 0)
        {
            ReadOnlySpan<double> actualValues = _actualBuffer.GetSpan();
            ReadOnlySpan<double> predictedValues = _predictedBuffer.GetSpan();

            double meanActual = _actualBuffer.Average();
            double sumSquaredTotal = 0;
            double sumSquaredResidual = 0;

            for (int i = 0; i < actualValues.Length; i++)
            {
                var (squaredResidual, squaredTotal) = CalculateSquaredErrors(actualValues[i], predictedValues[i], meanActual);
                sumSquaredResidual += squaredResidual;
                sumSquaredTotal += squaredTotal;
            }

            rsquared = sumSquaredTotal != 0 ? 1 - (sumSquaredResidual / sumSquaredTotal) : 0;
        }

        IsHot = _index >= WarmupPeriod;
        return rsquared;
    }
}
