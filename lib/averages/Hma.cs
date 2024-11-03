using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// HMA: Hull Moving Average
/// A moving average designed by Alan Hull to reduce lag while maintaining smoothness.
/// It combines weighted moving averages of different periods to achieve better
/// responsiveness to price changes while minimizing noise.
/// </summary>
/// <remarks>
/// The HMA calculation process:
/// 1. Calculate WMA with period n/2
/// 2. Calculate WMA with period n
/// 3. Calculate difference: 2*WMA(n/2) - WMA(n)
/// 4. Apply final WMA with period sqrt(n) to the difference
///
/// Key characteristics:
/// - Significantly reduced lag compared to traditional moving averages
/// - Maintains smoothness despite the reduced lag
/// - Responds more quickly to price changes
/// - Better at identifying trend changes
/// - Uses weighted moving averages for all calculations
///
/// Sources:
///     Alan Hull - "Better Trading with Hull Moving Average"
///     https://alanhull.com/hull-moving-average
/// </remarks>
public class Hma : AbstractBase
{
    private readonly Convolution _wmaHalf, _wmaFull, _wmaFinal;
    private readonly int _period;
    private readonly int _sqrtPeriod;
    private readonly double[] _kernelHalf;
    private readonly double[] _kernelFull;
    private readonly double[] _kernelFinal;

    /// <param name="period">The number of data points used in the HMA calculation. Must be at least 2.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 2.</exception>
    public Hma(int period)
    {
        if (period < 2)
        {
            throw new System.ArgumentException("Period must be greater than or equal to 2.", nameof(period));
        }
        _period = period;
        _sqrtPeriod = (int)System.Math.Sqrt(period);

        // Generate all kernels once
        _kernelHalf = GenerateWmaKernel(period / 2);
        _kernelFull = GenerateWmaKernel(period);
        _kernelFinal = GenerateWmaKernel(_sqrtPeriod);

        // Initialize convolutions with pre-generated kernels
        _wmaHalf = new Convolution(_kernelHalf);
        _wmaFull = new Convolution(_kernelFull);
        _wmaFinal = new Convolution(_kernelFinal);

        Name = "Hma";
        WarmupPeriod = period + _sqrtPeriod - 1;
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of data points used in the HMA calculation.</param>
    public Hma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Generates the weighted moving average kernel for the HMA calculation.
    /// </summary>
    /// <param name="period">The period for which to generate the kernel.</param>
    /// <returns>An array of linearly weighted values for the convolution operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double[] GenerateWmaKernel(int period)
    {
        double[] kernel = new double[period];
        double weightSum = period * (period + 1) * 0.5; // Multiply by 0.5 instead of dividing by 2
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
        _wmaHalf.Init();
        _wmaFull.Init();
        _wmaFinal.Init();
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

        // Calculate WMA(n/2) and WMA(n)
        double wmaHalfResult = _wmaHalf.Calc(Input).Value;
        double wmaFullResult = _wmaFull.Calc(Input).Value;

        // Calculate 2*WMA(n/2) - WMA(n)
        double intermediateResult = (2.0 * wmaHalfResult) - wmaFullResult;

        // Calculate final WMA
        var finalInput = new TValue(Input.Time, intermediateResult, Input.IsNew);
        double result = _wmaFinal.Calc(finalInput).Value;

        IsHot = _index >= WarmupPeriod;
        return result;
    }
}
