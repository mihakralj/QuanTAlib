namespace QuanTAlib;

public class Gma : AbstractBase
{
    private readonly int _period;
    private readonly Convolution _convolution;

    public Gma(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _convolution = new Convolution(GenerateKernel(_period));
        Name = "Gma";
        WarmupPeriod = period;
        Init();
    }

    public Gma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public static double[] GenerateKernel(int period, double sigma = 1.0)
    {
        double[] kernel = new double[period];
        double weightSum = 0;
        int center = period / 2;

        for (int i = 0; i < period; i++)
        {
            double x = (i - center) / (double)center;
            kernel[i] = Math.Exp(-(x * x) / (2 * sigma * sigma));
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