public class EMA
{
    private readonly int _period;
    private int _index, _hotIndex;
    public TValue Value { get; private set; }
    public bool IsHot => _index > _period;
    public int Period => Math.Min(_index, _period);
    private double _k;
    private double _lastEMA, _lastEMACandidate;

    public EMA(int period) {
        _period = period;
        Init();
    }

    public EMA(object source, int period) : this(period) {
        var sourceType = source.GetType();
        var updateMethod = sourceType.GetMethod("Update", new[] { typeof(TValue) });
        if (updateMethod != null) {
            var pubEvent = sourceType.GetEvent("Pub");
            if (pubEvent != null && pubEvent.EventHandlerType == typeof(Signal)) {
                pubEvent.AddEventHandler(source, new Signal(Sub));
            } else {
                throw new ArgumentException("Source object must have a Pub event of type NewValue.");
            }
        } else {
            throw new ArgumentException("Source object must have an Update(TValue) method.");
        }
    }

    public void Init() {
        Value = default;
        _index = _hotIndex = 0;
        _k = 2.0 / (_period + 1);
        _lastEMA = 0;
        _lastEMACandidate = 0;
    }

    public TValue Update(TValue input) {
        if (!input.IsHot && input.IsNew) { _hotIndex++; }

        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value)) { 
            Value = new TValue(input.Time, _lastEMA, input.IsNew, _index > _period);
            Pub?.Invoke(this, Value);
            return Value;
        }
        if (input.IsNew) {
            if (_index < 1) { _lastEMA = input.Value; }
            _lastEMACandidate = _lastEMA;
            _index++;
        } else {
            if (_index <= 1) { _lastEMACandidate = input.Value; }
            _lastEMA = _lastEMACandidate;
        }

        double kk = (_index <= _period) ? (2.0 / (_index + 1)) : _k;

        double ema = (input.Value - _lastEMA) * kk + _lastEMA;
        _lastEMA = ema;

        Value = new TValue(input.Time, ema, input.IsNew, _index > (_period + _hotIndex));
        Pub?.Invoke(this, Value);
        return Value;
    }

    public void Sub(object source, TValue arg) {
        Update(arg);
    }

    public event Signal Pub;
}