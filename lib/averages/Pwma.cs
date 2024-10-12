namespace QuanTAlib;

public class Pwma : AbstractBase
{
    private readonly int _period;
    private readonly Convolution _convolution;

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
