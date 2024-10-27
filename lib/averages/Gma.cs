using System;
namespace QuanTAlib;

/// <summary>
/// GMA: Gaussian Moving Average
/// A moving average that uses weights based on the Gaussian (normal) distribution curve.
/// This creates a smooth, bell-shaped weighting scheme that gives maximum weight to the
/// center of the period and gradually decreasing weights towards the edges.
/// </summary>
/// <remarks>
/// The GMA calculation process:
/// 1. Creates a Gaussian distribution of weights centered on the period
/// 2. Normalizes the weights to sum to 1
/// 3. Applies the weights through convolution
///
/// Key characteristics:
/// - Smooth, symmetric weight distribution
/// - Natural bell curve weighting
/// - Reduces noise while preserving signal characteristics
/// - Less sensitive to outliers than simple moving averages
/// - Implemented using efficient convolution operations
///
/// Implementation:
///     Based on Gaussian distribution principles from statistics
/// </remarks>

public class Gma : AbstractBase
{
    private readonly Convolution _convolution;

    /// <param name="period">The number of data points used in the GMA calculation.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Gma(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _convolution = new Convolution(GenerateKernel(period));
        Name = "Gma";
        WarmupPeriod = period;
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of data points used in the GMA calculation.</param>
    public Gma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Generates the Gaussian-based convolution kernel for the GMA calculation.
    /// </summary>
    /// <param name="period">The period for which to generate the kernel.</param>
    /// <param name="sigma">The standard deviation parameter controlling the spread of the Gaussian curve. Default is 1.0.</param>
    /// <returns>An array of normalized Gaussian-based weights for the convolution operation.</returns>
    public static double[] GenerateKernel(int period, double sigma = 1.0)
    {
        double[] kernel = new double[period];
        double weightSum = 0;
        int center = period / 2;

        for (int i = 0; i < period; i++)
        {
            double x = (i - center) / (double)center;
            kernel[i] = Math.Exp(-(x * x) / (2 * sigma * sigma));
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
