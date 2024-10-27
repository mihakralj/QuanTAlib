using System;
namespace QuanTAlib;

/// <summary>
/// FWMA: Fibonacci Weighted Moving Average
/// A moving average that uses Fibonacci numbers as weights in its calculation. The weights
/// are arranged in reverse order so that recent prices receive higher weights corresponding
/// to larger Fibonacci numbers.
/// </summary>
/// <remarks>
/// The FWMA calculation process:
/// 1. Generates a Fibonacci sequence up to the specified period
/// 2. Reverses the sequence to give higher weights to recent prices
/// 3. Normalizes the weights to sum to 1
/// 4. Applies the weights through convolution
///
/// Key characteristics:
/// - Uses Fibonacci sequence for weight distribution
/// - Recent prices receive higher weights
/// - Natural progression of weights based on the golden ratio
/// - Implemented using efficient convolution operations
///
/// Implementation:
///     Original implementation based on Fibonacci sequence principles
/// </remarks>

public class Fwma : AbstractBase
{
    private readonly Convolution _convolution;

    /// <param name="period">The number of data points used in the FWMA calculation.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Fwma(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _convolution = new Convolution(GenerateKernel(period));
        Name = "Fwma";
        WarmupPeriod = period;
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of data points used in the FWMA calculation.</param>
    public Fwma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Generates the Fibonacci-based convolution kernel for the FWMA calculation.
    /// </summary>
    /// <param name="period">The period for which to generate the kernel.</param>
    /// <returns>An array of normalized Fibonacci-based weights for the convolution operation.</returns>
    public static double[] GenerateKernel(int period)
    {
        double[] kernel = new double[period];
        double[] fibSeries = new double[period];
        double weightSum = 0;

        // Generate Fibonacci series
        fibSeries[0] = fibSeries[1] = 1;
        for (int i = 2; i < period; i++)
        {
            fibSeries[i] = fibSeries[i - 1] + fibSeries[i - 2];
        }

        // Reverse the series to give more weight to recent prices
        for (int i = 0; i < period; i++)
        {
            kernel[i] = fibSeries[period - 1 - i];
            weightSum += kernel[i];
        }

        // Normalize the kernel
        for (int i = 0; i < period; i++)
        {
            kernel[i] /= weightSum;
        }

        return kernel;
    }

    private new void Init()
    {
        base.Init();
        _convolution.Init();
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Use Convolution for calculation
        TValue convolutionResult = _convolution.Calc(Input);

        double result = convolutionResult.Value;
        IsHot = _index >= WarmupPeriod;

        return result;
    }
}
