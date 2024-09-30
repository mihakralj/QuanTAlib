namespace QuanTAlib;

public class T3 : AbstractBase {
    private readonly int _period;
    private readonly bool _useSma;
    private readonly double _k, _c1, _c2, _c3, _c4;
    private readonly CircularBuffer _buffer1, _buffer2, _buffer3, _buffer4, _buffer5, _buffer6;
    private double _lastEma1, _lastEma2, _lastEma3, _lastEma4, _lastEma5, _lastEma6;
    private double _p_lastEma1, _p_lastEma2, _p_lastEma3, _p_lastEma4, _p_lastEma5, _p_lastEma6;

    public T3(int period, double vfactor = 0.7, bool useSma = true) {
        if (period < 1) {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _useSma = useSma;
        WarmupPeriod = period;

        _k = 2.0 / (_period + 1);
        _c1 = -vfactor * vfactor * vfactor;
        _c2 = 3 * vfactor * vfactor + 3 * vfactor * vfactor * vfactor;
        _c3 = -6 * vfactor * vfactor - 3 * vfactor - 3 * vfactor * vfactor * vfactor;
        _c4 = 1 + 3 * vfactor + vfactor * vfactor * vfactor + 3 * vfactor * vfactor;

        _buffer1 = new(period);
        _buffer2 = new(period);
        _buffer3 = new(period);
        _buffer4 = new(period);
        _buffer5 = new(period);
        _buffer6 = new(period);


        Name = $"T3({_period}, {vfactor})";
        Init();
    }

    public T3(object source, int period, double vfactor = 0.7, bool useSma = true) : this(period, vfactor, useSma) {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init() {
        _lastEma1 = _lastEma2 = _lastEma3 = _lastEma4 = _lastEma5 = _lastEma6 = 0;
        _buffer1.Clear();
        _buffer2.Clear();
        _buffer3.Clear();
        _buffer4.Clear();
        _buffer5.Clear();
        _buffer6.Clear();
    }

    protected override void ManageState(bool isNew) {
        if (isNew) {
            _lastValidValue = Input.Value;
            _index++;
            _p_lastEma1 = _lastEma1;
            _p_lastEma2 = _lastEma2;
            _p_lastEma3 = _lastEma3;
            _p_lastEma4 = _lastEma4;
            _p_lastEma5 = _lastEma5;
            _p_lastEma6 = _lastEma6;
        } else {
            _lastEma1 = _p_lastEma1;
            _lastEma2 = _p_lastEma2;
            _lastEma3 = _p_lastEma3;
            _lastEma4 = _p_lastEma4;
            _lastEma5 = _p_lastEma5;
            _lastEma6 = _p_lastEma6;
        }
    }


    protected override double Calculation() {
        ManageState(Input.IsNew);

        double ema1, ema2, ema3, ema4, ema5, ema6;

        if (_index == 1) {
            ema1 = ema2 = ema3 = ema4 = ema5 = ema6 = Input.Value;
        } else if (_index <= _period && _useSma) {
            _buffer1.Add(Input.Value, Input.IsNew);
            ema1 = _buffer1.Average();
            _buffer2.Add(ema1, Input.IsNew);
            ema2 = _buffer2.Average();
            _buffer3.Add(ema2, Input.IsNew);
            ema3 = _buffer3.Average();
            _buffer4.Add(ema3, Input.IsNew);
            ema4 = _buffer4.Average();
            _buffer5.Add(ema4, Input.IsNew);
            ema5 = _buffer5.Average();
            _buffer6.Add(ema5, Input.IsNew);
            ema6 = _buffer6.Average();
        } else {
            ema1 = _k * (Input.Value - _lastEma1) + _lastEma1;
            ema2 = _k * (ema1 - _lastEma2) + _lastEma2;
            ema3 = _k * (ema2 - _lastEma3) + _lastEma3;
            ema4 = _k * (ema3 - _lastEma4) + _lastEma4;
            ema5 = _k * (ema4 - _lastEma5) + _lastEma5;
            ema6 = _k * (ema5 - _lastEma6) + _lastEma6;
        }

        _lastEma1 = ema1;
        _lastEma2 = ema2;
        _lastEma3 = ema3;
        _lastEma4 = ema4;
        _lastEma5 = ema5;
        _lastEma6 = ema6;

        double t3 = _c1 * ema6 + _c2 * ema5 + _c3 * ema4 + _c4 * ema3;

        IsHot = _index >= WarmupPeriod;
        return t3;
    }
}