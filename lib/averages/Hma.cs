namespace QuanTAlib;

public class Hma : AbstractBase
{
    private readonly Convolution _wmaHalf, _wmaFull, _wmaFinal;

    public Hma(int period)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be greater than or equal to 2.", nameof(period));
        }
        int _sqrtPeriod = (int)Math.Sqrt(period);
        _wmaHalf = new Convolution(GenerateWmaKernel(period / 2));
        _wmaFull = new Convolution(GenerateWmaKernel(period));
        _wmaFinal = new Convolution(GenerateWmaKernel(_sqrtPeriod));
        Name = "Hma";
        WarmupPeriod = period + _sqrtPeriod - 1;
        Init();
    }

    public Hma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    private static double[] GenerateWmaKernel(int period)
    {
        double[] kernel = new double[period];
        double weightSum = period * (period + 1) / 2.0;

        for (int i = 0; i < period; i++)
        {
            kernel[i] = (period - i) / weightSum;
        }

        return kernel;
    }

    private new void Init()
    {
        base.Init();
        _wmaHalf.Init();
        _wmaFull.Init();
        _wmaFinal.Init();
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

        // Calculate WMA(n/2) and WMA(n)
        double wmaHalfResult = _wmaHalf.Calc(Input).Value;
        double wmaFullResult = _wmaFull.Calc(Input).Value;

        // Calculate 2*WMA(n/2) - WMA(n)
        double intermediateResult = 2 * wmaHalfResult - wmaFullResult;

        // Calculate final WMA
        double result = _wmaFinal.Calc(new TValue(Input.Time, intermediateResult, Input.IsNew)).Value;

        IsHot = _index >= WarmupPeriod;
        return result;
    }
}