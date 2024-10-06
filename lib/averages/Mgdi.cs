namespace QuanTAlib;

public class Mgdi : AbstractBase
{
    private readonly int _period;
    private readonly double _kFactor;
    private double _prevMd, _p_prevMd;
    public Mgdi(int period, double kFactor = 0.6) : base()
    {
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 0.");
        }
        if (kFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(kFactor), "K-Factor must be greater than 0.");
        }
        _period = period;
        _kFactor = kFactor;
        Name = "Mgdi";
        WarmupPeriod = period;
        Init();
    }

    public Mgdi(object source, int period, double kFactor = 1.0) : this(period, kFactor)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _prevMd = _p_prevMd = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_prevMd = _prevMd;
            _index++;
        }
        else
        {
            _prevMd = _p_prevMd;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double value = Input.Value;
        if (_index < 2)
        {
            _prevMd = value;
        }
        else
        {
            double md = _prevMd + ((value - _prevMd) /
                (_kFactor * _period * Math.Pow(value / _prevMd, 4)));
            _prevMd = md;
        }

        IsHot = _index >= _period;
        return _prevMd;
    }
}