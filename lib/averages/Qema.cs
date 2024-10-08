namespace QuanTAlib;

public class Qema : AbstractBase
{
    private readonly double _k1, _k2, _k3, _k4;
    private readonly Ema _ema1, _ema2, _ema3, _ema4;
    private double _lastQema, _p_lastQema;

<<<<<<< HEAD
    public Qema(double k1 = 0.2, double k2 = 0.2, double k3 = 0.2, double k4 = 0.2) : base()
=======
    public Qema(double k1 = 0.2, double k2 = 0.2, double k3 = 0.2, double k4 = 0.2)
>>>>>>> dev
    {
        if (k1 <= 0 || k2 <= 0 || k3 <= 0 || k4 <= 0)
        {
            throw new ArgumentOutOfRangeException("All k values must be in the range (0, 1].");
        }

        _k1 = k1;
        _k2 = k2;
        _k3 = k3;
        _k4 = k4;

        _ema1 = new Ema(k1);
        _ema2 = new Ema(k2);
        _ema3 = new Ema(k3);
        _ema4 = new Ema(k4);

        Name = $"QEMA ({k1:F2},{k2:F2},{k3:F2},{k4:F2})";
        double smK = Math.Min(Math.Min(_k1, _k2), Math.Min(_k3, _k4));

        WarmupPeriod = (int)((2 - smK) / smK);
        Init();
    }

    public Qema(object source, double k1, double k2, double k3, double k4)
        : this(k1, k2, k3, k4)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _lastQema = 0;
        _p_lastQema = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_lastQema = _lastQema;
            _index++;
        }
        else
        {
            _lastQema = _p_lastQema;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double ema1 = _ema1.Calc(new TValue(Input.Time, Input.Value, Input.IsNew));
        double ema2 = _ema2.Calc(new TValue(Input.Time, ema1, Input.IsNew));
        double ema3 = _ema3.Calc(new TValue(Input.Time, ema2, Input.IsNew));
        double ema4 = _ema4.Calc(new TValue(Input.Time, ema3, Input.IsNew));

        _lastQema = 4 * ema1 - 6 * ema2 + 4 * ema3 - ema4;

        IsHot = _index >= WarmupPeriod;
        return _lastQema;
    }
}