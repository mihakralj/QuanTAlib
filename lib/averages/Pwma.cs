using System;
using System.Linq;
namespace QuanTAlib;

/// <summary>
/// PWMA: Pascal Weighted Moving Average
/// A moving average that uses Pascal's triangle coefficients as weights, providing
/// a natural distribution of weights that increases towards the center of the period.
/// This creates a smooth average with balanced emphasis on central values.
/// </summary>
/// <remarks>
/// The PWMA calculation process:
/// 1. Generates weights using Pascal's triangle coefficients
/// 2. Normalizes the weights to sum to 1
/// 3. Applies the weights through convolution
/// 4. Adjusts for partial periods during warmup
///
/// Key characteristics:
/// - Natural weight distribution from Pascal's triangle
/// - Symmetric weighting around the center
/// - Smooth response to price changes
/// - Balanced between recent and historical data
/// - Implemented using efficient convolution operations
///
/// Implementation:
///     Based on Pascal's triangle principles for weight generation
///     Uses convolution for efficient calculation
/// </remarks>

public class Pwma : AbstractBase
{
    private readonly int _period;
    private readonly Convolution _convolution;

    /// <param name="period">The number of data points used in the PWMA calculation.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Pwma(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _convolution = new Convolution(GenerateKernel(_period));
        Name = "Pwma";
        WarmupPeriod = period;
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of data points used in the PWMA calculation.</param>
    public Pwma(object source, int period) : this(period)
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

        // Adjust for partial periods during warmup
        if (_index < _period)
        {
            double[] partialKernel = GenerateKernel(_index);
            result /= partialKernel.Sum();
        }

        IsHot = _index >= WarmupPeriod;

        return result;
    }

    /// <summary>
    /// Generates the Pascal's triangle-based convolution kernel for the PWMA calculation.
    /// </summary>
    /// <param name="period">The period for which to generate the kernel.</param>
    /// <returns>An array of normalized Pascal's triangle-based weights for the convolution operation.</returns>
    public static double[] GenerateKernel(int period)
    {
        double[] kernel = new double[period];
        kernel[0] = 1;

        for (int i = 1; i < period; i++)
        {
            for (int j = i; j > 0; j--)
            {
                kernel[j] += kernel[j - 1];
            }
        }

        // Normalize the kernel
        double weightSum = kernel.Sum();
        for (int i = 0; i < period; i++)
        {
            kernel[i] /= weightSum;
        }

        return kernel;
    }
}
