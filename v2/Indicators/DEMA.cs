public class DEMA
{
    private readonly int _period;
    private int _index, _hotIndex;
    public TValue Tick { get; private set; }
    public int Period => Math.Min(_index, _period);
    public event Signal Pub = delegate { };
    private bool IsHot => _index > _period;
    private double _k;
    private double _lastEMA1, _lastEMA2, _lastDEMA;
    private double _lastEMA1Candidate, _lastEMA2Candidate;

    public DEMA(int period)
    {
        _period = period;
        Init();
    }

    public DEMA(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new Signal(Sub));
    }

    public void Init()
    {
        Tick = default;
        _index = _hotIndex = 0;
        _k = 2.0 / (_period + 1);
        _lastEMA1 = _lastEMA2 = _lastDEMA = 0;
        _lastEMA1Candidate = _lastEMA2Candidate = 0;
    }

    public TValue Update(TValue input)
    {
        if (!input.IsHot && input.IsNew) { _hotIndex++; }

        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value))
        {
            Tick = new TValue(input.Time, _lastDEMA, input.IsNew, _index > _period);
            Pub?.Invoke(this, new ValueEventArgs(Tick));
            return Tick;
        }

        if (input.IsNew)
        {
            if (_index < 1) { _lastEMA1 = _lastEMA2 = input.Value; }
            _lastEMA1Candidate = _lastEMA1;
            _lastEMA2Candidate = _lastEMA2;
            _index++;
        }
        else
        {
            if (_index <= 1) { _lastEMA1Candidate = _lastEMA2Candidate = input.Value; }
            _lastEMA1 = _lastEMA1Candidate;
            _lastEMA2 = _lastEMA2Candidate;
        }

        double kk = (_index <= _period) ? (2.0 / (_index + 1)) : _k;

        // Calculate first EMA
        double ema1 = (input.Value - _lastEMA1) * kk + _lastEMA1;
        _lastEMA1 = ema1;

        // Calculate second EMA (EMA of EMA)
        double ema2 = (ema1 - _lastEMA2) * kk + _lastEMA2;
        _lastEMA2 = ema2;

        // Calculate DEMA
        double dema = 2 * ema1 - ema2;
        _lastDEMA = dema;

        Tick = new TValue(input.Time, dema, input.IsNew, _index > (_period + _hotIndex));
        Pub?.Invoke(this, new ValueEventArgs(Tick));
        return Tick;
    }
    public void Sub(object source, ValueEventArgs args) {
        Update(args.Tick);
    }
}