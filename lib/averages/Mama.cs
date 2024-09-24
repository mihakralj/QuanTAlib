namespace QuanTAlib;


public class Mama : AbstractBase
{
    private readonly double _fastLimit, _slowLimit;
    private readonly CircularBuffer _pr, _sm, _dt, _i1, _q1, _i2, _q2, _re, _im, _pd, _ph;
    private double _mama, _fama;
    private double _prevMama, _prevFama, _sumPr;
    private double _p_prevMama, _p_prevFama, _p_sumPr;

    public TValue Fama { get; private set; }

    public Mama(double fastLimit = 0.5, double slowLimit = 0.05)
    {
        Fama = new TValue();
        Name = $"Mama({_fastLimit:F2}, {_slowLimit:F2})";
        _fastLimit = fastLimit;
        _slowLimit = slowLimit;
        _pr = new(7);
        _sm = new(7);
        _dt = new(7);
        _q1 = new(7);
        _i1 = new(7);
        _i2 = new(2);
        _q2 = new(2);
        _re = new(2);
        _im = new(2);
        _pd = new(2);
        _ph = new(2);
        Init();
    }

    public Mama(object source, double fastLimit = 0.5, double slowLimit = 0.05) : this(fastLimit, slowLimit)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        Fama = new TValue();
        base.Init();
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_prevMama = _prevMama;
            _p_prevFama = _prevFama;
            _p_sumPr = _sumPr;
            _lastValidValue = Input.Value;
            _index++;
        }
        else
        {
            _prevMama = _p_prevMama;
            _prevFama = _p_prevFama;
            _sumPr = _p_sumPr;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _pr.Add(Input.Value, Input.IsNew);

        if (_index > 6)
        {
            double adj = (0.075 * _pd[^1]) + 0.54;

            // Smooth
            _sm.Add(((4 * _pr[^1]) + (3 * _pr[^2]) + (2 * _pr[^3]) + _pr[^4]) / 10, Input.IsNew);

            // Detrender
            _dt.Add(((0.0962 * _sm[^1]) + (0.5769 * _sm[^3]) - (0.5769 * _sm[^5]) - (0.0962 * _sm[^7])) * adj, Input.IsNew);

            // In-phase and quadrature
            _q1.Add(((0.0962 * _dt[^1]) + (0.5769 * _dt[^3]) - (0.5769 * _dt[^5]) - (0.0962 * _dt[^7])) * adj, Input.IsNew);
            _i1.Add(_dt[^4], Input.IsNew);

            // Advance the phases by 90 degrees
            double jI = ((0.0962 * _i1[^1]) + (0.5769 * _i1[^3]) - (0.5769 * _i1[^5]) - (0.0962 * _i1[^7])) * adj;
            double jQ = ((0.0962 * _q1[^1]) + (0.5769 * _q1[^3]) - (0.5769 * _q1[^5]) - (0.0962 * _q1[^7])) * adj;

            // Phasor addition for 3-bar averaging
            _i2.Add(_i1[^1] - jQ, Input.IsNew);
            _q2.Add(_q1[^1] + jI, Input.IsNew);
            _i2[^1] = 0.2 * _i2[^1] + 0.8 * _i2[^2];
            _q2[^1] = 0.2 * _q2[^1] + 0.8 * _q2[^2];

            // Homodyne discriminator
            _re.Add((_i2[^1] * _i2[^2]) + (_q2[^1] * _q2[^2]), Input.IsNew);
            _im.Add((_i2[^1] * _q2[^2]) - (_q2[^1] * _i2[^2]), Input.IsNew);
            _re[^1] = (0.2 * _re[^1]) + (0.8 * _re[^2]);
            _im[^1] = (0.2 * _im[^1]) + (0.8 * _im[^2]);

            // Calculate period
            if (_im[^1] != 0 && _re[^1] != 0)
            {
                _pd.Add(2 * Math.PI / Math.Atan(_im[^1] / _re[^1]), Input.IsNew);
            }
            else
            {
                _pd.Add(_pd[^2], Input.IsNew);
            }

            // Adjust period to thresholds
            _pd[^1] = Math.Max(Math.Min(_pd[^1], 1.5 * _pd[^2]), 0.67 * _pd[^2]);
            _pd[^1] = Math.Max(Math.Min(_pd[^1], 50), 6);
            _pd[^1] = (0.2 * _pd[^1]) + (0.8 * _pd[^2]);

            // Determine phase position
            if (_i1[^1] != 0)
            {
                _ph.Add(Math.Atan(_q1[^1] / _i1[^1]) * 180 / Math.PI, Input.IsNew);
            }
            else
            {
                _ph.Add(_ph[^2], Input.IsNew);
            }

            // Change in phase
            double delta = Math.Max(_ph[^2] - _ph[^1], 1);

            // Adaptive alpha value
            double alpha = Math.Max(_fastLimit / delta, _slowLimit);

            // Final indicators
            _mama = alpha * (_pr[^1] - _prevMama) + _prevMama;
            _fama = 0.5 * alpha * (_mama - _prevFama) + _prevFama;

            _prevMama = _mama;
            _prevFama = _fama;
        }
        else
        {
            _pd.Add(0, Input.IsNew);
            _sm.Add(0, Input.IsNew);
            _dt.Add(0, Input.IsNew);
            _i1.Add(0, Input.IsNew);
            _q1.Add(0, Input.IsNew);
            _i2.Add(0, Input.IsNew);
            _q2.Add(0, Input.IsNew);
            _re.Add(0, Input.IsNew);
            _im.Add(0, Input.IsNew);
            _ph.Add(0, Input.IsNew);

            _sumPr += Input.Value;
            _mama = _fama = _prevMama = _prevFama = _sumPr / _index;
        }

        Fama = new TValue(Time: Input.Time, Value: _fama, IsNew: Input.IsNew);
        IsHot = _index >= 6;

        return _mama;
    }
}
