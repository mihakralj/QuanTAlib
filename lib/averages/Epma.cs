namespace QuanTAlib;

public class Epma : AbstractBase
{
    private readonly int _period;
    private readonly Convolution _convolution;

    public Epma(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _convolution = new Convolution(GenerateKernel(_period));
        Name = "Epma";
        WarmupPeriod = period;
        Init();
    }

    public Epma(object source, int period) : this(period)
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

    public static double[] GenerateKernel(int period)
    {
        double[] kernel = new double[period];
        double weightSum = 0;

        for (int i = 0; i < period; i++)
        {
            kernel[i] = (2 * period - 1) - 3 * i;
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