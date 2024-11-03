using System.Runtime.CompilerServices;
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
    private readonly double[] _kernel;

    /// <param name="period">The number of data points used in the PWMA calculation.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Pwma(int period)
    {
        if (period < 1)
        {
            throw new System.ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _kernel = GenerateKernel(_period);
        _convolution = new Convolution(_kernel);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private new void Init()
    {
        base.Init();
        _convolution.Init();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateKernelSum(double[] kernel, int length)
    {
        double sum = 0;
        for (int i = 0; i < length; i++)
        {
            sum += kernel[i];
        }
        return sum;
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Use Convolution for calculation
        var convolutionResult = _convolution.Calc(Input);
        double result = convolutionResult.Value;

        // Adjust for partial periods during warmup
        if (_index < _period)
        {
            double[] partialKernel = GenerateKernel(_index);
            result *= CalculateKernelSum(_kernel, _period) / CalculateKernelSum(partialKernel, _index);
        }

        IsHot = _index >= WarmupPeriod;
        return result;
    }

    /// <summary>
    /// Generates the Pascal's triangle-based convolution kernel for the PWMA calculation.
    /// </summary>
    /// <param name="period">The period for which to generate the kernel.</param>
    /// <returns>An array of normalized Pascal's triangle-based weights for the convolution operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double[] GenerateKernel(int period)
    {
        double[] kernel = new double[period];
        kernel[0] = 1;

        // Generate Pascal's triangle coefficients
        for (int i = 1; i < period; i++)
        {
            for (int j = i; j > 0; j--)
            {
                kernel[j] += kernel[j - 1];
            }
        }

        // Calculate sum and normalize in one pass
        double weightSum = CalculateKernelSum(kernel, period);
        double invWeightSum = 1.0 / weightSum;

        for (int i = 0; i < period; i++)
        {
            kernel[i] *= invWeightSum;
        }

        return kernel;
    }
}
