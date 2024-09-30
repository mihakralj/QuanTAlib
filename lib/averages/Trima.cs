namespace QuanTAlib;

public class Trima : AbstractBase
{
    private readonly Convolution _convolution;

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

    public Trima(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    private static double[] GenerateKernel(int period)
    {
        double[] kernel = new double[period];
        int halfPeriod = (period + 1) / 2;
        double weightSum = 0;

        for (int i = 0; i < period; i++)
        {
            if (i < halfPeriod)
            {
                kernel[i] = i + 1;
            }
            else
            {
                kernel[i] = period - i;
            }
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