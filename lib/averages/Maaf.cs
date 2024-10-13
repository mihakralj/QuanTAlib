namespace QuanTAlib;

// https://efs.kb.esignal.com/hc/en-us/articles/6362791434395-2005-Mar-The-Secret-Behind-The-Filter-MedianAdaptiveFilter-efs

//TODO Fix initial values

public class Maaf : AbstractBase
{
    private readonly CircularBuffer _priceBuffer;
    private readonly CircularBuffer _smoothBuffer;
    private double _prevFilter, _prevValue2;
    private readonly double _threshold;
    private double _p_prevFilter, _p_prevValue2;

    private readonly int _period;

    public Maaf(int period = 39, double threshold = 0.002)
    {
        _period = period;
        _threshold = threshold;
        _priceBuffer = new CircularBuffer(4);
        _smoothBuffer = new CircularBuffer(period);
        Name = "MAAF";
        WarmupPeriod = period;
        Init();
    }

    public Maaf(object source, int period = 39, double threshold = 0.002) : this(period, threshold)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        _priceBuffer.Clear();
        _smoothBuffer.Clear();
        _prevFilter = 0;
        _prevValue2 = 0;
        base.Init();
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
            _p_prevFilter = _prevFilter;
            _p_prevValue2 = _prevValue2;
        }
        else
        {
            _prevFilter = _p_prevFilter;
            _prevValue2 = _p_prevValue2;
        }
    }

    protected override double Calculation()
    {
        ManageState(IsNew);

        _priceBuffer.Add(Input.Value, Input.IsNew);

        if (_priceBuffer.Count < 4)
        {
            return Input.Value;
        }

        double smooth = (_priceBuffer[^1] + (2 * _priceBuffer[^2]) + (2 * _priceBuffer[^3]) + _priceBuffer[^4]) / 6;
        _smoothBuffer.Add(smooth, Input.IsNew);


        if (_smoothBuffer.Count < _period)
        {
            return smooth;
        }

        int length = _period;
        double value3 = 0.2;
        double value2 = _prevValue2;

        while (value3 > _threshold && length > 0)
        {
            double alpha = 2.0 / (length + 1);

            var sortedValues = _smoothBuffer.TakeLast(length).OrderBy(x => x).ToList();
            double value1 = sortedValues[length / 2];
            value2 = alpha * (smooth - _prevValue2) + _prevValue2;

            if (value1 != 0)
            {
                value3 = Math.Abs(value1 - value2) / value1;
            }

            length -= 2;
        }

        if (length < 3) length = 3;

        double finalAlpha = 2.0 / (length + 1);
        double filter = finalAlpha * (smooth - _prevFilter) + _prevFilter;

        _p_prevFilter = _prevFilter;
        _prevFilter = filter;
        _p_prevValue2 = _prevValue2;
        _prevValue2 = value2;

        IsHot = _index >= WarmupPeriod;
        return filter;
    }
}
