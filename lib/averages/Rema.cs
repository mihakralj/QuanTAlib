using QuanTAlib;

//https://user42.tuxfamily.org/chart/manual/Regularized-Exponential-Moving-Average.html

public class Rema : AbstractBase
{
    private readonly int _period;
    private readonly double _lambda;
    private double _lastRema, _prevRema;
    private double _savedLastRema, _savedPrevRema;

    public int Period => _period;
    public double Lambda => _lambda;

    public Rema(int period, double lambda = 0.5) : base()
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        if (lambda < 0)
            throw new ArgumentOutOfRangeException(nameof(lambda), "Lambda must be non-negative.");

        _period = period;
        _lambda = lambda;
        Name = $"REMA({period},{lambda:F2})";
        WarmupPeriod = period;
        Init();
    }

    public override void Init()
    {
        base.Init();
        _lastRema = 0;
        _prevRema = 0;
        _savedLastRema = 0;
        _savedPrevRema = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _savedLastRema = _lastRema;
            _savedPrevRema = _prevRema;
            _index++;
        }
        else
        {
            _lastRema = _savedLastRema;
            _prevRema = _savedPrevRema;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double alpha = 2.0 / (Math.Min(_period, _index) + 1);

        if (_index > 2)
        {
            double rema = (_lastRema + alpha * (Input.Value - _lastRema) + _lambda * (_lastRema + (_lastRema - _prevRema))) / (1 + _lambda);
            _prevRema = _lastRema;
            _lastRema = rema;
        }
        else if (_index == 2)
        {
            _prevRema = _lastRema;
            _lastRema = Input.Value;
        }
        else
        { // _index == 1
            _lastRema = Input.Value;
        }

        IsHot = _index >= WarmupPeriod;
        return _lastRema;
    }
}