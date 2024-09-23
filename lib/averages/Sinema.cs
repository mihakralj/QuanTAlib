namespace QuanTAlib;

public class Sinema : AbstractBase
{
    private readonly int _period;
    private readonly Convolution _convolution;

    public Sinema(int period)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _convolution = new Convolution(GenerateKernel(_period));
        Name = "Sinema";
        WarmupPeriod = period;
        Init();
    }

    public Sinema(object source, int period) : this(period)
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
        IsHot = _index >= WarmupPeriod;

        return result;
    }

    public static double[] GenerateKernel(int period)
    {
        double[] kernel = new double[period];
        double weightSum = 0;

        for (int i = 0; i < period; i++)
        {
            // Use sine function to generate weights
            kernel[i] = Math.Sin((i + 1) * Math.PI / (period + 1));
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