using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// WMA: Weighted Moving Average
/// A moving average that assigns linearly decreasing weights to older data points.
/// The most recent price has the highest weight, and each older price receives
/// linearly less weight, creating a more responsive average than SMA.
/// </summary>
/// <remarks>
/// The WMA calculation process:
/// 1. Assigns weights linearly decreasing with age
/// 2. Most recent price gets weight of period
/// 3. Each older price gets decremented weight
/// 4. Normalizes weights by sum of weights
/// 5. Applies weights through convolution
///
/// Key characteristics:
/// - Linear weight distribution
/// - More responsive than SMA
/// - Less lag than SMA
/// - Emphasizes recent prices
/// - Implemented using efficient convolution operations
///
/// Sources:
///     https://www.investopedia.com/articles/technical/060401.asp
///     https://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:weighted_moving_average
/// </remarks>
public class Wma : AbstractBase
{
    private readonly int _period;
    private readonly Convolution _convolution;
    private readonly double[] _kernel;

    /// <param name="period">The number of data points used in the WMA calculation.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Wma(int period)
    {
        if (period < 1)
        {
            throw new System.ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _kernel = GenerateWmaKernel(_period);
        _convolution = new Convolution(_kernel);
        Name = "Wma";
        WarmupPeriod = _period;
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of data points used in the WMA calculation.</param>
    public Wma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Generates the linearly weighted convolution kernel for the WMA calculation.
    /// </summary>
    /// <param name="period">The period for which to generate the kernel.</param>
    /// <returns>An array of normalized linearly decreasing weights for the convolution operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double[] GenerateWmaKernel(int period)
    {
        double[] kernel = new double[period];
        double weightSum = period * (period + 1) * 0.5;  // Multiply by 0.5 instead of dividing by 2
        double invWeightSum = 1.0 / weightSum;

        for (int i = 0; i < period; i++)
        {
            kernel[i] = (period - i) * invWeightSum;
        }

        return kernel;
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

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Use Convolution for calculation
        var convolutionResult = _convolution.Calc(Input);
        IsHot = _index >= WarmupPeriod;

        return convolutionResult.Value;
    }
}
