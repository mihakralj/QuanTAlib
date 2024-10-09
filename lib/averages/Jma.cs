namespace QuanTAlib;
//TODO fails consistency test
public class Jma : AbstractBase
{
    public readonly int Period;
    private readonly double _phase;
    private readonly int _vshort, _vlong;
    private readonly CircularBuffer _values;
    private readonly CircularBuffer _voltyShort;
    private readonly CircularBuffer _vsumBuff;
    private readonly CircularBuffer _avoltyBuff;

    private double _beta, _len1, _pow1;
    private double _upperBand, _lowerBand, _prevMa1, _prevDet0, _prevDet1, _prevJma;
    private double _p_UpperBand, _p_LowerBand, _p_prevMa1, _p_prevDet0, _p_prevDet1, _p_prevJma;

    public Jma(int period, double phase = 0, int vshort = 10)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        Period = period;
        _vshort = vshort;
        _vlong = 65;
        _phase = Math.Clamp((phase * 0.01) + 1.5, 0.5, 2.5);

        _values = new CircularBuffer(period);
        _voltyShort = new CircularBuffer(vshort);
        _vsumBuff = new CircularBuffer(_vlong);
        _avoltyBuff = new CircularBuffer(2);

        Name = "JMA";
        WarmupPeriod = period * 2;
        Init();
    }

    public Jma(object source, int period, double phase = 0, int vshort = 10) : this(period, phase, vshort)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        _upperBand = _lowerBand = _prevMa1 = _prevDet0 = _prevDet1 = _prevJma = 0.0;
        _p_UpperBand = _p_LowerBand = _p_prevMa1 = _p_prevDet0 = _p_prevDet1 = _p_prevJma = 0.0;
        _beta = 0.45 * (Period - 1) / (0.45 * (Period - 1) + 2);
        _len1 = Math.Max((Math.Log(Math.Sqrt(Period - 1)) / Math.Log(2.0)) + 2.0, 0);
        _pow1 = Math.Max(_len1 - 2.0, 0.5);
        _avoltyBuff.Clear();
        _avoltyBuff.Add(0, true);
        _avoltyBuff.Add(0, true);
        base.Init();
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
            // Save current state
            _p_UpperBand = _upperBand;
            _p_LowerBand = _lowerBand;
            _p_prevMa1 = _prevMa1;
            _p_prevDet0 = _prevDet0;
            _p_prevDet1 = _prevDet1;
            _p_prevJma = _prevJma;
        }
        else
        {
            // Restore previous state
            _upperBand = _p_UpperBand;
            _lowerBand = _p_LowerBand;
            _prevMa1 = _p_prevMa1;
            _prevDet0 = _p_prevDet0;
            _prevDet1 = _p_prevDet1;
            _prevJma = _p_prevJma;

        }
    }
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _values.Add(Input.Value, Input.IsNew);

        if (_index == 1)
        {
            _prevMa1 = _prevJma = Input.Value;
            return Input.Value;
        }

        double hprice = _values.Max();
        double lprice = _values.Min();

        double del1 = hprice - _upperBand;
        double del2 = lprice - _lowerBand;
        double volty = Math.Max(Math.Abs(del1), Math.Abs(del2));

        _voltyShort.Add(volty, Input.IsNew);
        double vsum = _vsumBuff.Newest() + 0.1 * (volty - _voltyShort.Oldest());
        _vsumBuff.Add(vsum, Input.IsNew);

        double prevAvolty = _avoltyBuff.Newest();
        double avolty = prevAvolty + 2.0 / (Math.Max(4.0 * Period, 30) + 1.0) * (vsum - prevAvolty);
        _avoltyBuff.Add(avolty, Input.IsNew);

        double dVolty = (avolty > 0) ? volty / avolty : 0;
        dVolty = Math.Min(Math.Max(dVolty, 1.0), Math.Pow(_len1, 1.0 / _pow1));

        double pow2 = Math.Pow(dVolty, _pow1);
        double len2 = Math.Sqrt(0.5 * (Period - 1)) * _len1;
        double _Kv = Math.Pow(len2 / (len2 + 1), Math.Sqrt(pow2));

        _upperBand = (del1 > 0) ? hprice : hprice - (_Kv * del1);
        _lowerBand = (del2 < 0) ? lprice : lprice - (_Kv * del2);

        double alpha = Math.Pow(_beta, pow2);
        double ma1 = (1 - alpha) * Input.Value + alpha * _prevMa1;
        _prevMa1 = ma1;

        double det0 = (1 - _beta) * (Input.Value - ma1) + _beta * _prevDet0;
        _prevDet0 = det0;
        double ma2 = ma1 + (_phase + 1) * det0;

        double det1 = ((1 - alpha) * (1 - alpha) * (ma2 - _prevJma)) + (alpha * alpha * _prevDet1);
        _prevDet1 = det1;
        double jma = _prevJma + det1;
        _prevJma = jma;

        IsHot = _index >= WarmupPeriod;
        return jma;
    }
}