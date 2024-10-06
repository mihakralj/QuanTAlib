namespace QuanTAlib;

public class Hwma : AbstractBase
{
    private readonly int _period;
    private readonly double _nA, _nB, _nC;
    private double _pF, _pV, _pA;
    private double _ppF, _ppV, _ppA;

    public Hwma(int period) : this(period, 2.0 / (1 + period), 1.0 / period, 1.0 / period)
    {
    }

    public Hwma(double nA, double nB, double nC) : this((int)((2 - nA) / nA), nA, nB, nC)
    {
    }

    public Hwma(int period, double nA, double nB, double nC)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _nA = nA;
        _nB = nB;
        _nC = nC;
        WarmupPeriod = period;
        Name = $"Hwma({_period})";
        Init();
    }

    public Hwma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _pF = _pV = _pA = 0;
        _ppF = _ppV = _ppA = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
            _ppF = _pF;
            _ppV = _pV;
            _ppA = _pA;
        }
        else
        {
            _pF = _ppF;
            _pV = _ppV;
            _pA = _ppA;

        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_index == 1)
        {
            _pF = Input.Value;
            _pA = _pV = 0;
        }

        double nA = _nA, nB = _nB, nC = _nC;
        if (_period == 1)
        {
            nA = 1;
            nB = 0;
            nC = 0;
        }

        double F = (1 - nA) * (_pF + _pV + 0.5 * _pA) + nA * Input.Value;
        double V = (1 - nB) * (_pV + _pA) + nB * (F - _pF);
        double A = (1 - nC) * _pA + nC * (V - _pV);

        double hwma = F + V + 0.5 * A;

        _pF = F;
        _pV = V;
        _pA = A;

        IsHot = _index >= WarmupPeriod;
        return hwma;
    }
}