using System;
namespace QuanTAlib;

/// <summary>
/// TRIMA: Triangular Moving Average
/// A moving average that uses triangular-shaped weights that increase linearly to
/// the middle of the period and then decrease linearly. This creates a smoother
/// output than simple moving averages.
/// </summary>
/// <remarks>
/// The TRIMA calculation process:
/// 1. Generates triangular weights that peak at the center
/// 2. Weights increase linearly to middle point
/// 3. Weights decrease linearly from middle point
/// 4. Applies normalized weights through convolution
///
/// Key characteristics:
/// - Smoother than simple moving average
/// - Natural emphasis on central values
/// - Reduced noise sensitivity
/// - Double smoothing effect
/// - Implemented using efficient convolution operations
///
/// Sources:
///     https://www.investopedia.com/terms/t/triangularaverage.asp
///     Technical Analysis of Stocks & Commodities magazine
/// </remarks>

public class Trima : AbstractBase
{
    private readonly Convolution _convolution;

    /// <param name="period">The number of data points used in the TRIMA calculation.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Trima(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _convolution = new Convolution(GenerateKernel(period));
        Name = "Trima";
        WarmupPeriod = period;
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of data points used in the TRIMA calculation.</param>
    public Trima(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Generates the triangular-shaped convolution kernel for the TRIMA calculation.
    /// </summary>
    /// <param name="period">The period for which to generate the kernel.</param>
    /// <returns>An array of normalized triangular weights for the convolution operation.</returns>
    private static double[] GenerateKernel(int period)
    {
        double[] kernel = new double[period];
        int halfPeriod = (period + 1) / 2;
        double weightSum = 0;

        for (int i = 0; i < period; i++)
        {
            kernel[i] = i < halfPeriod ? i + 1 : period - i;
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
