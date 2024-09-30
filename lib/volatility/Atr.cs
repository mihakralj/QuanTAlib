namespace QuanTAlib;

public class Atr : AbstractBarBase
{
    private readonly Ema _ma;
    private double _prevClose, _p_prevClose;

    public Atr(int period) : base()
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _ma = new(1.0/period);
        WarmupPeriod = _ma.WarmupPeriod;
        Name = $"ATR({period})";
    }

    public Atr(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _ma.Init();
        _prevClose = double.NaN;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_prevClose = _prevClose;
        }
        else
        {
            _prevClose = _p_prevClose;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double trueRange = Math.Max(
            Math.Max(
                Input.High - Input.Low,
                Math.Abs(Input.High - _prevClose)
            ),
            Math.Abs(Input.Low - _prevClose)
        );
        if (_index < 2)
        {
            trueRange = Input.High - Input.Low;
        }

        TValue emaTrueRange = _ma.Calc(new TValue(Input.Time, trueRange, Input.IsNew));
        IsHot = _ma.IsHot;
        _prevClose = Input.Close;

        return emaTrueRange.Value;
    }

}

