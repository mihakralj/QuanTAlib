using System;
namespace QuanTAlib;

/// <summary>
/// SINEMA: Sine-weighted Exponential Moving Average
/// A moving average that uses sine function-based weights to create a natural
/// distribution of importance across the period. The weights follow a sine curve,
/// providing smooth transitions and natural emphasis on different parts of the data.
/// </summary>
/// <remarks>
/// The SINEMA calculation process:
/// 1. Generates weights using sine function over the period
/// 2. Normalizes weights to sum to 1
/// 3. Applies weights through convolution
/// 4. Produces smooth output with natural weight distribution
///
/// Key characteristics:
/// - Sine-based weight distribution
/// - Natural smoothing through trigonometric weights
/// - No sharp transitions in weight values
/// - Balanced emphasis across the period
/// - Implemented using efficient convolution operations
///
/// Implementation:
///     Based on sine function principles for weight generation
///     Uses convolution for efficient calculation
/// </remarks>

public class Sinema : AbstractBase
{
    private readonly Convolution _convolution;

    /// <param name="period">The number of data points used in the SINEMA calculation.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Sinema(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _convolution = new Convolution(GenerateKernel(period));
        Name = "Sinema";
        WarmupPeriod = period;
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of data points used in the SINEMA calculation.</param>
    public Sinema(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
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

    /// <summary>
    /// Generates the sine-based convolution kernel for the SINEMA calculation.
    /// </summary>
    /// <param name="period">The period for which to generate the kernel.</param>
    /// <returns>An array of normalized sine-based weights for the convolution operation.</returns>
    public static double[] GenerateKernel(int period)
    {
        double[] kernel = new double[period];
        double weightSum = 0;

        for (int i = 0; i < period; i++)
        {
            // Use sine function to generate weights
            kernel[i] = Math.Sin((i + 1) * Math.PI / (period + 1));
            weightSum += kernel[i];
        }

        // Normalize the kernel
        for (int i = 0; i < period; i++)
        {
            kernel[i] /= weightSum;
        }

        return kernel;
    }
}
