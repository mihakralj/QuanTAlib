public class TEMA
{
    private readonly int _period;
    private int _index, _hotIndex;
    public TValue Tick { get; private set; }
    public int Period => Math.Min(_index, _period);
    public event Signal Pub = delegate { };
    private bool IsHot => _index > _period;
    private double _k;
    private double _lastEMA1, _lastEMA2, _lastEMA3, _lastTEMA;
    private double _lastEMA1Candidate, _lastEMA2Candidate, _lastEMA3Candidate;

    public TEMA(int period)
    {
        _period = period;
        Init();
    }

    public TEMA(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new Signal(Sub));
    }

    public void Init()
    {
        Tick = default;
        _index = _hotIndex = 0;
        _k = 2.0 / (_period + 1);
        _lastEMA1 = _lastEMA2 = _lastEMA3 = _lastTEMA = 0;
        _lastEMA1Candidate = _lastEMA2Candidate = _lastEMA3Candidate = 0;
    }

    public TValue Update(TValue input)
    {
        if (!input.IsHot && input.IsNew) { _hotIndex++; }

        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value))
        {
            Tick = new TValue(input.Time, _lastTEMA, input.IsNew, _index > _period);
            Pub?.Invoke(this, new ValueEventArgs(Tick));
            return Tick;
        }

        if (input.IsNew)
        {
            if (_index < 1) { _lastEMA1 = _lastEMA2 = _lastEMA3 = input.Value; }
            _lastEMA1Candidate = _lastEMA1;
            _lastEMA2Candidate = _lastEMA2;
            _lastEMA3Candidate = _lastEMA3;
            _index++;
        }
        else
        {
            if (_index <= 1) { _lastEMA1Candidate = _lastEMA2Candidate = _lastEMA3Candidate = input.Value; }
            _lastEMA1 = _lastEMA1Candidate;
            _lastEMA2 = _lastEMA2Candidate;
            _lastEMA3 = _lastEMA3Candidate;
        }

        double kk = (_index <= _period) ? (2.0 / (_index + 1)) : _k;

        // Calculate first EMA
        double ema1 = (input.Value - _lastEMA1) * kk + _lastEMA1;
        _lastEMA1 = ema1;

        // Calculate second EMA (EMA of EMA)
        double ema2 = (ema1 - _lastEMA2) * kk + _lastEMA2;
        _lastEMA2 = ema2;

        // Calculate third EMA (EMA of EMA of EMA)
        double ema3 = (ema2 - _lastEMA3) * kk + _lastEMA3;
        _lastEMA3 = ema3;

        // Calculate TEMA
        double tema = 3 * ema1 - 3 * ema2 + ema3;
        _lastTEMA = tema;

        Tick = new TValue(input.Time, tema, input.IsNew, _index > (_period + _hotIndex));
        Pub?.Invoke(this, new ValueEventArgs(Tick));
        return Tick;
    }

    public void Sub(object source, ValueEventArgs args)
    {
        Update(args.Tick);
    }
}