namespace QuanTAlib;

// https://www.mesasoftware.com/papers/TimeWarp.pdf

public class Ltma : AbstractBase
{
    private readonly double _gamma;
    private double _prevL0, _prevL1, _prevL2, _prevL3;
    private double _p_prevL0, _p_prevL1, _p_prevL2, _p_prevL3;

    public double Gamma => _gamma;

    public Ltma(double gamma = 0.1)
    {
        if (gamma < 0 || gamma > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(gamma), "Gamma must be between 0 and 1.");
        }
        _gamma = gamma;
        Name = $"Laguerre({gamma:F2})";
        WarmupPeriod = 4; // Minimum number of samples needed
        Init();
    }

    public Ltma(object source, double gamma = 0.1) : this(gamma)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _prevL0 = _prevL1 = _prevL2 = _prevL3 = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_prevL0 = _prevL0;
            _p_prevL1 = _prevL1;
            _p_prevL2 = _prevL2;
            _p_prevL3 = _prevL3;
            _index++;
        }
        else
        {
            _prevL0 = _p_prevL0;
            _prevL1 = _p_prevL1;
            _prevL2 = _p_prevL2;
            _prevL3 = _p_prevL3;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Laguerre filter calculation
        double _l0 = (1 - _gamma) * Input.Value + _gamma * _prevL0;
        double _l1 = -_gamma * _l0 + _prevL0 + _gamma * _prevL1;
        double _l2 = -_gamma * _l1 + _prevL1 + _gamma * _prevL2;
        double _l3 = -_gamma * _l2 + _prevL2 + _gamma * _prevL3;
        _prevL0 = _l0;
        _prevL1 = _l1;
        _prevL2 = _l2;
        _prevL3 = _l3;

        double filteredValue = (_l0 + 2 * _l1 + 2 * _l2 + _l3) / 6;

        IsHot = _index >= WarmupPeriod;

        return filteredValue;
    }
}
