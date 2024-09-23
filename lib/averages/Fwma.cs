namespace QuanTAlib;

public class Fwma : AbstractBase
{
    private readonly int _period;
    private readonly Convolution _convolution;

    public Fwma(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _convolution = new Convolution(GenerateKernel(_period));
        Name = "Fwma";
        WarmupPeriod = period;
        Init();
    }

    public Fwma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

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