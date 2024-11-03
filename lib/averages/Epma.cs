using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// EPMA: Endpoint Moving Average
/// A moving average that uses a specialized convolution kernel to emphasize recent price movements
/// while maintaining a connection to historical data. The weights decrease linearly with a focus
/// on endpoints.
/// </summary>
/// <remarks>
/// The EPMA uses a unique weighting scheme where:
/// - The most recent price gets the highest weight: (2 * period - 1)
/// - Each previous price gets a weight reduced by 3: (2 * period - 1) - 3i
/// - Weights are normalized to sum to 1
///
/// Key characteristics:
/// - Emphasizes recent price movements more than traditional moving averages
/// - Maintains some influence from historical data
/// - Uses convolution for efficient calculation
/// - Provides better endpoint preservation than simple moving averages
///
/// Implementation:
///     Original implementation based on convolution principles
/// </remarks>
public class Epma : AbstractBase
{
    private readonly int _period;
    private readonly Convolution _convolution;

    /// <param name="period">The number of data points used in the EPMA calculation.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Epma(int period)
    {
        if (period < 1)
        {
            throw new System.ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        double[] _baseKernel = GenerateKernel(_period);
        _convolution = new Convolution(_baseKernel);
        Name = "Epma";
        WarmupPeriod = period;
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of data points used in the EPMA calculation.</param>
    public Epma(object source, int period) : this(period)
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
    private static double CalculateKernelSum(int period)
    {
        // Using arithmetic sequence sum formula: n(a1 + an)/2
        // where a1 = (2p-1) and an = (2p-1) - 3(n-1)
        double firstTerm = (2 * period) - 1;
        double lastTerm = firstTerm - (3 * (period - 1));
        return period * (firstTerm + lastTerm) * 0.5;
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
            result *= CalculateKernelSum(_period) / CalculateKernelSum(_index);
        }

        IsHot = _index >= WarmupPeriod;
        return result;
    }

    /// <summary>
    /// Generates the convolution kernel for the EPMA calculation.
    /// </summary>
    /// <param name="period">The period for which to generate the kernel.</param>
    /// <returns>An array of normalized weights for the convolution operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double[] GenerateKernel(int period)
    {
        double[] kernel = new double[period];
        double weightSum = CalculateKernelSum(period);
        double invWeightSum = 1.0 / weightSum;
        double baseWeight = (2 * period) - 1;

        for (int i = 0; i < period; i++)
        {
            kernel[i] = (baseWeight - (3 * i)) * invWeightSum;
        }

        return kernel;
    }
}
