using System.Runtime.CompilerServices;
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
    private readonly double[] _kernel;

    /// <param name="period">The number of data points used in the SINEMA calculation.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Sinema(int period)
    {
        if (period < 1)
        {
            throw new System.ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _kernel = GenerateKernel(period);
        _convolution = new Convolution(_kernel);
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

    /// <summary>
    /// Generates the sine-based convolution kernel for the SINEMA calculation.
    /// </summary>
    /// <param name="period">The period for which to generate the kernel.</param>
    /// <returns>An array of normalized sine-based weights for the convolution operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double[] GenerateKernel(int period)
    {
        double[] kernel = new double[period];
        double weightSum = 0;
        double piDivPeriodPlus1 = System.Math.PI / (period + 1);

        // Calculate weights and sum in one pass
        for (int i = 0; i < period; i++)
        {
            kernel[i] = System.Math.Sin((i + 1) * piDivPeriodPlus1);
            weightSum += kernel[i];
        }

        // Normalize using multiplication instead of division
        double invWeightSum = 1.0 / weightSum;
        for (int i = 0; i < period; i++)
        {
            kernel[i] *= invWeightSum;
        }

        return kernel;
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Use Convolution for calculation
        var convolutionResult = _convolution.Calc(Input);
        IsHot = _index >= WarmupPeriod;

        return convolutionResult.Value;
    }
}
